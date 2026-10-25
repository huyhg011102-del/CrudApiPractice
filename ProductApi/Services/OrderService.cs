using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Dtos;
using ProductApi.Entities;
using ProductApi.Exceptions;

namespace ProductApi.Services;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct);
    Task<OrderDto> GetByIdAsync(int id, CancellationToken ct);
}

// Bài 23: Tạo đơn hàng = tạo Order + tạo các OrderItem + trừ Stock từng sản phẩm.
// Bất kỳ bước nào lỗi → rollback toàn bộ. Tồn kho không đủ → 409.
//
// (Service này dùng thẳng DbContext thay vì repository — để thấy rõ API transaction của EF.)
public class OrderService(AppDbContext db, ILogger<OrderService> logger) : IOrderService
{
    public async Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        // Mở transaction tường minh. "await using" → nếu thoát khỏi hàm mà CHƯA Commit
        // (do exception ở bất kỳ đâu), transaction bị Dispose → tự ROLLBACK.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Gộp các dòng trùng ProductId (client gửi 2 dòng cùng 1 sản phẩm)
        var lines = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();

        var ids = lines.Select(l => l.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);  // 1 query, không N+1

        var order = new Order
        {
            CustomerName = request.CustomerName.Trim(),
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending,
        };

        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new NotFoundException($"Không tìm thấy sản phẩm {line.ProductId}");

            if (product.Stock < line.Quantity)
                throw new ConflictException($"'{product.Name}' chỉ còn {product.Stock}, không đủ {line.Quantity}");

            product.Stock -= line.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = line.Quantity,
            });
        }

        order.TotalAmount = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        db.Orders.Add(order);

        // Hai người cùng mua món cuối cùng? Product có RowVersion → người lưu sau bị
        // DbUpdateConcurrencyException (409) thay vì làm Stock âm.
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Order {OrderId} created for {Customer}, {ItemCount} items, total {Total}",
            order.Id, order.CustomerName, order.Items.Count, order.TotalAmount);

        return ToDto(order);

        // GHI CHÚ khi phỏng vấn:
        //  - Ở bài này chỉ có MỘT lần SaveChanges, mà bản thân SaveChanges đã chạy trong transaction
        //    → về kỹ thuật không cần BeginTransaction. Transaction tường minh cần khi có NHIỀU lần
        //    SaveChanges, hoặc trộn EF với raw SQL / Dapper trong cùng một nghiệp vụ.
        //  - Nếu bật EnableRetryOnFailure thì phải bọc bằng db.Database.CreateExecutionStrategy().ExecuteAsync(...).
    }

    public async Task<OrderDto> GetByIdAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
                                   .Include(o => o.Items)
                                   .FirstOrDefaultAsync(o => o.Id == id, ct)
                    ?? throw new NotFoundException($"Không tìm thấy đơn hàng {id}");
        return ToDto(order);
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id, o.CustomerName, o.Status.ToString(), o.TotalAmount, o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)).ToList());
}
