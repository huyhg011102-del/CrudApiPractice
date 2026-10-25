using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Dtos;
using ProductApi.Entities;

namespace ProductApi.Repositories;

public interface ICategoryRepository
{
    Task<List<CategoryDto>> GetAllAsync(CancellationToken ct);
    Task<Category?> GetByIdAsync(int id, CancellationToken ct);
    Task<bool> ExistsAsync(int id, CancellationToken ct);
    Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct);
    void Add(Category category);
    void Remove(Category category);
}

// Repository này KHÔNG có SaveChanges — việc lưu do UnitOfWork (Bài 26) quyết định.
public class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public Task<List<CategoryDto>> GetAllAsync(CancellationToken ct) =>
        db.Categories.AsNoTracking()
                     .OrderBy(c => c.Name)
                     .Select(c => new CategoryDto(c.Id, c.Name, c.Products.Count()))   // query filter vẫn áp dụng → không đếm sản phẩm đã xóa
                     .ToListAsync(ct);

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Categories.AnyAsync(c => c.Id == id, ct);

    public Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct) =>
        db.Categories.AnyAsync(c => c.Name == name && c.Id != excludeId, ct);

    public void Add(Category category) => db.Categories.Add(category);
    public void Remove(Category category) => db.Categories.Remove(category);
}
