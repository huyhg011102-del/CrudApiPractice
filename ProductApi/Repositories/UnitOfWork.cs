using ProductApi.Data;

namespace ProductApi.Repositories;

// Bài 26: Unit of Work — gom nhiều repository, chỉ SaveChanges MỘT lần ở cuối.
//
// Vì tất cả cùng là Scoped, trong một request chúng dùng CHUNG một instance AppDbContext
// → mọi thay đổi qua các repository nằm trong cùng change tracker → 1 lần SaveChanges = 1 transaction.
//
// Câu hỏi: Với EF Core thì DbContext đã là Unit of Work sẵn chưa, có cần lớp này không?
//   - Đúng, DbContext ĐÃ là Unit of Work (theo dõi thay đổi + SaveChanges atomically),
//     và DbSet<T> đã là Repository. Bọc thêm một lớp UoW + Repository nhiều khi chỉ là lặp lại.
//   - Lớp này đáng có khi: muốn ẩn EF khỏi tầng service (dễ mock khi unit test),
//     hoặc team quy ước rõ "repository không được tự SaveChanges".
//   - Không đáng khi: project nhỏ, CRUD đơn giản — dùng thẳng DbContext trong service là đủ.
public interface IUnitOfWork
{
    IProductRepository Products { get; }
    ICategoryRepository Categories { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class UnitOfWork(AppDbContext db, IProductRepository products, ICategoryRepository categories) : IUnitOfWork
{
    public IProductRepository Products { get; } = products;
    public ICategoryRepository Categories { get; } = categories;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
