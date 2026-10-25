using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Entities;

namespace ProductApi.Services;

// Bài 34: Insert 100.000 sản phẩm.
//
// Vì sao AddRange(100k) + SaveChanges thông thường chậm?
//   - Change tracker phải theo dõi 100k entity (RAM + CPU cho DetectChanges).
//   - EF vẫn gửi từng câu INSERT (gom batch ~42 câu/lần) và đọc lại Id + RowVersion được sinh ra.
//
// Cách 1 — EF chia lô (chunk): tắt AutoDetectChanges, mỗi 5.000 dòng SaveChanges một lần rồi Clear tracker.
//   + Vẫn là EF thuần, giữ validation/interceptor/khóa ngoại, không thêm thư viện.
//   - Vẫn chậm hơn nhiều so với bulk copy (vẫn là các câu INSERT).
//
// Cách 2 — SqlBulkCopy: đẩy thẳng một luồng dữ liệu vào bảng, nhanh nhất (thường gấp 10–50 lần).
//   + Cực nhanh, ít log, ít round-trip.
//   - Bỏ qua EF: không có Id trả về, không chạy interceptor/giá trị mặc định của EF,
//     phải tự map cột; chỉ dùng cho SQL Server.
//
// Cách 3 (nêu khi phỏng vấn) — thư viện EFCore.BulkExtensions: API kiểu EF, bên dưới dùng SqlBulkCopy
//   (lưu ý license của thư viện). Hoặc đẩy file vào staging table bằng BULK INSERT / bcp.
public class BulkInsertService(AppDbContext db, ILogger<BulkInsertService> logger)
{
    private const string Prefix = "Bulk-";

    public async Task<object> InsertWithEfBatchesAsync(int count, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var runId = DateTime.UtcNow.Ticks;
        const int chunkSize = 5_000;

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            for (var start = 0; start < count; start += chunkSize)
            {
                var size = Math.Min(chunkSize, count - start);
                db.Products.AddRange(Enumerable.Range(start, size).Select(i => NewProduct(runId, i)));
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();          // giải phóng các entity đã lưu khỏi tracker
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        logger.LogInformation("EF batch inserted {Count} products in {Elapsed} ms", count, sw.ElapsedMilliseconds);
        return new { method = "ef-batch", count, elapsedMs = sw.ElapsedMilliseconds };
    }

    public async Task<object> InsertWithSqlBulkCopyAsync(int count, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var runId = DateTime.UtcNow.Ticks;

        var table = new DataTable();
        table.Columns.Add(nameof(Product.Name), typeof(string));
        table.Columns.Add(nameof(Product.Price), typeof(decimal));
        table.Columns.Add(nameof(Product.Stock), typeof(int));
        table.Columns.Add(nameof(Product.CreatedAt), typeof(DateTime));
        table.Columns.Add(nameof(Product.CategoryId), typeof(int));
        table.Columns.Add(nameof(Product.IsDeleted), typeof(bool));
        table.Columns.Add(nameof(Product.CreatedBy), typeof(string));
        table.Columns.Add(nameof(Product.ViewCount), typeof(int));

        for (var i = 0; i < count; i++)
        {
            var p = NewProduct(runId, i);
            table.Rows.Add(p.Name, p.Price, p.Stock, p.CreatedAt, p.CategoryId, p.IsDeleted, p.CreatedBy, p.ViewCount);
        }

        var connectionString = db.Database.GetConnectionString();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        using var bulk = new SqlBulkCopy(connection) { DestinationTableName = "Products", BatchSize = 10_000 };
        foreach (DataColumn column in table.Columns)
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);   // Id, RowVersion do SQL Server tự sinh

        await bulk.WriteToServerAsync(table, ct);

        logger.LogInformation("SqlBulkCopy inserted {Count} products in {Elapsed} ms", count, sw.ElapsedMilliseconds);
        return new { method = "sqlbulkcopy", count, elapsedMs = sw.ElapsedMilliseconds };
    }

    // Dọn dữ liệu thử: ExecuteDeleteAsync xóa thẳng bằng 1 câu DELETE, không load entity (Bài 7 cũng nhắc tới)
    public async Task<int> CleanupAsync(CancellationToken ct) =>
        await db.Products.IgnoreQueryFilters()
                         .Where(p => p.Name.StartsWith(Prefix))
                         .ExecuteDeleteAsync(ct);

    private static Product NewProduct(long runId, int i) => new()
    {
        Name = $"{Prefix}{runId}-{i}",       // tên phải duy nhất (unique index)
        Price = 10_000 + i % 1000,
        Stock = i % 100,
        CreatedAt = DateTime.UtcNow,
        CategoryId = 1,
        CreatedBy = "bulk",
    };
}
