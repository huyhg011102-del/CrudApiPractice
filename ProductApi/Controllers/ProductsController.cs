using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProductApi.Authorization;
using ProductApi.Dtos;
using ProductApi.Services;

namespace ProductApi.Controllers;

// ======================================================================================
//  Bài 11 → 20 (+ 24, 29, 30, 32, 40): bản nhiều tầng Controller → Service → Repository.
//  Controller chỉ làm 3 việc: nhận request, gọi service, map status code (Bài 12).
//  Lỗi nghiệp vụ do service ném exception, GlobalExceptionMiddleware đổi sang 404/409/422 (Bài 20, 21).
// ======================================================================================
[ApiController]
[Route("api/products")]
public class ProductsController(IProductService service) : ControllerBase
{
    // Bài 13 + 14: GET /api/products?page=1&pageSize=20&name=a&minPrice=100000&sortBy=price&desc=true
    // Bài 32: rate limit 100 request/phút cho nhóm endpoint đọc → vượt thì 429.
    // Bài 30: ai cũng đọc được (không có [Authorize]).
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Read)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPaged([FromQuery] ProductQuery query, CancellationToken ct) =>
        Ok(await service.GetPagedAsync(query, ct));

    // Bài 15: sản phẩm kèm tên category. 404 do service ném NotFoundException.
    [HttpGet("{id:int}")]
    [EnableRateLimiting(RateLimitPolicies.Read)]
    public async Task<ActionResult<ProductDetailDto>> GetById(int id, CancellationToken ct) =>
        Ok(await service.GetByIdAsync(id, ct));

    // Bài 40: bản ĐÃ SỬA của đoạn code review (xem THEORY.md để đối chiếu bản lỗi).
    [HttpGet("search")]
    [EnableRateLimiting(RateLimitPolicies.Read)]
    public async Task<ActionResult<List<ProductDto>>> Search(
        [FromQuery, Required, MinLength(2), MaxLength(100)] string name, CancellationToken ct) =>
        Ok(await service.SearchAsync(name, ct));

    // Bài 20: 201 Created. Trùng tên → 409 (service), body sai → 400 (tự động).
    // Bài 29 + 30: phải đăng nhập, role Staff hoặc Admin.
    [HttpPost]
    [Authorize(Policy = Policies.CanWrite)]
    public async Task<ActionResult<ProductDetailDto>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, User.Identity!.Name!, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // Bài 20 + 24 + 30: 204 khi thành công; 404 / 403 (không phải người tạo) / 409 (trùng tên, bị sửa trước) / 422 (giảm giá > 50%).
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.CanWrite)]
    public async Task<IActionResult> Update(int id, UpdateProductRequest request, CancellationToken ct)
    {
        await service.UpdateAsync(id, request, User, ct);
        return NoContent();
    }

    // Bài 18 + 30: xóa mềm, chỉ Admin.
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.CanDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    // Bài 18: xem các sản phẩm đã xóa mềm (IgnoreQueryFilters)
    [HttpGet("deleted")]
    [Authorize(Policy = Policies.CanDelete)]
    public async Task<ActionResult<List<ProductDto>>> GetDeleted(CancellationToken ct) =>
        Ok(await service.GetDeletedAsync(ct));

    [HttpPost("{id:int}/restore")]
    [Authorize(Policy = Policies.CanDelete)]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        await service.RestoreAsync(id, ct);
        return NoContent();
    }
}

public static class RateLimitPolicies
{
    public const string Read = "read";
}
