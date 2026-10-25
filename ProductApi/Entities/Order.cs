namespace ProductApi.Entities;

// Bài 23 (tạo đơn trong transaction), Bài 33 (job đánh dấu Expired), Bài 35–36 (báo cáo)
public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public enum OrderStatus
{
    Pending,    // vừa tạo, chưa thanh toán
    Paid,
    Expired,    // Bài 33: quá 24h chưa thanh toán
    Cancelled,
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    // "Chụp" lại tên và giá tại thời điểm đặt hàng: sau này sản phẩm đổi giá/đổi tên/bị xóa mềm
    // thì đơn cũ vẫn giữ đúng thông tin lúc mua.
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
