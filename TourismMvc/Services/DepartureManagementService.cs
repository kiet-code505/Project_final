using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;
using TourismMvc.Payments;

namespace TourismMvc.Services;

public partial class DepartureService
{
    public async Task PrepareFormAsync(DepartureFormViewModel model)
    {
        model.Tours = await db.Tours.AsNoTracking().OrderBy(t => t.TourName)
            .Select(t => new SelectListItem(t.TourCode + " · " + t.TourName, t.TourID.ToString())).ToListAsync();
        model.Policies = await db.Policies.AsNoTracking().OrderBy(p => p.TemplateName)
            .Select(p => new SelectListItem(p.TemplateName, p.PolicyTemplateID.ToString())).ToListAsync();
        model.Airports = await db.Locations.AsNoTracking().Where(l => l.AirportCode != null && l.AirportCode != "")
            .OrderBy(l => l.AirportCode).Select(l => new SelectListItem(l.AirportName + " (" + l.AirportCode + ")", l.AirportCode)).ToListAsync();
        model.Pricings ??= [];
        model.Transports ??= [];
    }

    public async Task<DepartureFormViewModel?> GetForEditAsync(int id)
    {
        var d = (await GetDetailAsync(id))?.Departure;
        if (d == null) return null;
        var model = new DepartureFormViewModel
        {
            Id = id, Revision = d.UpdatedAt?.Ticks, TourId = d.TourId, DepartureDate = d.DepartureDate,
            AvailableSlots = d.AvailableSlots, Status = d.Status == true, TourGuideInfo = d.TourGuideInfo,
            PolicyTemplateId = d.PolicyTemplateId
        };
        foreach (var price in model.Pricings)
        {
            var existing = d.Pricings.FirstOrDefault(p => p.PassengerType == price.PassengerType);
            if (existing == null) continue;
            price.AgeDescription = existing.AgeDescription;
            price.OriginalPrice = existing.OriginalPrice; price.SalePrice = existing.SalePrice;
        }
        model.Transports = d.Transports.OrderBy(t => t.Type).ThenBy(t => t.TransportID).Select(t => new DepartureTransportInput
        {
            Type = t.Type, Enabled = true, VehicleType = t.VehicleType, VehicleName = t.VehicleName,
            TransportCode = t.TransportCode, StartPoint = t.StartPoint, EndPoint = t.EndPoint,
            DepartTime = t.DepartTime, ArrivalTime = t.ArrivalTime
        }).ToList();
        foreach (var type in Enum.GetValues<TransportType>())
            if (!model.Transports.Any(t => t.Type == type)) model.Transports.Add(new() { Type = type });
        await PrepareFormAsync(model);
        return model;
    }

