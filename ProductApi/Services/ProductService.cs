using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ProductApi.Authorization;
using ProductApi.Dtos;
using ProductApi.Exceptions;
using ProductApi.Repositories;

namespace ProductApi.Services;

// Bài 12: Service layer — nằm giữa controller và repository, chứa MỌI logic nghiệp vụ.
// Controller chỉ làm 3 việc: nhận request → gọi service → trả status code.
// (Chữ S trong SOLID: mỗi lớp một trách nhiệm.)
public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPagedAsync(ProductQuery query, CancellationToken ct);
    Task<ProductDetailDto> GetByIdAsync(int id, CancellationToken ct);
    Task<ProductDetailDto> CreateAsync(CreateProductRequest request, string createdBy, CancellationToken ct);
    Task UpdateAsync(int id, UpdateProductRequest request, ClaimsPrincipal user, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task<List<ProductDto>> GetDeletedAsync(CancellationToken ct);
    Task RestoreAsync(int id, CancellationToken ct);
    Task<List<ProductDto>> SearchAsync(string name, CancellationToken ct);
}

// Bài 22: inject ILogger<T>, log đúng level, dùng structured logging:
//   ✔ logger.LogInformation("Created product {ProductId}", id)    → log lưu được field ProductId để lọc/tìm
//   ✘ logger.LogInformation($"Created product {id}")              → chỉ là chuỗi, mất cấu trúc, tốn cấp phát
public class ProductService(
    IProductRepository repo,
    ICategoryRepository categories,
    IAuthorizationService authorization,
    ILogger<ProductService> logger) : IProductService
{
    public Task<PagedResult<ProductDto>> GetPagedAsync(ProductQuery query, CancellationToken ct) =>
        repo.GetPagedAsync(query, ct);

    public async Task<ProductDetailDto> GetByIdAsync(int id, CancellationToken ct)
    {
        var product = await repo.GetWithCategoryAsync(id, ct)
                      ?? throw new NotFoundException($"Không tìm thấy sản phẩm {id}");
        return product.ToDetailDto();
    }

    public async Task<ProductDetailDto> CreateAsync(CreateProductRequest request, string createdBy, CancellationToken ct)
    {
        // Bài 12: quy tắc nghiệp vụ "không trùng tên" nằm ở service, không nằm ở controller.
        // Bài 20: trùng tên = xung đột trạng thái → 409.
        if (await repo.NameExistsAsync(request.Name.Trim(), null, ct))
        {
            logger.LogWarning("Duplicate product name {ProductName} by {User}", request.Name, createdBy);   // Warning: lỗi phía client, đáng chú ý
            throw new ConflictException($"Sản phẩm '{request.Name}' đã tồn tại");
        }

        await EnsureCategoryExistsAsync(request.CategoryId, ct);

        var product = request.ToEntity(createdBy);
        repo.Add(product);
        await repo.SaveChangesAsync(ct);

        logger.LogInformation("Product {ProductId} '{ProductName}' created by {User}", product.Id, product.Name, createdBy);

        return (await repo.GetWithCategoryAsync(product.Id, ct))!.ToDetailDto();
    }

    public async Task UpdateAsync(int id, UpdateProductRequest request, ClaimsPrincipal user, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(id, ct)
                      ?? throw new NotFoundException($"Không tìm thấy sản phẩm {id}");

        // Bài 30: policy tùy chỉnh — chỉ được sửa sản phẩm do chính mình tạo (Admin thì được sửa tất cả).
        // Đây là "resource-based authorization": cần có product rồi mới quyết định được → không dùng [Authorize] được.
        var auth = await authorization.AuthorizeAsync(user, product, Policies.SameCreator);
        if (!auth.Succeeded)
            throw new ForbiddenException("Bạn chỉ được sửa sản phẩm do chính mình tạo");

        if (await repo.NameExistsAsync(request.Name.Trim(), id, ct))
            throw new ConflictException($"Sản phẩm '{request.Name}' đã tồn tại");

        // Bài 20: body hợp lệ về cú pháp nhưng sai nghiệp vụ → 422
        if (request.Price < product.Price * 0.5m)
            throw new BusinessRuleException("Không được giảm giá quá 50% trong một lần cập nhật");

        if (request.CategoryId != product.CategoryId)
            await EnsureCategoryExistsAsync(request.CategoryId, ct);

        // Bài 24: concurrency — báo cho EF "phiên bản client đang cầm là bản này".
        // Nếu trong DB đã khác (người khác vừa sửa), SaveChanges ném DbUpdateConcurrencyException
        // → GlobalExceptionMiddleware trả 409.
        var rowVersion = new byte[8];
        if (!Convert.TryFromBase64String(request.RowVersion, rowVersion, out var written) || written != 8)
            throw new BusinessRuleException("RowVersion không hợp lệ");
        repo.SetOriginalRowVersion(product, rowVersion);

        var oldPrice = product.Price;
        request.Apply(product);        // Bài 6: gán lại từng field lên entity đang được track
        await repo.SaveChangesAsync(ct);

        logger.LogInformation("Product {ProductId} updated by {User}, price {OldPrice} → {NewPrice}",
            id, user.Identity?.Name, oldPrice, product.Price);
    }

    // Bài 18: xóa mềm
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var product = await repo.GetByIdAsync(id, ct)
                      ?? throw new NotFoundException($"Không tìm thấy sản phẩm {id}");
        product.IsDeleted = true;
        await repo.SaveChangesAsync(ct);
        logger.LogInformation("Product {ProductId} soft-deleted", id);
    }

    public Task<List<ProductDto>> GetDeletedAsync(CancellationToken ct) => repo.GetDeletedAsync(ct);

    public async Task RestoreAsync(int id, CancellationToken ct)
    {
        var product = await repo.GetDeletedByIdAsync(id, ct)
                      ?? throw new NotFoundException($"Không có sản phẩm đã xóa với Id {id}");
        if (await repo.NameExistsAsync(product.Name, id, ct))
            throw new ConflictException($"Đã có sản phẩm khác tên '{product.Name}', không khôi phục được");
        product.IsDeleted = false;
        await repo.SaveChangesAsync(ct);
    }

    // Bài 40: bản đã sửa — tham số hóa, async, trả DTO, giới hạn số dòng, tăng ViewCount bằng 1 câu UPDATE.
    public async Task<List<ProductDto>> SearchAsync(string name, CancellationToken ct)
    {
        var result = await repo.SearchAsync(name.Trim(), take: 50, ct);
        if (result.Count > 0)
            await repo.IncreaseViewCountAsync(result.Select(p => p.Id).ToList(), ct);
        return result;
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken ct)
    {
        if (!await categories.ExistsAsync(categoryId, ct))
            throw new BusinessRuleException($"Category {categoryId} không tồn tại");
    }
}
