using System.ComponentModel.DataAnnotations;

namespace ProductApi.Dtos;

// ===== Category (Bài 26, 31) =====
public record CategoryDto(int Id, string Name, int ProductCount);

public class CategoryRequest
{
    [Required, MaxLength(50)]
    public string Name { get; set; } = "";
}

// ===== Order (Bài 23) =====
public class CreateOrderRequest
{
    [Required, MaxLength(100)]
    public string CustomerName { get; set; } = "";

    [Required, MinLength(1, ErrorMessage = "Đơn hàng phải có ít nhất 1 sản phẩm")]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }
}

public record OrderItemDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice);
public record OrderDto(int Id, string CustomerName, string Status, decimal TotalAmount, DateTime CreatedAt, List<OrderItemDto> Items);

// ===== Auth (Bài 29) =====
public record LoginRequest([Required] string UserName, [Required] string Password);
public record LoginResponse(string AccessToken, DateTime ExpiresAt, string Role);

// ===== Reports (Bài 35, 36, 37) =====
// Dùng class có setter (không dùng record positional) để EF SqlQuery và Dapper map theo tên cột dễ dàng.
public class CategorySalesDto
{
    public string CategoryName { get; set; } = "";
    public int TotalQuantity { get; set; }
    public decimal Revenue { get; set; }
}

public class TopProductDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public int TotalSold { get; set; }
    public decimal Revenue { get; set; }
}

public record DashboardDto(int ProductCount, int PendingOrders, decimal RevenueVnd, decimal UsdRate, decimal RevenueUsd);
