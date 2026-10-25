using System.ComponentModel.DataAnnotations;

namespace ProductApi.Entities;

// Bài 1: Entity Product (Id, Name, Price, Stock, CreatedAt).
// Property tên "Id" (hoặc "<TênClass>Id") → EF tự nhận là khóa chính, không cần [Key].
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; }

    // Bài 17: cột được thêm ở migration thứ hai (AddProductDescription)
    public string? Description { get; set; }

    // Bài 15: quan hệ 1-n — một Category có nhiều Product.
    // CategoryId là khóa ngoại, Category là navigation property.
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Bài 18: soft delete — không xóa thật, chỉ đánh dấu (xem HasQueryFilter trong AppDbContext)
    public bool IsDeleted { get; set; }

    // Bài 24: optimistic concurrency. SQL Server tự tăng cột rowversion mỗi lần UPDATE dòng này.
    // EF thêm "WHERE RowVersion = @giá_trị_cũ" vào câu UPDATE → nếu ai đó đã sửa trước,
    // không dòng nào bị ảnh hưởng → EF ném DbUpdateConcurrencyException.
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // Bài 30: người tạo — dùng cho policy "chỉ được sửa sản phẩm do chính mình tạo"
    public string CreatedBy { get; set; } = "";

    // Bài 40: đếm lượt xem khi search
    public int ViewCount { get; set; }
}