    public async Task<int?> SaveAsync(int? id, DepartureFormViewModel model, ModelStateDictionary errors)
    {
        model.Id = id;
        model.Pricings ??= []; model.Transports ??= [];
        ValidatePricesAndTransport(model, errors);
        if (!await db.Tours.AnyAsync(t => t.TourID == model.TourId)) errors.AddModelError(nameof(model.TourId), "Tour không tồn tại.");
        if (!await db.Policies.AnyAsync(p => p.PolicyTemplateID == model.PolicyTemplateId)) errors.AddModelError(nameof(model.PolicyTemplateId), "Mẫu chính sách không tồn tại.");
        if (!errors.IsValid) return null;

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var d = id.HasValue
                ? await db.Departures.FromSqlInterpolated($"SELECT * FROM tour_departures WHERE departureid = {id.Value} FOR UPDATE").SingleOrDefaultAsync()
                : new TourDeparture { TourId = model.TourId };
            if (d == null) { errors.AddModelError("", "Lịch khởi hành không còn tồn tại."); return null; }
            if (id.HasValue && (d.TourId != model.TourId || d.UpdatedAt?.Ticks != model.Revision))
            {
                errors.AddModelError("", "Lịch đã thay đổi sau khi bạn mở trang (có thể vừa có booking mới). Vui lòng tải lại trang sửa trước khi lưu."); return null;
            }
            if (!model.DepartureDate.HasValue || (model.DepartureDate != d.DepartureDate && model.DepartureDate <= PaymentTime.Now(clock)))
                errors.AddModelError(nameof(model.DepartureDate), "Ngày khởi hành mới phải ở trong tương lai.");
            // Serialize duplicate checks for this tour, including concurrent create/clone requests.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(73521, {model.TourId})");
            if (await db.Departures.AnyAsync(x => x.TourId == model.TourId && x.DepartureDate == model.DepartureDate && x.DepartureID != (id ?? 0)))
                errors.AddModelError(nameof(model.DepartureDate), "Tour này đã có lịch khởi hành vào thời điểm đã chọn.");
            if (!errors.IsValid) return null;

            if (id.HasValue)
            {
                await db.Entry(d).Collection(x => x.Pricings).LoadAsync();
                await db.Entry(d).Collection(x => x.Transports).LoadAsync();
                db.Pricings.RemoveRange(d.Pricings); db.Transports.RemoveRange(d.Transports);
                d.Pricings.Clear(); d.Transports.Clear();
            }
            d.DepartureDate = model.DepartureDate!.Value; d.AvailableSlots = model.AvailableSlots!.Value;
            d.Status = model.Status; d.PolicyTemplateId = model.PolicyTemplateId; d.TourGuideInfo = model.TourGuideInfo.Trim();
            d.Pricings = model.Pricings.Where(p => p.OriginalPrice.HasValue).Select(p => new DeparturePricing
            {
                PassengerType = p.PassengerType, AgeDescription = string.IsNullOrWhiteSpace(p.AgeDescription) ? DeparturePriceInput.DefaultAge(p.PassengerType) : p.AgeDescription.Trim(),
                OriginalPrice = p.OriginalPrice!.Value, SalePrice = p.SalePrice
            }).ToList();
            d.Transports = model.Transports.Where(t => t.Enabled).Select(t => new DepartureTransport
            {
                Type = t.Type, VehicleType = t.VehicleType, VehicleName = t.VehicleName?.Trim(), TransportCode = t.TransportCode?.Trim(),
                StartPoint = t.StartPoint?.Trim(), EndPoint = t.EndPoint?.Trim(), DepartTime = t.DepartTime, ArrivalTime = t.ArrivalTime
            }).ToList();
            if (!id.HasValue) db.Departures.Add(d);
            // Force a revision update even when only pricing or transport has changed.
            else db.Entry(d).Property(x => x.UpdatedAt).IsModified = true;
            await db.SaveChangesAsync(); await transaction.CommitAsync(); return d.DepartureID;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" or "23505" or "23514" })
        {
            errors.AddModelError("", "Không thể lưu vì dữ liệu liên quan đã thay đổi hoặc lịch bị trùng. Vui lòng tải lại và kiểm tra thông tin.");
            db.ChangeTracker.Clear(); return null;
        }
    }

    private static void ValidatePricesAndTransport(DepartureFormViewModel model, ModelStateDictionary errors)
    {
        if (model.AvailableSlots is null or < 0) errors.AddModelError(nameof(model.AvailableSlots), "Số chỗ trống phải từ 0 trở lên.");
        if (string.IsNullOrWhiteSpace(model.TourGuideInfo)) errors.AddModelError(nameof(model.TourGuideInfo), "Vui lòng nhập thông tin hướng dẫn viên.");
        if (model.Pricings.Count != 4 || model.Pricings.Any(p => !Enum.IsDefined(p.PassengerType)) || model.Pricings.Select(p => p.PassengerType).Distinct().Count() != 4)
            errors.AddModelError("", "Bảng giá phải gồm bốn loại khách khác nhau.");
        if (!model.Pricings.Any(p => p.PassengerType == PassengerType.ADULT && p.OriginalPrice > 0 && (p.SalePrice ?? p.OriginalPrice) > 0))
            errors.AddModelError("", "Giá gốc và giá bán người lớn phải lớn hơn 0.");
        for (var i = 0; i < model.Pricings.Count; i++)
        {
            var p = model.Pricings[i];
            if (p.SalePrice.HasValue && !p.OriginalPrice.HasValue) errors.AddModelError($"Pricings[{i}].OriginalPrice", "Nhập giá gốc trước khi nhập giá bán.");
            if (p.SalePrice > p.OriginalPrice) errors.AddModelError($"Pricings[{i}].SalePrice", "Giá bán không được vượt giá gốc.");
            if (p.OriginalPrice is < 0 || p.SalePrice is < 0) errors.AddModelError("", "Giá không được âm.");
            if (p.OriginalPrice.HasValue && decimal.Truncate(p.OriginalPrice.Value) != p.OriginalPrice || p.SalePrice.HasValue && decimal.Truncate(p.SalePrice.Value) != p.SalePrice)
                errors.AddModelError("", "Giá VND phải là số nguyên.");
        }
        if (model.Transports.Any(t => !Enum.IsDefined(t.Type) || !Enum.IsDefined(t.VehicleType))) errors.AddModelError("", "Loại vận chuyển không hợp lệ.");
        for (var i = 0; i < model.Transports.Count; i++)
        {
            var t = model.Transports[i]; if (!t.Enabled) continue;
            if (!t.DepartTime.HasValue || !t.ArrivalTime.HasValue || t.ArrivalTime < t.DepartTime)
                errors.AddModelError($"Transports[{i}].ArrivalTime", "Nhập đủ giờ đi và đến; giờ đến phải từ giờ đi trở đi.");
        }
    }

