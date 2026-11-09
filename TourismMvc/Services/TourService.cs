using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

/// <summary>
/// Tương đương TourManagementServiceImpl bên Spring Boot.
/// Toàn bộ truy vấn DB của module Tour nằm ở đây — Controller chỉ điều phối.
/// </summary>
public class TourService(AppDbContext db, ILogger<TourService> logger) : ITourService
{
    // ── Danh sách + lọc + sắp xếp + phân trang ─────────────────────────────
    public async Task<TourIndexViewModel> GetPagedToursAsync(
        string? search, int? startLocId, int? endLocId, bool? status,
        string sortBy, string sortDir, int page, int pageSize)
    {
        if (pageSize <= 0) pageSize = 10;
        sortBy = string.IsNullOrWhiteSpace(sortBy) ? "updatedAt" : sortBy;
        var asc = string.Equals(sortDir, "ASC", StringComparison.OrdinalIgnoreCase);

        var baseQ = db.Tours.AsNoTracking().Where(t => t.IsDeleted != true);
        var q = baseQ;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            q = q.Where(t => EF.Functions.ILike(t.TourCode, pattern)
                          || EF.Functions.ILike(t.TourName, pattern)
                          || EF.Functions.ILike(t.Attractions, pattern));
        }
        if (startLocId.HasValue) q = q.Where(t => t.StartLocationId == startLocId);
        if (endLocId.HasValue) q = q.Where(t => t.EndLocationId == endLocId);
        if (status.HasValue) q = q.Where(t => t.Status == status);

        q = (sortBy, asc) switch
        {
            ("tourCode", true) => q.OrderBy(t => t.TourCode),
            ("tourCode", false) => q.OrderByDescending(t => t.TourCode),
            ("tourName", true) => q.OrderBy(t => t.TourName),
            ("tourName", false) => q.OrderByDescending(t => t.TourName),
            ("departures", true) => q.OrderBy(t => t.Departures.Count),
            ("departures", false) => q.OrderByDescending(t => t.Departures.Count),
            (_, true) => q.OrderBy(t => t.UpdatedAt).ThenBy(t => t.TourID),
            _ => q.OrderByDescending(t => t.UpdatedAt == null)
                  .ThenByDescending(t => t.UpdatedAt)
                  .ThenByDescending(t => t.TourID),
        };

        var total = await q.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        // Projection: chỉ lấy đúng dữ liệu cần → 1 câu SQL, không N+1
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Include(t => t.StartLocation)
            .Include(t => t.EndLocation)
            .Select(t => new
            {
                Tour = t,
                MainImage = t.Images.OrderByDescending(i => i.IsMainImage)
                                    .ThenBy(i => i.SortOrder)
                                    .Select(i => i.ImageURL).FirstOrDefault(),
                ImageCount = t.Images.Count,
                ItineraryCount = t.ItineraryDays.Count,
                DepartureCount = t.Departures.Count
            })
            .ToListAsync();

