using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public class AdminUserService(AppDbContext db) : IAdminUserService
{
    // Project only the fields displayed in admin; password hashes never enter view models.
    private static readonly Expression<Func<User, AdminUserSummary>> Summary = u => new AdminUserSummary
    {
        Id = u.UserID, FullName = u.FullName, Email = u.Email, Phone = u.Phone,
        Role = u.Role, Status = u.Status, IsEmailVerified = u.IsEmailVerified, BookingCount = u.Bookings.Count
    };

    public async Task<AdminUserIndexViewModel> SearchAsync(AdminUserFilter filter)
    {
        filter.FullName = filter.FullName?.Trim(); filter.Email = filter.Email?.Trim(); filter.Phone = filter.Phone?.Trim();
        filter.Status = filter.Status is "active" or "locked" ? filter.Status : null;
        if (filter.Role.HasValue && !Enum.IsDefined(filter.Role.Value)) filter.Role = null;
        filter.PageSize = filter.PageSize is 6 or 12 or 24 ? filter.PageSize : 6;
        var stats = await db.Users.AsNoTracking().GroupBy(u => 1).Select(g => new
        {
            Total = g.Count(), Active = g.Count(u => u.Status), Locked = g.Count(u => !u.Status), Admins = g.Count(u => u.Role == Role.ADMIN)
        }).SingleOrDefaultAsync();
        var query = db.Users.AsNoTracking();
        // Contains treats user-entered % and _ as literal characters.
        if (!string.IsNullOrEmpty(filter.FullName)) query = query.Where(u => u.FullName.ToLower().Contains(filter.FullName.ToLower()));
        if (!string.IsNullOrEmpty(filter.Email)) query = query.Where(u => u.Email.ToLower().Contains(filter.Email.ToLower()));
        if (!string.IsNullOrEmpty(filter.Phone)) query = query.Where(u => u.Phone != null && u.Phone.Contains(filter.Phone));
        if (filter.Status != null) query = query.Where(u => u.Status == (filter.Status == "active"));
        if (filter.Role.HasValue) query = query.Where(u => u.Role == filter.Role.Value);
        var count = await query.CountAsync();
        filter.Page = Math.Clamp(filter.Page, 1, Math.Max(1, (int)Math.Ceiling((double)count / filter.PageSize)));
        return new AdminUserIndexViewModel
        {
            Filter = filter, TotalItems = count, TotalUsers = stats?.Total ?? 0, ActiveUsers = stats?.Active ?? 0,
            LockedUsers = stats?.Locked ?? 0, AdminUsers = stats?.Admins ?? 0,
            Items = await query.OrderByDescending(u => u.UserID).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(Summary).ToListAsync()
        };
    }

    public Task<AdminUserSummary?> GetAsync(int id) => db.Users.AsNoTracking().Where(u => u.UserID == id).Select(Summary).SingleOrDefaultAsync();

    public async Task<AdminUserDetailsViewModel?> GetDetailsAsync(int id, int page)
    {
        var user = await GetAsync(id);
        if (user == null) return null;
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(user.BookingCount / 10d)));
        return new AdminUserDetailsViewModel
        {
            Account = user, Page = page,
            Bookings = await db.Bookings.AsNoTracking().Where(b => b.UserId == id)
                .OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.BookingID).Skip((page - 1) * 10).Take(10)
                .Select(b => new AdminUserBookingRow
                {
                    Id = b.BookingID, BookingCode = b.BookingCode, BookingDate = b.BookingDate, TotalPrice = b.TotalPrice,
                    TotalPassengers = b.TotalPassengers, Status = b.BookingStatus,
                    TourName = b.TourDeparture!.Tour!.TourName, TourCode = b.TourDeparture.Tour.TourCode,
                    DepartureDate = b.TourDeparture.DepartureDate
                }).ToListAsync()
        };
    }

    public async Task<(bool Success, string Message)> SetStatusAsync(int id, bool status, int actorId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Serialize administrator changes and recheck the acting admin after acquiring locks.
        // Two admins cannot concurrently lock each other and leave no active administrator.
        var admins = await db.Users.FromSqlRaw("SELECT * FROM users WHERE role = 'ADMIN' ORDER BY userid FOR UPDATE").ToListAsync();
        if (!admins.Any(u => u.UserID == actorId && u.Status && u.IsEmailVerified))
            return (false, "Phiên quản trị không còn hợp lệ. Vui lòng đăng nhập lại.");
        if (id == actorId && !status) return (false, "Bạn không thể tự khóa tài khoản đang đăng nhập.");
        var account = admins.FirstOrDefault(u => u.UserID == id)
            ?? await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE userid = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (account == null) return (false, "Tài khoản không còn tồn tại. Vui lòng tải lại danh sách.");
        if (account.Status == status) return (true, status ? "Tài khoản hiện đang hoạt động." : "Tài khoản hiện đã bị khóa.");
        account.Status = status;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, status ? "Đã mở khóa tài khoản. Người dùng có thể đăng nhập lại." : "Đã khóa tài khoản. Phiên đăng nhập của người dùng sẽ bị thu hồi.");
    }
}
