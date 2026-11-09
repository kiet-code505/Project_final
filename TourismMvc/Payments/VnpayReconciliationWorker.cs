using Microsoft.EntityFrameworkCore;
using TourismMvc.Data;
using TourismMvc.Models.Entities;
using TourismMvc.Services;

namespace TourismMvc.Payments;

public class VnpayReconciliationWorker(IServiceScopeFactory scopeFactory, TimeProvider clock, ILogger<VnpayReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        // Give the host time to start; each iteration also catches up after an application restart.
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                List<string> codes;
                await using (var scope = scopeFactory.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var now = PaymentTime.Now(clock);
                    var recent = now.AddDays(-1);
                    codes = await db.Bookings.AsNoTracking().Where(b => b.BookingStatus == BookingStatus.PENDING_PAYMENT && b.PaymentDeadline != null
                        && (b.PaymentDeadline <= now || b.Payments.Any(p => p.Status == VnpayPaymentStatus.PENDING))
                        || (b.BookingStatus == BookingStatus.EXPIRED || b.BookingStatus == BookingStatus.CANCELLED)
                            && b.Payments.Any(p => p.Status == VnpayPaymentStatus.EXPIRED && p.CreatedAt >= recent))
                        .OrderBy(b => b.PaymentDeadline).Select(b => b.BookingCode).Take(50).ToListAsync(stoppingToken);
                }
                foreach (var code in codes)
                {
                    if (stoppingToken.IsCancellationRequested) return;
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IVnpayPaymentService>().RefreshAsync(code, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError("VNPAY background reconciliation failed: {ErrorType}", ex.GetType().Name); }
        }
    }
}
