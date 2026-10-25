using Microsoft.Extensions.Caching.Memory;
using ProductApi.Dtos;
using ProductApi.Entities;
using ProductApi.Exceptions;
using ProductApi.Repositories;

namespace ProductApi.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(CancellationToken ct);
    Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken ct);
    Task UpdateAsync(int id, CategoryRequest request, CancellationToken ct);
    Task DeleteAsync(int id, int? moveProductsTo, CancellationToken ct);
}

// Bài 31: Cache danh sách category bằng IMemoryCache, hết hạn sau 10 phút,
//         và xóa cache (invalidation) mỗi khi thêm/sửa/xóa.
// Bài 26: dùng Unit of Work — nhiều thay đổi, một lần SaveChanges.
//
// Vì sao IMemoryCache phải là Singleton? (AddMemoryCache() đã đăng ký sẵn Singleton)
//   Cache phải SỐNG LÂU HƠN một request và DÙNG CHUNG giữa mọi request.
//   Nếu là Scoped, mỗi request có một cache mới tinh, rỗng → cache vô nghĩa.
//   Service này là Scoped nhưng inject Singleton vào được (ngắn sống inject dài sống: OK).
//   Chiều ngược lại mới lỗi — xem Bài 38.
public class CategoryService(IUnitOfWork uow, IMemoryCache cache, ILogger<CategoryService> logger) : ICategoryService
{
    private const string CacheKey = "categories:all";

    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken ct)
    {
        var result = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            logger.LogInformation("Category cache miss → query DB");
            return await uow.Categories.GetAllAsync(ct);
        });
        return result!;
    }

    public async Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await uow.Categories.NameExistsAsync(name, null, ct))
            throw new ConflictException($"Category '{name}' đã tồn tại");

        var category = new Category { Name = name };
        uow.Categories.Add(category);
        await uow.SaveChangesAsync(ct);
        cache.Remove(CacheKey);                 // invalidation
        return new CategoryDto(category.Id, category.Name, 0);
    }

    public async Task UpdateAsync(int id, CategoryRequest request, CancellationToken ct)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException($"Không tìm thấy category {id}");
        var name = request.Name.Trim();
        if (await uow.Categories.NameExistsAsync(name, id, ct))
            throw new ConflictException($"Category '{name}' đã tồn tại");

        category.Name = name;
        await uow.SaveChangesAsync(ct);
        cache.Remove(CacheKey);
    }

    // Bài 26: xóa category và (tùy chọn) chuyển toàn bộ sản phẩm sang category khác — 2 repository,
    // MỘT lần SaveChanges → hoặc cả hai cùng thành công, hoặc không gì thay đổi.
    public async Task DeleteAsync(int id, int? moveProductsTo, CancellationToken ct)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException($"Không tìm thấy category {id}");

        var products = await uow.Products.GetByCategoryAsync(id, ct);
        if (products.Count > 0)
        {
            if (moveProductsTo is null)
                throw new ConflictException($"Category còn {products.Count} sản phẩm. Truyền ?moveProductsTo=<id> để chuyển chúng đi.");
            if (moveProductsTo == id || !await uow.Categories.ExistsAsync(moveProductsTo.Value, ct))
                throw new BusinessRuleException($"Category đích {moveProductsTo} không hợp lệ");

            foreach (var p in products)
                p.CategoryId = moveProductsTo.Value;
        }

        uow.Categories.Remove(category);
        await uow.SaveChangesAsync(ct);         // UPDATE products + DELETE category trong cùng 1 transaction
        cache.Remove(CacheKey);
    }
}
