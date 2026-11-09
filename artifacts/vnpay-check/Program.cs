using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TourismMvc.Data;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var root = @"D:\TourismMvc\TourismMvc";
var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json",true).Build();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(config.GetConnectionString("Default")).Options);
var mode = args.FirstOrDefault() ?? "schema";
if (mode == "schema")
{
    var checks = await db.Database.SqlQueryRaw<string>("SELECT conname || ': ' || pg_get_constraintdef(oid) AS \"Value\" FROM pg_constraint WHERE conrelid = 'public.bookings'::regclass AND contype='c'").ToListAsync();
    foreach (var check in checks) Console.WriteLine(check);
    Console.WriteLine("Legacy payments table exists: " + await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('public.payments') IS NOT NULL AS \"Value\"").SingleAsync());
    return;
}
if (mode == "migrate")
{
    await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(root,"Data/vnpay_payments.sql")));
    Console.WriteLine("Applied additive MVC VNPAY payment schema."); return;
}
await PaymentChecks.RunAsync(mode, config.GetConnectionString("Default")!);