        return new TourIndexViewModel
        {
            Items = rows.Select(r => new TourListItem
            {
                Tour = r.Tour,
                MainImageUrl = r.MainImage,
                ImageCount = r.ImageCount,
                ItineraryCount = r.ItineraryCount,
                DepartureCount = r.DepartureCount
            }).ToList(),
            Search = search,
            StartLocationId = startLocId,
            EndLocationId = endLocId,
            Status = status,
            SortBy = sortBy,
            SortDir = asc ? "ASC" : "DESC",
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            StatTotal = await baseQ.CountAsync(),
            StatActive = await baseQ.CountAsync(t => t.Status),
            StatInactive = await baseQ.CountAsync(t => !t.Status),
            StatWithDepartures = await baseQ.CountAsync(t => t.Departures.Any()),
            LocationOptions = await GetLocationOptionsAsync()
        };
    }

    // ── Chi tiết ────────────────────────────────────────────────────────────
    public async Task<TourDetailViewModel?> GetTourDetailAsync(int tourId)
    {
        var t = await db.Tours.AsNoTracking()
            .Include(x => x.StartLocation)
            .Include(x => x.EndLocation)
            .Include(x => x.Images)
            .Include(x => x.ItineraryDays)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.TourID == tourId && x.IsDeleted != true);
        if (t == null) return null;

        var images = t.Images.OrderByDescending(i => i.IsMainImage).ThenBy(i => i.SortOrder)
            .Select(i => new TourImageItemViewModel
            {
                ImageID = i.ImageID, ImageURL = i.ImageURL, IsMainImage = i.IsMainImage, SortOrder = i.SortOrder
            }).ToList();

        return new TourDetailViewModel
        {
            TourID = t.TourID,
            TourCode = t.TourCode,
            TourName = t.TourName,
            Duration = t.Duration,
            Transportation = t.Transportation,
            Attractions = t.Attractions,
            Meals = t.Meals,
            IdealTime = t.IdealTime,
            TripTransportation = t.TripTransportation,
            SuitableCustomer = t.SuitableCustomer,
            Hotel = t.Hotel,
            Status = t.Status,
            StartLocationId = t.StartLocationId,
            StartLocationName = t.StartLocation?.LocationName ?? "—",
            EndLocationId = t.EndLocationId,
            EndLocationName = t.EndLocation?.LocationName ?? "—",
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            Images = images,
            MainImageUrl = images.FirstOrDefault()?.ImageURL,
            ItineraryDays = t.ItineraryDays.OrderBy(d => d.DayNumber)
                .Select(d => new TourItineraryItemViewModel
                {
                    DayID = d.DayID, DayNumber = d.DayNumber, Title = d.Title,
                    Description = d.Description, Meals = d.Meals, Accommodation = d.Accommodation
                }).ToList(),
            DepartureCount = await db.Departures.CountAsync(d => d.TourId == tourId)
        };
    }

    // ── Form ────────────────────────────────────────────────────────────────
    public async Task<TourFormViewModel> PrepareCreateViewModelAsync() => new()
    {
        Status = true,
        LocationOptions = await GetLocationOptionsAsync()
    };

    public async Task<TourFormViewModel?> GetForEditAsync(int tourId)
    {
        var t = await db.Tours.AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.ItineraryDays)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.TourID == tourId && x.IsDeleted != true);
        if (t == null) return null;

        return new TourFormViewModel
        {
            TourID = t.TourID,
            TourCode = t.TourCode,
            TourName = t.TourName,
            Duration = t.Duration,
            Transportation = t.Transportation,
            StartLocationId = t.StartLocationId,
            EndLocationId = t.EndLocationId,
            Attractions = t.Attractions,
            Meals = t.Meals,
            IdealTime = t.IdealTime,
            TripTransportation = t.TripTransportation,
            SuitableCustomer = t.SuitableCustomer,
            Hotel = t.Hotel,
            Status = t.Status,
            ItineraryDays = t.ItineraryDays.OrderBy(d => d.DayNumber).Select(d => new TourItineraryItemInput
            {
                DayID = d.DayID, DayNumber = d.DayNumber, Title = d.Title,
                Description = d.Description, Meals = d.Meals, Accommodation = d.Accommodation
            }).ToList(),
            Images = t.Images.OrderBy(i => i.SortOrder).Select(i => new TourImageItemInput
            {
                ImageID = i.ImageID, ImageURL = i.ImageURL, IsMainImage = i.IsMainImage, SortOrder = i.SortOrder ?? 0
            }).ToList(),
            LocationOptions = await GetLocationOptionsAsync()
        };
    }

    public Task<bool> CheckTourCodeExistsAsync(string tourCode, int? excludeTourId = null)
    {
        var code = (tourCode ?? "").Trim().ToUpperInvariant();
        return db.Tours.AnyAsync(t => t.TourCode == code && (!excludeTourId.HasValue || t.TourID != excludeTourId));
    }

    // ── Tạo mới ─────────────────────────────────────────────────────────────
    public async Task<bool> CreateTourAsync(TourFormViewModel vm, ModelStateDictionary modelState)
    {
        Normalize(vm);
        await ValidateAsync(vm, null, modelState);
        if (!modelState.IsValid)
        {
            vm.LocationOptions = await GetLocationOptionsAsync();
            return false;
        }

        var tour = new Tour();
        MapGeneral(vm, tour);
        tour.ItineraryDays = BuildItinerary(vm);
        tour.Images = BuildImages(vm);

        db.Tours.Add(tour);
        await db.SaveChangesAsync();
        logger.LogInformation("Created tour {Code} (ID {Id})", tour.TourCode, tour.TourID);
        return true;
    }

    // ── Cập nhật (thay toàn bộ lịch trình + ảnh, giống updateTour bên Spring) ──
    public async Task<bool> UpdateTourAsync(int tourId, TourFormViewModel vm, ModelStateDictionary modelState)
    {
        vm.TourID = tourId;
        Normalize(vm);

        var tour = await db.Tours
            .Include(t => t.Images)
            .Include(t => t.ItineraryDays)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.TourID == tourId && t.IsDeleted != true);
        if (tour == null)
        {
            modelState.AddModelError("", $"Không tìm thấy tour với ID: {tourId}");
            vm.LocationOptions = await GetLocationOptionsAsync();
            return false;
        }

        await ValidateAsync(vm, tourId, modelState);
        if (!modelState.IsValid)
        {
            vm.LocationOptions = await GetLocationOptionsAsync();
            return false;
        }

        MapGeneral(vm, tour);

        db.ItineraryDays.RemoveRange(tour.ItineraryDays);
        db.TourImages.RemoveRange(tour.Images);
        tour.ItineraryDays = BuildItinerary(vm);
        tour.Images = BuildImages(vm);

        await db.SaveChangesAsync();
        logger.LogInformation("Updated tour {Code} (ID {Id})", tour.TourCode, tour.TourID);
        return true;
    }

    // ── Xóa ─────────────────────────────────────────────────────────────────
    public async Task<(bool Success, string? ErrorMessage)> DeleteTourAsync(int tourId)
    {
        var tour = await db.Tours.FirstOrDefaultAsync(t => t.TourID == tourId && t.IsDeleted != true);
        if (tour == null) return (false, $"Không tìm thấy tour với ID: {tourId}");

        var hasBookings = await db.Bookings.AnyAsync(b => b.TourDeparture != null && b.TourDeparture.TourId == tourId);
        if (hasBookings)
            return (false, $"Không thể xóa tour \"{tour.TourName}\" vì đã có booking. Hãy tạm dừng tour thay vì xóa.");

        var relatedData = new List<string>();
        if (await db.ItineraryDays.AnyAsync(d => d.TourId == tourId)) relatedData.Add("lịch trình");
        if (await db.TourImages.AnyAsync(i => i.TourId == tourId)) relatedData.Add("hình ảnh");
        if (await db.Departures.AnyAsync(d => d.TourId == tourId)) relatedData.Add("lịch khởi hành");
        if (relatedData.Count > 0)
            return (false, $"Không thể xóa tour \"{tour.TourName}\" vì đang có {string.Join(", ", relatedData)} liên quan. Bạn có thể tạm dừng tour để ngừng hiển thị.");

        db.Tours.Remove(tour);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // Dữ liệu liên quan có thể được thêm sau bước kiểm tra ở trên.
            db.Entry(tour).State = EntityState.Unchanged;
            logger.LogWarning(ex, "Cannot delete tour {Id} because related data exists", tourId);
            return (false, $"Không thể xóa tour \"{tour.TourName}\" vì đang được sử dụng bởi dữ liệu liên quan. Bạn có thể tạm dừng tour để ngừng hiển thị.");
        }
        logger.LogInformation("Deleted tour {Code} (ID {Id})", tour.TourCode, tourId);
        return (true, null);
    }

    public async Task<(bool Success, bool NewStatus)> ToggleStatusAsync(int tourId)
    {
        var tour = await db.Tours.FirstOrDefaultAsync(t => t.TourID == tourId && t.IsDeleted != true);
        if (tour == null) return (false, false);
        tour.Status = !tour.Status;
        await db.SaveChangesAsync();
        return (true, tour.Status);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private Task<List<LocationOptionViewModel>> GetLocationOptionsAsync() =>
        db.Locations.AsNoTracking()
            .Where(l => l.IsDeleted != true)
            .OrderBy(l => l.LocationName)
            .Select(l => new LocationOptionViewModel { LocationId = l.LocationID, LocationName = l.LocationName })
            .ToListAsync();

    private static void Normalize(TourFormViewModel vm)
    {
        vm.TourCode = (vm.TourCode ?? "").Trim().ToUpperInvariant().Replace(" ", "");
        vm.TourName = (vm.TourName ?? "").Trim();
        vm.ItineraryDays = (vm.ItineraryDays ?? new())
            .Where(d => !string.IsNullOrWhiteSpace(d.Title) || !string.IsNullOrWhiteSpace(d.Description))
            .ToList();
        vm.Images = (vm.Images ?? new()).Where(i => !string.IsNullOrWhiteSpace(i.ImageURL)).ToList();
    }

    private async Task ValidateAsync(TourFormViewModel vm, int? excludeId, ModelStateDictionary ms)
    {
        if (!string.IsNullOrEmpty(vm.TourCode) && await CheckTourCodeExistsAsync(vm.TourCode, excludeId))
            ms.AddModelError(nameof(vm.TourCode), $"Mã tour \"{vm.TourCode}\" đã tồn tại");

        if (vm.StartLocationId.HasValue && !await db.Locations.AnyAsync(l => l.LocationID == vm.StartLocationId))
            ms.AddModelError(nameof(vm.StartLocationId), "Điểm khởi hành không tồn tại");
        if (vm.EndLocationId.HasValue && !await db.Locations.AnyAsync(l => l.LocationID == vm.EndLocationId))
            ms.AddModelError(nameof(vm.EndLocationId), "Điểm đến không tồn tại");

        var dupDays = vm.ItineraryDays.GroupBy(d => d.DayNumber).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupDays.Count > 0)
            ms.AddModelError(nameof(vm.ItineraryDays), $"Trùng số ngày trong lịch trình: {string.Join(", ", dupDays)}");
    }

    private static void MapGeneral(TourFormViewModel vm, Tour t)
    {
        t.TourCode = vm.TourCode;
        t.TourName = vm.TourName;
        t.Duration = vm.Duration.Trim();
        t.Transportation = vm.Transportation.Trim();
        t.StartLocationId = vm.StartLocationId!.Value;
        t.EndLocationId = vm.EndLocationId!.Value;
        t.Attractions = vm.Attractions.Trim();
        t.Meals = vm.Meals?.Trim();
        t.IdealTime = vm.IdealTime?.Trim();
        t.TripTransportation = vm.TripTransportation?.Trim();
        t.SuitableCustomer = vm.SuitableCustomer?.Trim();
        t.Hotel = vm.Hotel?.Trim();
        t.Status = vm.Status;
    }

    private static List<ItineraryDay> BuildItinerary(TourFormViewModel vm) =>
        vm.ItineraryDays.OrderBy(d => d.DayNumber).Select(d => new ItineraryDay
        {
            DayNumber = d.DayNumber,
            Title = d.Title.Trim(),
            Description = d.Description,
            Meals = d.Meals,
            Accommodation = d.Accommodation
        }).ToList();

    /// <summary>Chỉ cho phép đúng 1 ảnh chính — nếu không chọn thì ảnh đầu tiên là ảnh chính.</summary>
    private static List<TourImage> BuildImages(TourFormViewModel vm)
    {
        var list = vm.Images.Select((img, idx) => new TourImage
        {
            ImageURL = img.ImageURL.Trim(),
            IsMainImage = img.IsMainImage,
            SortOrder = idx
        }).ToList();

        var mainSet = false;
        foreach (var img in list)
        {
            if (img.IsMainImage && !mainSet) mainSet = true;
            else img.IsMainImage = false;
        }
        if (!mainSet && list.Count > 0) list[0].IsMainImage = true;
        return list;
    }
}
