using Dapper;
using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Dtos;
using ProductApi.Entities;

namespace ProductApi.Services;

public interface IReportService
{
    Task<List<CategorySalesDto>> GetSalesByCategoryAsync(DateTime from, CancellationToken ct);
    Task<List<ProductDto>> SearchRawSqlAsync(string name, CancellationToken ct);
    Task<List<TopProductDto>> GetTopProductsAsync(int top, CancellationToken ct);
    Task<(int ProductCount, int PendingOrders, decimal Revenue)> GetDashboardStatsAsync(CancellationToken ct);
}

public class ReportService(AppDbContext db) : IReportService
{
    // Bài 35: gọi stored procedure (được tạo trong migration AddSalesReportProcedure) và map sang DTO.
    // SqlQuery nhận FormattableString: {from} KHÔNG bị nối chuỗi mà thành tham số @p0 → an toàn SQL injection.
    public Task<List<CategorySalesDto>> GetSalesByCategoryAsync(DateTime from, CancellationToken ct) =>
        db.Database
          .SqlQuery<CategorySalesDto>($"EXEC dbo.sp_GetSalesByCategory @FromDate = {from}")
          .ToListAsync(ct);

    // Bài 35: raw SQL có tham số.
    //   ✔ FromSql($"... {pattern}")              → tham số hóa
    //   ✔ FromSqlRaw("... {0}", pattern)         → tham số hóa
    //   ✘ FromSqlRaw("... '" + name + "'")       → SQL injection!
    // Câu SELECT * này vẫn được EF "bọc" thêm global query filter (IsDeleted = 0) và AsDto.
    public Task<List<ProductDto>> SearchRawSqlAsync(string name, CancellationToken ct)
    {
        var pattern = $"%{name}%";
        return db.Products
                 .FromSql($"SELECT * FROM Products WHERE Name LIKE {pattern}")
                 .AsNoTracking()
                 .OrderBy(p => p.Id)
                 .Select(ProductMappings.AsDto)
                 .ToListAsync(ct);
    }

    // Bài 36: báo cáo tổng hợp viết bằng Dapper ngay trong project EF Core.
    // Dùng lại connection của DbContext (cùng connection string, Dapper tự Open/Close nếu đang đóng).
    // Cũng có thể: new SqlConnection(configuration.GetConnectionString("Default")).
    public async Task<List<TopProductDto>> GetTopProductsAsync(int top, CancellationToken ct)
    {
        const string sql = """
            SELECT TOP (@Top)
                   p.Id                          AS ProductId,
                   p.Name                        AS Name,
                   SUM(oi.Quantity)              AS TotalSold,
                   SUM(oi.Quantity * oi.UnitPrice) AS Revenue
            FROM OrderItems oi
            JOIN Orders   o ON o.Id = oi.OrderId
            JOIN Products p ON p.Id = oi.ProductId
            WHERE o.Status = 'Paid' AND p.IsDeleted = 0
            GROUP BY p.Id, p.Name
            ORDER BY TotalSold DESC, Revenue DESC
            """;

        var connection = db.Database.GetDbConnection();
        var rows = await connection.QueryAsync<TopProductDto>(
            new CommandDefinition(sql, new { Top = top }, cancellationToken: ct));   // @Top là tham số → an toàn
        return rows.ToList();
    }

    // Bài 37: dùng trong ReportsController.GetDashboard
    public async Task<(int ProductCount, int PendingOrders, decimal Revenue)> GetDashboardStatsAsync(CancellationToken ct)
    {
        // Mỗi query await xong mới chạy query tiếp: MỘT DbContext KHÔNG được chạy 2 query song song.
        var productCount = await db.Products.CountAsync(ct);
        var pending = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending, ct);
        var revenue = await db.Orders.Where(o => o.Status == OrderStatus.Paid).SumAsync(o => o.TotalAmount, ct);
        return (productCount, pending, revenue);
    }
}
