using Microsoft.EntityFrameworkCore;
using Npgsql;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Models.ViewModels;

namespace TourismMvc.Services;

public class AccountService(AppDbContext db, ILogger<AccountService> logger) : IAccountService
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), workFactor: 11);

    public async Task<(User? User, string? Error)> AuthenticateAsync(string email, string password, bool adminOnly)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        var hash = user?.Password ?? DummyHash;
        var valid = false;
        try
        {
            // Spring Security có thể lưu tiền tố {bcrypt} trước chuỗi BCrypt.
            if (hash.StartsWith("{bcrypt}", StringComparison.Ordinal)) hash = hash[8..];
            valid = BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or BCrypt.Net.SaltParseException)
        {
            logger.LogWarning("Invalid password hash format for account ID {UserId}", user?.UserID);
        }
        if (user == null || !valid) return (null, "Email hoặc mật khẩu không đúng.");
        if (!user.Status) return (null, "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
        if (!user.IsEmailVerified) return (null, "Tài khoản chưa được xác thực email. Vui lòng liên hệ hỗ trợ.");
        if (adminOnly && user.Role != Role.ADMIN) return (null, "Tài khoản này không có quyền quản trị.");
        return (user, null);
    }

    public async Task<string?> RegisterAsync(RegisterViewModel model)
    {
        var email = model.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email)) return "Email đã được đăng ký. Vui lòng dùng email khác hoặc đăng nhập.";
        var user = new User
        {
            FullName = model.FullName.Trim(),
            Email = email,
            Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim(),
            Password = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 11),
            Role = Role.CUSTOMER,
            Status = true,
            IsEmailVerified = true
        };
        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(user).State = EntityState.Detached;
            return "Email đã được đăng ký. Vui lòng dùng email khác hoặc đăng nhập.";
        }
        return null;
    }
}
