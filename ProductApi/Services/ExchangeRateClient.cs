namespace ProductApi.Services;

// Bài 37: giả lập một API bên ngoài (gọi HTTP mất ~300ms).
// Thực tế: typed HttpClient đăng ký bằng builder.Services.AddHttpClient<IExchangeRateClient, ExchangeRateClient>()
// — KHÔNG new HttpClient() mỗi lần gọi (cạn socket).
public interface IExchangeRateClient
{
    Task<decimal> GetUsdRateAsync(CancellationToken ct);
}

public class FakeExchangeRateClient : IExchangeRateClient
{
    public async Task<decimal> GetUsdRateAsync(CancellationToken ct)
    {
        await Task.Delay(300, ct);      // giả lập network I/O: thread được trả về pool trong lúc chờ
        return 25_400m;
    }
}
