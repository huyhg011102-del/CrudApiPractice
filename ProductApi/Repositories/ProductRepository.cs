using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Dtos;
using ProductApi.Entities;

namespace ProductApi.Repositories;

// Bài 11: Repository pattern — gom mọi truy cập DB của Product vào một chỗ.
// Đăng ký AddScoped vì bên trong dùng AppDbContext (Scoped). Xem Program.cs.
public interface IProductRepository
{
    Task<PagedResult<ProductDto>> GetPagedAsync(ProductQuery query, CancellationToken ct);
    Task<Product?> GetWithCategoryAsync(int id, CancellationToken ct);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct);
    Task<List<Product>> GetByCategoryAsync(int categoryId, CancellationToken ct);
    Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct);
    Task<List<ProductDto>> SearchAsync(string name, int take, CancellationToken ct);
    Task IncreaseViewCountAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
    Task<List<ProductDto>> GetDeletedAsync(CancellationToken ct);
    Task<Product?> GetDeletedByIdAsync(int id, CancellationToken ct);
    void Add(Product product);
    void SetOriginalRowVersion(Product product, byte[] rowVersion);
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public class ProductRepository(AppDbContext db) : IProductRepository
{
    // Bài 13 + 14 + 16: phân trang + lọc + sắp xếp động, chỉ đọc nên AsNoTracking.
    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductQuery query, CancellationToken ct)
    {
        // Bài 13: chặn pageSize quá lớn (client xin 1 triệu dòng) và page < 1
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Bài 14: build query từng bước. IQueryable là deferred → mọi điều kiện gộp thành MỘT câu SQL,
        // chưa có gì chạy xuống DB cho tới CountAsync / ToListAsync.
        IQueryable<Product> q = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Name))
            q = q.Where(p => p.Name.Contains(query.Name));          // → LIKE N'%' + @name + N'%' (có tham số, không SQL injection)
        if (query.MinPrice.HasValue)
            q = q.Where(p => p.Price >= query.MinPrice.Value);
        if (query.MaxPrice.HasValue)
            q = q.Where(p => p.Price <= query.MaxPrice.Value);
        if (query.CategoryId.HasValue)
            q = q.Where(p => p.CategoryId == query.CategoryId.Value);

        // Bài 14: sắp xếp động. Luôn ThenBy(Id) để thứ tự ổn định giữa các trang.
        q = (query.SortBy?.ToLowerInvariant(), query.Desc) switch
        {
            ("name", false) => q.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ("name", true) => q.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
            ("price", false) => q.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ("price", true) => q.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            ("createdat", false) => q.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            ("createdat", true) => q.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            (_, true) => q.OrderByDescending(p => p.Id),
            _ => q.OrderBy(p => p.Id),
        };

        // Bài 13: gọi DB 2 lần là chấp nhận được: 1 lần COUNT, 1 lần lấy trang.
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .Select(ProductMappings.AsDto)          // Bài 19: projection dịch sang SQL
                           .ToListAsync(ct);

        return new PagedResult<ProductDto>(items, page, pageSize, total);
    }

    // Bài 15: Include để load kèm Category (không Include thì p.Category = null).
    // Bài 16: chỉ đọc → AsNoTracking.
    // Cách khác gọn hơn: .Select(p => new ProductDetailDto(..., p.Category.Name, ...)) — EF tự JOIN, khỏi Include.
    public Task<Product?> GetWithCategoryAsync(int id, CancellationToken ct) =>
        db.Products.AsNoTracking()
                   .Include(p => p.Category)
                   .FirstOrDefaultAsync(p => p.Id == id, ct);

    // Bài 16: KHÔNG dùng AsNoTracking ở đây — entity này sẽ bị sửa rồi SaveChanges.
    // Nếu AsNoTracking, EF không theo dõi thay đổi → SaveChanges không ghi gì cả.
    public Task<Product?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id, ct);

    // Bài 26: lấy cả sản phẩm đã xóa mềm (vẫn còn khóa ngoại trỏ tới category)
    public Task<List<Product>> GetByCategoryAsync(int categoryId, CancellationToken ct) =>
        db.Products.IgnoreQueryFilters().Where(p => p.CategoryId == categoryId).ToListAsync(ct);

    public Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct) =>
        db.Products.AnyAsync(p => p.Name == name && p.Id != excludeId, ct);

    // Bài 40: bản đã sửa của đoạn code review
    public Task<List<ProductDto>> SearchAsync(string name, int take, CancellationToken ct) =>
        db.Products.AsNoTracking()
                   .Where(p => p.Name.Contains(name))
                   .OrderBy(p => p.Name)
                   .Take(take)
                   .Select(ProductMappings.AsDto)
                   .ToListAsync(ct);

    // Bài 40: tăng ViewCount bằng MỘT câu UPDATE, không load entity, không SaveChanges trong vòng lặp.
    public Task IncreaseViewCountAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        db.Products.Where(p => ids.Contains(p.Id))
                   .ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, p => p.ViewCount + 1), ct);

    // Bài 18: muốn thấy bản ghi đã xóa mềm → IgnoreQueryFilters()
    public Task<List<ProductDto>> GetDeletedAsync(CancellationToken ct) =>
        db.Products.IgnoreQueryFilters().AsNoTracking()
                   .Where(p => p.IsDeleted)
                   .OrderBy(p => p.Id)
                   .Select(ProductMappings.AsDto)
                   .ToListAsync(ct);

    public Task<Product?> GetDeletedByIdAsync(int id, CancellationToken ct) =>
        db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted, ct);

    public void Add(Product product) => db.Products.Add(product);

    // Bài 24: gán RowVersion client gửi lên làm "giá trị gốc" → EF dùng nó trong WHERE của câu UPDATE.
    public void SetOriginalRowVersion(Product product, byte[] rowVersion) =>
        db.Entry(product).Property(p => p.RowVersion).OriginalValue = rowVersion;

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
