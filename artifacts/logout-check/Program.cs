using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TourismMvc.Data;
using TourismMvc.Models.Entities;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior",true);
var root=@"D:\TourismMvc";
var fixturePath=Path.Combine(root,"artifacts/logout-check/accounts.json");
var configuration=new ConfigurationBuilder().SetBasePath(Path.Combine(root,"TourismMvc")).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json",optional:true).Build();
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(configuration.GetConnectionString("Default")).Options);
if(args.FirstOrDefault()=="cleanup")
{
    if(File.Exists(fixturePath))
    {
        using var data=JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
        var emails=data.RootElement.GetProperty("accounts").EnumerateArray().Select(a=>a.GetProperty("email").GetString()!).ToArray();
        if(emails.Any(e=>!e.StartsWith("logout-check-",StringComparison.Ordinal)||!e.EndsWith("@example.invalid",StringComparison.Ordinal)))throw new Exception("Invalid fixture cleanup scope");
        await db.Users.Where(u=>emails.Contains(u.Email)).ExecuteDeleteAsync();File.Delete(fixturePath);
    }
    Console.WriteLine("Temporary logout test accounts removed.");return;
}
if(File.Exists(fixturePath))throw new Exception("Existing logout fixture must be cleaned first.");
var marker=Guid.NewGuid().ToString("N");var password="Aa1!"+Guid.NewGuid().ToString("N");
var users=new[]{new User{Email=$"logout-check-admin-{marker}@example.invalid",FullName="Logout Check Admin",Password=BCrypt.Net.BCrypt.HashPassword(password),Role=Role.ADMIN,Status=true,IsEmailVerified=true},new User{Email=$"logout-check-customer-{marker}@example.invalid",FullName="Logout Check Customer",Password=BCrypt.Net.BCrypt.HashPassword(password),Role=Role.CUSTOMER,Status=true,IsEmailVerified=true}};
await File.WriteAllTextAsync(fixturePath,JsonSerializer.Serialize(new{password,accounts=users.Select(u=>new{email=u.Email,role=u.Role.ToString()})}));
db.Users.AddRange(users);await db.SaveChangesAsync();Console.WriteLine("Temporary logout test accounts prepared.");
