using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductApi.Authorization;
using ProductApi.Dtos;
using ProductApi.Services;

namespace ProductApi.Controllers;

// ===== Bài 29: đăng nhập trả JWT =====
[ApiController]
[Route("api/auth")]
public class AuthController(ITokenService tokens) : ControllerBase
{
    // Tài khoản demo: admin/admin123 (Admin), staff1/staff123, staff2/staff123 (Staff)
    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var result = tokens.Login(request);
        return result is null ? Unauthorized() : Ok(result);
    }

    // Gọi thử với token để xem server đọc được claims nào
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        name = User.Identity?.Name,
        isAdmin = User.IsInRole(Roles.Admin),
        claims = User.Claims.Select(c => new { c.Type, c.Value }),
    });
}

// ===== Bài 26 + 31: Category (Unit of Work + cache) =====
[ApiController]
[Route("api/categories")]
public class CategoriesController(ICategoryService service) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Read)]
    public async Task<ActionResult<List<CategoryDto>>> GetAll(CancellationToken ct) =>
        Ok(await service.GetAllAsync(ct));      // gọi 2 lần liên tiếp: lần 2 không thấy log "cache miss"

    [HttpPost]
    [Authorize(Policy = Policies.CanWrite)]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return Created($"/api/categories/{created.Id}", created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.CanWrite)]
    public async Task<IActionResult> Update(int id, CategoryRequest request, CancellationToken ct)
    {
        await service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    // DELETE /api/categories/2?moveProductsTo=1
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.CanDelete)]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? moveProductsTo, CancellationToken ct)
    {
        await service.DeleteAsync(id, moveProductsTo, ct);
        return NoContent();
    }
}

// ===== Bài 23: tạo đơn hàng trong transaction =====
[ApiController]
[Route("api/orders")]
public class OrdersController(IOrderService service) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetByIdAsync(id, ct));

    // 201 khi tạo được; 404 nếu có sản phẩm không tồn tại; 409 nếu không đủ tồn kho
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }
}

// ===== Bài 35, 36, 37: báo cáo =====
[ApiController]
[Route("api/reports")]
public class ReportsController(IReportService reports, IExchangeRateClient exchangeRate) : ControllerBase
{
    // Bài 35: stored procedure
    [HttpGet("sales-by-category")]
    public async Task<ActionResult<List<CategorySalesDto>>> SalesByCategory([FromQuery] DateTime? from, CancellationToken ct) =>
        Ok(await reports.GetSalesByCategoryAsync(from ?? new DateTime(2026, 1, 1), ct));

    // Bài 35: raw SQL có tham số — thử name = "' OR 1=1 --" sẽ thấy KHÔNG bị injection
    [HttpGet("raw-search")]
    public async Task<ActionResult<List<ProductDto>>> RawSearch([FromQuery] string name, CancellationToken ct) =>
        Ok(await reports.SearchRawSqlAsync(name, ct));

    // Bài 36: Dapper
    [HttpGet("top-products")]
    public async Task<ActionResult<List<TopProductDto>>> TopProducts([FromQuery] int top = 5, CancellationToken ct = default) =>
        Ok(await reports.GetTopProductsAsync(Math.Clamp(top, 1, 100), ct));

    // Bài 37: bản ĐÃ SỬA của code gây thread pool starvation.
    //
    // Code lỗi trong đề:
    //     public IActionResult GetReport()
    //     {
    //         var data  = _service.GetDataAsync().Result;                              // chặn thread
    //         var extra = Task.Run(() => _http.GetStringAsync(url)).Result;           // chiếm THÊM 1 thread rồi lại chặn
    //         return Ok(new { data, extra });
    //     }
    //
    // Sửa: async từ đầu đến cuối ("async all the way"), không .Result / .Wait() / Task.Run bọc I/O.
    // Hai việc độc lập (DB + HTTP) → chạy song song bằng Task.WhenAll.
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken ct)
    {
        var statsTask = reports.GetDashboardStatsAsync(ct);   // DB
        var rateTask = exchangeRate.GetUsdRateAsync(ct);      // HTTP — khác nguồn nên chạy song song an toàn

        await Task.WhenAll(statsTask, rateTask);

        var (productCount, pending, revenue) = statsTask.Result;   // .Result SAU khi await WhenAll: task đã xong, không chặn
        var rate = rateTask.Result;
        return Ok(new DashboardDto(productCount, pending, revenue, rate, Math.Round(revenue / rate, 2)));
    }
}

// ===== Bài 34: bulk insert =====
// Thực tế phải [Authorize(Policy = Policies.CanDelete)]; để mở cho tiện thử.
[ApiController]
[Route("api/admin")]
public class AdminController(BulkInsertService bulk) : ControllerBase
{
    // POST /api/admin/bulk-insert?count=100000&method=sqlbulkcopy   (method: ef | sqlbulkcopy)
    [HttpPost("bulk-insert")]
    public async Task<IActionResult> BulkInsert([FromQuery] int count = 10_000, [FromQuery] string method = "sqlbulkcopy", CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 200_000);
        var result = method.Equals("ef", StringComparison.OrdinalIgnoreCase)
            ? await bulk.InsertWithEfBatchesAsync(count, ct)
            : await bulk.InsertWithSqlBulkCopyAsync(count, ct);
        return Ok(result);
    }

    [HttpDelete("bulk-products")]
    public async Task<IActionResult> Cleanup(CancellationToken ct) =>
        Ok(new { deleted = await bulk.CleanupAsync(ct) });
}
