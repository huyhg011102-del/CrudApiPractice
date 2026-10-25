using System.Linq.Expressions;
using ProductApi.Entities;

namespace ProductApi.Dtos;

// Bài 19: Mapping thủ công bằng extension method.
//
// Map tay vs AutoMapper:
//   + Map tay: rõ ràng, Ctrl+Click là thấy, debug dễ, không reflection, lỗi bắt được lúc compile.
//   + AutoMapper: viết nhanh khi có hàng chục DTO; nhưng cấu hình sai chỉ lộ ra lúc runtime,
//     khó lần "field này lấy từ đâu", và từ v15 AutoMapper đã chuyển sang license thương mại.
public static class ProductMappings
{
    // Dùng cho object đã nằm trong RAM: product.ToDto()
    public static ProductDto ToDto(this Product p) => new(p.Id, p.Name, p.Price);

    // Dùng trong IQueryable: db.Products.Select(ProductMappings.AsDto)
    // Vì là Expression (không phải Func) nên EF dịch được sang SQL → chỉ SELECT Id, Name, Price.
    // Nếu viết .Select(p => p.ToDto()) trên IQueryable, EF vẫn chạy được nhưng phải kéo cả entity về rồi mới map.
    public static readonly Expression<Func<Product, ProductDto>> AsDto =
        p => new ProductDto(p.Id, p.Name, p.Price);

    public static ProductDetailDto ToDetailDto(this Product p) => new(
        p.Id, p.Name, p.Price, p.Stock, p.Category.Name, p.Description, p.CreatedBy,
        Convert.ToBase64String(p.RowVersion));

    public static Product ToEntity(this CreateProductRequest r, string createdBy) => new()
    {
        Name = r.Name.Trim(),
        Price = r.Price,
        Stock = r.Stock,
        CategoryId = r.CategoryId,
        Description = r.Description,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = createdBy,
    };

    public static void Apply(this CreateProductRequest r, Product p)
    {
        p.Name = r.Name.Trim();
        p.Price = r.Price;
        p.Stock = r.Stock;
        p.CategoryId = r.CategoryId;
        p.Description = r.Description;
    }
}
