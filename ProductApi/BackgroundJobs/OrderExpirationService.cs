using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Entities;

namespace ProductApi.BackgroundJobs;

// Bài 33: Mỗi 5 phút, đánh dấu các đơn Pending quá 24 giờ thành Expired.
//
// Cảnh báo trong đề: BackgroundService là Singleton (sống suốt đời app), còn DbContext là Scoped.
//   ✘ Inject thẳng AppDbContext vào constructor → lỗi lúc khởi động
//     ("Cannot consume scoped service ... from singleton"), hoặc nếu tắt kiểm tra thì
//     dùng chung một DbContext mãi mãi: không thread-safe, change tracker phình to, connection hỏng là chết luôn.
//   ✔ Inject IServiceScopeFactory (Singleton), mỗi lần chạy TỰ TẠO scope mới → lấy DbContext mới → dispose sau khi xong.
public class OrderExpirationService(IServiceScopeFactory scopeFactory, ILogger<OrderExpirationService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ExpireAfter = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // PeriodicTimer (.NET 6+): không chồng lần chạy, hỗ trợ CancellationToken — tốt hơn System.Threading.Timer
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow - ExpireAfter;

                // ExecuteUpdateAsync: 1 câu UPDATE ... WHERE, không load đơn hàng lên RAM
                var expired = await db.Orders
                    .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Expired), stoppingToken);

                if (expired > 0)
                    logger.LogInformation("Marked {Count} pending orders as Expired", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Không để exception làm chết job: log rồi chờ lần chạy sau
                logger.LogError(ex, "Order expiration job failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