    public async Task<DepartureCloneViewModel?> GetForCloneAsync(int id)
    {
        var detail = await GetDetailAsync(id);
        return detail == null ? null : new DepartureCloneViewModel
        {
            Id = id, TourName = detail.Departure.Tour?.TourName ?? "", OriginalDate = detail.Departure.DepartureDate,
            AvailableSlots = detail.Departure.AvailableSlots + detail.BookedSlots
        };
    }

    public async Task<int?> CloneAsync(int id, DepartureCloneViewModel model, ModelStateDictionary errors)
    {
        if (!model.NewDepartureDate.HasValue || model.NewDepartureDate <= PaymentTime.Now(clock)) errors.AddModelError(nameof(model.NewDepartureDate), "Ngày khởi hành mới phải ở trong tương lai.");
        if (model.AvailableSlots is null or < 1) errors.AddModelError(nameof(model.AvailableSlots), "Số chỗ phải lớn hơn 0.");
        if (!errors.IsValid) return null;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var d = await db.Departures.FromSqlInterpolated($"SELECT * FROM tour_departures WHERE departureid = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (d == null) { errors.AddModelError("", "Lịch gốc không còn tồn tại."); return null; }
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(73521, {d.TourId})");
        if (await db.Departures.AnyAsync(x => x.TourId == d.TourId && x.DepartureDate == model.NewDepartureDate))
        { errors.AddModelError(nameof(model.NewDepartureDate), "Tour này đã có lịch vào thời điểm đã chọn."); return null; }
        await db.Entry(d).Collection(x => x.Pricings).LoadAsync(); await db.Entry(d).Collection(x => x.Transports).LoadAsync();
        var shift = model.NewDepartureDate!.Value - d.DepartureDate;
        var clone = new TourDeparture
        {
            TourId = d.TourId, PolicyTemplateId = d.PolicyTemplateId, DepartureDate = model.NewDepartureDate.Value,
            AvailableSlots = model.AvailableSlots!.Value, Status = true, TourGuideInfo = d.TourGuideInfo,
            Pricings = d.Pricings.Select(p => new DeparturePricing { PassengerType = p.PassengerType, AgeDescription = p.AgeDescription, OriginalPrice = p.OriginalPrice, SalePrice = p.SalePrice }).ToList(),
            Transports = d.Transports.Select(t => new DepartureTransport { Type = t.Type, VehicleType = t.VehicleType, VehicleName = t.VehicleName, TransportCode = t.TransportCode,
                StartPoint = t.StartPoint, EndPoint = t.EndPoint, DepartTime = t.DepartTime + shift, ArrivalTime = t.ArrivalTime + shift }).ToList()
        };
        db.Departures.Add(clone); await db.SaveChangesAsync(); await transaction.CommitAsync(); return clone.DepartureID;
    }

    public async Task<(bool Success, string Message)> DeleteAsync(IEnumerable<int> ids)
    {
        var selected = ids.Distinct().Order().ToArray();
        if (selected.Length == 0 || selected.Length > 50 || selected.Any(id => id <= 0)) return (false, "Vui lòng chọn từ 1 đến 50 lịch để xóa.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var departures = new List<TourDeparture>();
            foreach (var id in selected)
            {
                var d = await db.Departures.FromSqlInterpolated($"SELECT * FROM tour_departures WHERE departureid = {id} FOR UPDATE").SingleOrDefaultAsync();
                if (d == null) return (false, "Một lịch đã được xóa trước đó. Vui lòng tải lại danh sách.");
                departures.Add(d);
            }
            if (await db.Bookings.AnyAsync(b => selected.Contains(b.DepartureId)))
                return (false, "Không thể xóa lịch đã có booking, kể cả đơn đã hủy. Bạn có thể sửa lịch và chuyển sang Tạm dừng. Chưa có lịch nào được xóa.");
            // The original PostgreSQL foreign keys do not all use ON DELETE CASCADE.
            await db.Pricings.Where(p => selected.Contains(p.DepartureId)).ExecuteDeleteAsync();
            await db.Transports.Where(t => selected.Contains(t.DepartureId)).ExecuteDeleteAsync();
            db.Departures.RemoveRange(departures); await db.SaveChangesAsync(); await transaction.CommitAsync();
            return (true, $"Đã xóa {selected.Length} lịch khởi hành.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" })
        { db.ChangeTracker.Clear(); return (false, "Lịch đang có dữ liệu liên quan nên không thể xóa. Chưa có lịch nào được xóa."); }
    }
}
