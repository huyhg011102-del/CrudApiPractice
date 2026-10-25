using Microsoft.EntityFrameworkCore;
using ProductApi.Entities;

namespace ProductApi.Data;

/// <summary>Dữ liệu mẫu, được chèn vào DB qua migration (HasData).</summary>
public static class SeedData
{
    public static void Apply(ModelBuilder b)
    {
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        b.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Điện tử" },
            new Category { Id = 2, Name = "Thời trang" },
            new Category { Id = 3, Name = "Gia dụng" },
            new Category { Id = 4, Name = "Sách" },
            new Category { Id = 5, Name = "Trống" });   // category chưa có sản phẩm nào — để test xóa category

        // CreatedBy: "staff1" / "staff2" / "admin" — dùng để thử Bài 30 (chỉ sửa sản phẩm của mình)
        b.Entity<Product>().HasData(
            P(1,  "Laptop Dell XPS 13",       1, 32_000_000, 5,   "admin"),
            P(2,  "iPhone 15",                1, 22_000_000, 8,   "admin"),
            P(3,  "Tai nghe Sony WH-1000XM5", 1,  7_500_000, 12,  "staff1"),
            P(4,  "Chuột Logitech MX",        1,  2_200_000, 30,  "staff1"),
            P(5,  "Áo thun basic",            2,    150_000, 200, "staff2"),
            P(6,  "Quần jean",                2,    450_000, 80,  "staff2"),
            P(7,  "Giày sneaker",             2,  1_200_000, 25,  "staff2"),
            P(8,  "Nồi cơm điện",             3,  1_350_000, 15,  "staff1"),
            P(9,  "Ấm siêu tốc",              3,    320_000, 35,  "staff1"),
            P(10, "Clean Code",               4,    280_000, 50,  "admin"),
            P(11, "Đắc nhân tâm",             4,     95_000, 2,   "admin"),
            P(12, "Atomic Habits",            4,    180_000, 0,   "staff2"));

        Product P(int id, string name, int categoryId, decimal price, int stock, string createdBy) => new()
        {
            Id = id, Name = name, CategoryId = categoryId, Price = price, Stock = stock,
            CreatedAt = t0.AddDays(id), CreatedBy = createdBy,
        };

        b.Entity<Order>().HasData(
            new Order { Id = 1, CustomerName = "Nguyễn Văn An",  CreatedAt = t0.AddMonths(1), Status = OrderStatus.Paid,      TotalAmount = 34_200_000 },
            new Order { Id = 2, CustomerName = "Trần Thị Bình",  CreatedAt = t0.AddMonths(2), Status = OrderStatus.Paid,      TotalAmount = 750_000 },
            new Order { Id = 3, CustomerName = "Lê Hoàng Cường", CreatedAt = t0.AddMonths(3), Status = OrderStatus.Paid,      TotalAmount = 2_510_000 },
            new Order { Id = 4, CustomerName = "Phạm Thu Dung",  CreatedAt = t0.AddMonths(4), Status = OrderStatus.Cancelled, TotalAmount = 22_000_000 },
            // Đơn Pending đã quá 24h → Bài 33: job nền sẽ chuyển sang Expired ngay lần chạy đầu
            new Order { Id = 5, CustomerName = "Hoàng Minh Đức", CreatedAt = t0.AddMonths(5), Status = OrderStatus.Pending,   TotalAmount = 280_000 });

        b.Entity<OrderItem>().HasData(
            new OrderItem { Id = 1, OrderId = 1, ProductId = 1,  ProductName = "Laptop Dell XPS 13", UnitPrice = 32_000_000, Quantity = 1 },
            new OrderItem { Id = 2, OrderId = 1, ProductId = 4,  ProductName = "Chuột Logitech MX",  UnitPrice = 2_200_000,  Quantity = 1 },
            new OrderItem { Id = 3, OrderId = 2, ProductId = 5,  ProductName = "Áo thun basic",      UnitPrice = 150_000,    Quantity = 2 },
            new OrderItem { Id = 4, OrderId = 2, ProductId = 6,  ProductName = "Quần jean",          UnitPrice = 450_000,    Quantity = 1 },
            new OrderItem { Id = 5, OrderId = 3, ProductId = 8,  ProductName = "Nồi cơm điện",       UnitPrice = 1_350_000,  Quantity = 1 },
            new OrderItem { Id = 6, OrderId = 3, ProductId = 9,  ProductName = "Ấm siêu tốc",        UnitPrice = 320_000,    Quantity = 1 },
            new OrderItem { Id = 7, OrderId = 3, ProductId = 10, ProductName = "Clean Code",         UnitPrice = 280_000,    Quantity = 3 },
            new OrderItem { Id = 8, OrderId = 4, ProductId = 2,  ProductName = "iPhone 15",          UnitPrice = 22_000_000, Quantity = 1 },
            new OrderItem { Id = 9, OrderId = 5, ProductId = 10, ProductName = "Clean Code",         UnitPrice = 280_000,    Quantity = 1 });
    }
}
