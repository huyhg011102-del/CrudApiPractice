using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProductApi.Entities;

namespace ProductApi.Data;

// Bài 1: DbContext cần constructor nhận DbContextOptions<AppDbContext> và truyền lên base.
// (Cú pháp primary constructor của C# 12 — tương đương constructor thường.)
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // EF cảnh báo: Product có query filter (soft delete) mà OrderItem lại "bắt buộc" trỏ tới Product
        // → nếu Include(i => i.Product) thì item của sản phẩm đã xóa mềm sẽ biến mất khỏi kết quả.
        // Ở đây an toàn vì OrderItem đã lưu sẵn ProductName/UnitPrice và không bao giờ đi qua navigation Product.
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Price).HasPrecision(18, 2);     // decimal luôn phải khai báo precision
            e.Property(p => p.CreatedBy).HasMaxLength(50);
            e.Property(p => p.Description).HasMaxLength(500);

            // Bài 18: Global query filter — MỌI truy vấn vào Products tự thêm "WHERE IsDeleted = 0".
            // Muốn lấy cả bản ghi đã xóa: db.Products.IgnoreQueryFilters()
            e.HasQueryFilter(p => !p.IsDeleted);

            // Bài 12/20: tên sản phẩm là duy nhất (chỉ tính sản phẩm chưa xóa) — chốt chặn cuối ở tầng DB
            e.HasIndex(p => p.Name).IsUnique().HasFilter("[IsDeleted] = 0");

            // Bài 15: cấu hình quan hệ 1-n (EF tự suy ra được, viết ra cho rõ).
            // Restrict: không cho xóa Category khi còn Product trỏ tới.
            e.HasOne(p => p.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.CustomerName).HasMaxLength(100).IsRequired();
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);  // lưu enum dạng chữ cho dễ đọc
            e.HasIndex(o => new { o.Status, o.CreatedAt });                      // Bài 33: job quét theo 2 cột này
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.ProductName).HasMaxLength(100);
            e.Property(i => i.UnitPrice).HasPrecision(18, 2);
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        SeedData.Apply(modelBuilder);
    }
}
