using System.ComponentModel.DataAnnotations;

namespace ProductApi.Dtos;

// Bài 9: DTO trả ra ngoài — chỉ Id, Name, Price. KHÔNG trả entity Product trực tiếp vì:
//   1. Lộ field nội bộ (IsDeleted, RowVersion, CreatedBy, ViewCount...).
//   2. Navigation 2 chiều (Product.Category.Products...) → JSON serialize vòng lặp vô tận.
//   3. Tách hợp đồng API khỏi cấu trúc DB: đổi DB không làm vỡ client.
public record ProductDto(int Id, string Name, decimal Price);

// Bài 15: DTO chi tiết kèm tên category. Bài 24: kèm RowVersion để client gửi lại khi PUT.
public record ProductDetailDto(
    int Id,
    string Name,
    decimal Price,
    int Stock,
    string CategoryName,
    string? Description,
    string CreatedBy,
    string RowVersion);   // byte[] → base64 cho dễ truyền qua JSON

// Bài 9 + 10: request tạo mới, kèm validation.
// Có [ApiController] → body sai sẽ tự trả 400 (ValidationProblemDetails), không cần check ModelState.
public class CreateProductRequest
{
    [Required(ErrorMessage = "Tên sản phẩm là bắt buộc")]
    [MaxLength(100, ErrorMessage = "Tên tối đa 100 ký tự")]
    public string Name { get; set; } = "";

    [Range(0.01, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho không được âm")]
    public int Stock { get; set; }

    // Thêm từ Bài 15 (quan hệ với Category)
    [Range(1, int.MaxValue, ErrorMessage = "CategoryId không hợp lệ")]
    public int CategoryId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

// Bài 6 + 24: request cập nhật — bắt buộc gửi kèm RowVersion lấy được lúc GET.
public class UpdateProductRequest : CreateProductRequest
{
    [Required(ErrorMessage = "Phải gửi RowVersion (lấy từ GET /api/products/{id})")]
    public string RowVersion { get; set; } = "";
}

// Bài 13 + 14: query string cho danh sách: ?page=1&pageSize=20&name=abc&minPrice=100&sortBy=price&desc=true
public class ProductQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Name { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? CategoryId { get; set; }
    public string? SortBy { get; set; }     // name | price | createdAt (mặc định: id)
    public bool Desc { get; set; }
}

// Bài 13: kết quả phân trang
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
