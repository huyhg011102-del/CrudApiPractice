# Lý thuyết và các bài "đọc code, chỉ lỗi"

Câu trả lời cho phần **giải thích** của từng bài, kèm các bài không viết thành code chạy được (25, 37, 38, 40 và test 27–28).
Code tương ứng: tìm `Bài N` trong solution (Ctrl+Shift+F).

---

## Nền tảng (Bài 2, 4, 8, 9, 16)

**Bài 2. Vì sao `AddDbContext` là Scoped?**
`DbContext` giữ change tracker và connection, **không thread-safe**. Scoped nghĩa là mỗi HTTP request có một instance riêng, dùng xong thì dispose. Nếu là Singleton, mọi request dùng chung một context: dữ liệu lẫn nhau và lỗi đa luồng. Nếu là Transient, trong một request mỗi repository lại nhận một context khác nhau, nên không SaveChanges chung được.

**Bài 4. `FindAsync` khác `FirstOrDefaultAsync` thế nào?**
`FindAsync(id)` chỉ tìm theo **khóa chính**. Nó xem change tracker trước, entity đã được load rồi thì không query DB nữa. `FirstOrDefaultAsync(p => ...)` lọc được theo điều kiện bất kỳ, nhưng **luôn** query DB. Cả hai đều áp dụng global query filter.

**Bài 8. Vì sao không dùng `.Result` / `.Wait()`?**
Hai cách này chặn thread trong lúc chờ I/O. Khi tải cao sẽ dẫn tới thread pool starvation, xem bài 37. Hãy dùng `async`/`await` từ đầu đến cuối, và nhận `CancellationToken` để huỷ query khi client ngắt kết nối.

| Đồng bộ | Bất đồng bộ (EF Core) |
|---|---|
| `ToList()` | `ToListAsync()` |
| `Find()` | `FindAsync()` |
| `FirstOrDefault()` | `FirstOrDefaultAsync()` |
| `SaveChanges()` | `SaveChangesAsync()` |
| `Any()` / `Count()` | `AnyAsync()` / `CountAsync()` |
| — | `ExecuteUpdateAsync()` / `ExecuteDeleteAsync()` (.NET 7+) |

**Bài 9. Vì sao không trả entity ra ngoài?**
(1) Lộ field nội bộ như `IsDeleted`, `RowVersion`, `CreatedBy`. (2) Navigation hai chiều gây vòng lặp khi serialize JSON. (3) API bị gắn chặt vào cấu trúc DB, đổi một cột là vỡ client. (4) Over-posting: nhận entity làm input thì client gửi được cả `Id`, `IsDeleted`...

**Bài 16. `AsNoTracking` làm gì, khi nào KHÔNG được dùng?**
Mặc định EF chụp lại giá trị của mọi entity được load để phát hiện thay đổi, việc này tốn RAM và CPU. Endpoint chỉ đọc thì thêm `AsNoTracking()` để bỏ phần đó. **Không dùng** khi định sửa entity rồi `SaveChanges`: EF không theo dõi nên sẽ không ghi gì xuống DB. Có thể bật mặc định cho cả context bằng `UseQueryTrackingBehavior(NoTracking)`.

---

## Bài 17. Migration

```bash
dotnet tool install --global dotnet-ef           # cài 1 lần
dotnet ef migrations add InitialCreate            # tạo migration đầu tiên
dotnet ef database update                         # áp dụng vào DB
# ... thêm property Description vào Product ...
dotnet ef migrations add AddProductDescription    # migration thứ 2
dotnet ef database update
dotnet ef database update InitialCreate           # rollback về một migration cũ (chạy Down())
dotnet ef migrations remove                       # xóa migration CUỐI (chỉ khi chưa apply)
dotnet ef migrations script --idempotent          # sinh file SQL để DBA chạy trên production
```

- Mỗi migration gồm `Up()` (áp dụng) và `Down()` (rollback). File `.Designer.cs` cùng `AppDbContextModelSnapshot.cs` là ảnh chụp model, **không sửa tay**.
- **Không sửa migration đã apply lên production.** Muốn thay đổi thì tạo migration mới.
- Project này có 3 migration: `ProductApi/Data/Migrations/`. Migration thứ 3 tạo stored procedure cho bài 35.

---

## Bài 19. Map tay hay dùng AutoMapper?

| | Map tay (`ToDto()`) | AutoMapper |
|---|---|---|
| Rõ ràng / debug | Ctrl+Click là thấy | "Ma thuật", khó lần field lấy từ đâu |
| Lỗi | Bắt được lúc compile | Lỗi cấu hình chỉ lộ ra lúc runtime |
| Hiệu năng | Không dùng reflection | Có chi phí cấu hình và reflection |
| Viết nhanh | Lâu hơn khi có hàng chục DTO | Nhanh |
| Dịch sang SQL | Viết dạng `Expression` thì được | Có `ProjectTo<T>()` |

Từ v15, AutoMapper chuyển sang license thương mại, nên nhiều team quay lại map tay hoặc dùng Mapperly (source generator).

---

## Bài 20. Status code

| Code | Khi nào | Ví dụ trong project |
|---|---|---|
| 200 | Đọc thành công | GET |
| 201 | Tạo mới, kèm header `Location` | POST |
| 204 | Thành công, không có body | PUT, DELETE |
| 400 | Request **sai định dạng hoặc validation** | thiếu Name, Price = 0 |
| 401 | **Chưa đăng nhập** hoặc token sai/hết hạn | POST không có token |
| 403 | Đã đăng nhập nhưng **không có quyền** | Staff xóa, sửa sản phẩm người khác |
| 404 | Không tìm thấy | `/products/9999` |
| 409 | **Xung đột trạng thái** | trùng tên, hết hàng, RowVersion cũ, khóa ngoại |
| 422 | Đúng cú pháp nhưng **sai nghiệp vụ** | giảm giá > 50%, category không tồn tại |
| 429 | Vượt rate limit | bài 32 |
| 500 | Lỗi phía server | exception không lường trước |

409 và 422 dễ nhầm: **409** là xung đột với *trạng thái hiện tại* của tài nguyên (thử lại sau khi trạng thái đổi thì có thể thành công). **422** là bản thân yêu cầu vi phạm quy tắc (gửi lại y nguyên thì vẫn sai).

---

## Bài 25. N+1 query ⭐

```csharp
var orders = await db.Orders.ToListAsync();          // 1 query lấy N đơn
foreach (var o in orders)
{
    var customerName = o.Customer.Name;              // lazy load → MỖI vòng 1 query
    Console.WriteLine($"{o.Id} - {customerName}");
}
// Tổng: 1 + N query. 1.000 đơn = 1.001 round-trip tới DB.
```

**Vì sao xảy ra:** lazy loading (proxy) tự query khi chạm vào navigation `o.Customer` lần đầu.

**Cách sửa:**

```csharp
// Cách 1: Include → 1 query có JOIN
var orders = await db.Orders.Include(o => o.Customer).ToListAsync();

// Cách 2 (tốt nhất): projection — chỉ lấy đúng cột cần, EF tự JOIN, không cần Include
var rows = await db.Orders
    .Select(o => new { o.Id, CustomerName = o.Customer.Name })
    .ToListAsync();
```

**Cách phát hiện:** bật log `Microsoft.EntityFrameworkCore.Database.Command` (đã bật ở `appsettings.Development.json`) rồi đếm số câu SQL trong một request. Có thể dùng MiniProfiler hoặc Application Insights. Nhiều team **không bật lazy loading** để tránh lỗi này.

---

## Bài 26. DbContext đã là Unit of Work chưa?

Rồi. `DbContext` theo dõi mọi thay đổi và `SaveChanges()` ghi tất cả trong **một transaction**, đó chính là Unit of Work. `DbSet<T>` cũng đã là Repository. Bọc thêm UoW + Repository chỉ đáng khi muốn ẩn EF khỏi tầng service (dễ mock khi unit test) hoặc team có quy ước rõ ràng. Với CRUD đơn giản, dùng thẳng DbContext là đủ. Xem `Repositories/UnitOfWork.cs`.

---

## Bài 27. Unit test `ProductService.CreateAsync` (xUnit + Moq)

```csharp
public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _repo = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IAuthorizationService> _auth = new();
    private readonly ProductService _sut;                      // sut = system under test

    public ProductServiceTests() =>
        _sut = new ProductService(_repo.Object, _categories.Object, _auth.Object, NullLogger<ProductService>.Instance);

    [Fact]
    public async Task CreateAsync_ValidRequest_AddsAndSaves()
    {
        var request = new CreateProductRequest { Name = "Mới", Price = 1000, Stock = 1, CategoryId = 1 };
        _repo.Setup(r => r.NameExistsAsync("Mới", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _categories.Setup(c => c.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repo.Setup(r => r.GetWithCategoryAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Product { Name = "Mới", Category = new Category { Name = "A" }, RowVersion = new byte[8] });

        var result = await _sut.CreateAsync(request, "staff1", CancellationToken.None);

        Assert.Equal("Mới", result.Name);
        _repo.Verify(r => r.Add(It.Is<Product>(p => p.Name == "Mới" && p.CreatedBy == "staff1")), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsConflict()
    {
        _repo.Setup(r => r.NameExistsAsync("Trùng", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(new CreateProductRequest { Name = "Trùng", Price = 1, CategoryId = 1 }, "staff1", CancellationToken.None));

        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);   // không được lưu
    }
}
```

Cấu trúc **Arrange, Act, Assert**. Mock những thứ service phụ thuộc, sau đó kiểm tra cả **kết quả** lẫn **hành vi** (`Verify`).

## Bài 28. Integration test bằng `WebApplicationFactory` + InMemory

```csharp
public class ProductsApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PostBasicProduct_Returns201()
    {
        var client = factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();                 // bỏ SQL Server
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("test"));
        })).CreateClient();

        var response = await client.PostAsJsonAsync("/api/basic/products",
            new { name = "Test", price = 1000, stock = 1, categoryId = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }
}
// Program.cs cần thêm ở cuối:  public partial class Program;
```

**Hạn chế của InMemory provider so với SQL Server thật:** không có transaction thật, không kiểm tra khóa ngoại và unique index, không có rowversion/concurrency, không chạy raw SQL, stored procedure hay `ExecuteUpdate/Delete`. So sánh chuỗi lại phân biệt hoa thường (SQL Server mặc định thì không), và nhiều câu LINQ chạy được trên InMemory nhưng **không dịch được sang SQL**. Nên dùng **SQLite in-memory** hoặc **Testcontainers** (SQL Server chạy trong Docker) để sát thực tế.

---

## Bài 31. Vì sao cache phải là Singleton?

Cache phải **sống lâu hơn một request** và **dùng chung** cho mọi request. Nếu là Scoped, mỗi request có một cache rỗng mới, nên cache không có tác dụng gì. `AddMemoryCache()` đã đăng ký Singleton sẵn. Lưu ý: `IMemoryCache` nằm trong RAM của **từng server**. Chạy nhiều instance thì cache lệch nhau, khi đó cần cache phân tán như Redis (`IDistributedCache` / `HybridCache`).

## Bài 33. BackgroundService là Singleton, DbContext là Scoped thì làm sao?

Inject `IServiceScopeFactory`, rồi mỗi lần job chạy tạo `CreateAsyncScope()` và lấy `AppDbContext` từ scope đó. Xem `BackgroundJobs/OrderExpirationService.cs`. Nếu inject thẳng DbContext vào constructor, app báo lỗi khi khởi động: *"Cannot consume scoped service from singleton"*.

## Bài 34. Bulk insert: so sánh đánh đổi

Kết quả đo trên máy bạn (LocalDB): **SqlBulkCopy 100.000 dòng ≈ 2,5s**, còn **EF chia lô 20.000 dòng ≈ 2,4s**. Như vậy SqlBulkCopy nhanh hơn khoảng 5 lần.

| | EF chia lô (`AddRange` + `SaveChanges` mỗi 5.000, `ChangeTracker.Clear()`) | `SqlBulkCopy` |
|---|---|---|
| Tốc độ | Trung bình | Nhanh nhất |
| Giữ logic EF (giá trị mặc định, interceptor, Id trả về) | Có | Không |
| Phụ thuộc DB | Không | Chỉ SQL Server |
| Code | Ngắn | Phải tự map cột |

Cách thứ 3: thư viện `EFCore.BulkExtensions` (API kiểu EF, bên dưới dùng bulk copy, chú ý license).

## Bài 36. Khi nào dùng EF Core, khi nào dùng Dapper?

- **EF Core:** CRUD, nghiệp vụ có nhiều quan hệ, cần change tracking, migration, LINQ type-safe. Chiếm khoảng 90% công việc.
- **Dapper:** báo cáo hoặc truy vấn phức tạp mà bạn muốn **tự viết SQL tối ưu** (CTE, window function, hint), những query nóng cần nhanh tối đa, hoặc DB có sẵn với nhiều stored procedure.
- Hai thứ **dùng chung được** trong một project, thậm chí chung connection (`db.Database.GetDbConnection()`).

---

## Bài 37. Code chặn thread ⭐

```csharp
[HttpGet]
public IActionResult GetReport()
{
    var data = _service.GetDataAsync().Result;                           // (1)
    var extra = Task.Run(() => _http.GetStringAsync(url)).Result;       // (2)
    return Ok(new { data, extra });
}
```

**Vấn đề:**
1. `.Result` **chặn** thread request trong suốt thời gian chờ DB. Thread nằm im, không phục vụ request khác được.
2. `Task.Run(...)` lấy **thêm một thread** từ pool để chạy việc vốn đã async, rồi lại `.Result` để chặn tiếp. Kết quả là chiếm 2 thread cho một request (anti-pattern "sync over async").
3. Khi tải cao, mọi thread trong pool đều đang bị chặn chờ. Thread pool chỉ tạo thêm thread rất chậm (khoảng 1–2 thread/giây), nên request xếp hàng, latency tăng vọt rồi timeout. Đây gọi là **thread pool starvation**.
4. Hai việc độc lập (DB và HTTP) đang bị làm **tuần tự**.

**Sửa:** xem `ReportsController.GetDashboard`.

```csharp
[HttpGet]
public async Task<IActionResult> GetReport(CancellationToken ct)
{
    var dataTask = _service.GetDataAsync(ct);
    var extraTask = _http.GetStringAsync(url, ct);
    await Task.WhenAll(dataTask, extraTask);                 // song song, không chặn thread nào
    return Ok(new { data = dataTask.Result, extra = extraTask.Result });   // đã xong nên .Result không chặn
}
```

**Phát hiện trên production:** `dotnet-counters monitor` thấy *ThreadPool Queue Length* tăng và *Thread Count* tăng dần, trong khi CPU **thấp**. `dotnet-dump` / `dotnet-stack` thấy rất nhiều thread đứng ở `Task.Wait` / `GetResult`. APM (Application Insights) thấy latency tăng vọt dù DB không chậm.

---

## Bài 38. Lỗi DI lifetime ⭐

```csharp
builder.Services.AddDbContext<AppDbContext>(...);                 // Scoped
builder.Services.AddSingleton<IOrderService, OrderService>();      // ✘ Singleton phụ thuộc Scoped

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    public OrderService(AppDbContext db) => _db = db;
}
```

**Lỗi:** Singleton giữ một dependency Scoped. Đây gọi là **captive dependency**, dependency bị "giam" sống lâu hơn vòng đời của nó.

**Hậu quả cụ thể:**
- Ở Development (`ValidateScopes = true`): app **không khởi động được**, báo `Cannot consume scoped service 'AppDbContext' from singleton 'IOrderService'`.
- Nếu tắt kiểm tra (Production mặc định): **một DbContext dùng chung cho mọi request suốt đời app**.
  - Hai request đồng thời dùng một context: `InvalidOperationException: A second operation was started on this context instance...` (DbContext không thread-safe).
  - Change tracker **phình to mãi**, rò rỉ bộ nhớ, và trả về dữ liệu cũ đang được cache trong tracker.
  - Connection lỗi một lần là service hỏng cho tới khi restart app.

**Sửa:** `AddScoped<IOrderService, OrderService>()` (xem `Program.cs`). Nếu buộc phải là Singleton, inject `IServiceScopeFactory` hoặc `IDbContextFactory<AppDbContext>` rồi tạo context mỗi lần dùng.

**Quy tắc vòng đời:** chỉ được inject *dài sống hơn hoặc bằng* vào *ngắn sống hơn*. Ví dụ Singleton vào Scoped là được, Scoped vào Singleton là **không**.

---

## Bài 40. Code review ⭐ (bản sửa: `ProductsController.Search` → `ProductService.SearchAsync`)

```csharp
[HttpGet("search")]
public List<Product> Search(string name)
{
    var sql = "SELECT * FROM Products WHERE Name LIKE '%" + name + "%'";
    var db = new AppDbContext();
    var result = db.Products.FromSqlRaw(sql).ToList();
    foreach (var p in result)
    {
        p.ViewCount = p.ViewCount + 1;
        db.SaveChanges();
    }
    return result;
}
```

| # | Lỗi | Hậu quả | Sửa |
|---|---|---|---|
| 1 | **SQL injection**: nối chuỗi `name` vào SQL | `name = "' ; DROP TABLE Products --"` | LINQ `Where(p => p.Name.Contains(name))` hoặc `FromSql($"... {pattern}")` |
| 2 | `new AppDbContext()` thủ công | Bỏ qua DI và cấu hình, connection string hard-code | Inject qua constructor |
| 3 | **Không dispose** DbContext | Rò rỉ connection | DI tự dispose theo scope |
| 4 | **Đồng bộ** (`ToList`, `SaveChanges`) | Chặn thread (bài 37) | `async Task<...>`, `ToListAsync`, `CancellationToken` |
| 5 | **`SaveChanges` trong vòng lặp** | N round-trip, N transaction, chậm và không nguyên tử | Một lần `SaveChanges` sau vòng lặp, hoặc tốt hơn là `ExecuteUpdateAsync` (1 câu UPDATE) |
| 6 | **Trả thẳng entity** `List<Product>` | Lộ field nội bộ, vòng lặp JSON (bài 9) | Trả `ProductDto` |
| 7 | **Không giới hạn số dòng** | `name = ""` thì trả cả bảng | `Take(50)` hoặc phân trang |
| 8 | **Không validate input** | `name` null hoặc rỗng | `[Required, MinLength(2), MaxLength(100)]` |
| 9 | `SELECT *` | Lấy thừa cột | Projection `Select(...)` |
| 10 | `LIKE '%...%'` | Không dùng được index | Chấp nhận với dữ liệu nhỏ; lớn thì Full-Text Search |
| 11 | Trả `List<Product>` thay vì `ActionResult<T>` | Không trả được 400/404 đúng cách | `ActionResult<List<ProductDto>>` |
| 12 | Một request GET lại **ghi dữ liệu** | GET phải an toàn và idempotent; crawler hay prefetch cũng làm tăng lượt xem | Đếm view bằng event/queue riêng, hoặc tách thành POST |
| 13 | Đếm view bằng read-modify-write | 2 request đồng thời làm mất lượt đếm | `SET ViewCount = ViewCount + 1` ngay trong SQL (`ExecuteUpdate`) |

---

## Phụ lục: câu hỏi hay đi kèm

- **`IEnumerable` khác `IQueryable` thế nào?** `IQueryable` giữ biểu thức để EF dịch sang SQL và chạy ở DB. `IEnumerable` chạy trong RAM. Gọi `ToList()` hoặc `AsEnumerable()` quá sớm sẽ kéo cả bảng về.
- **Middleware khác filter thế nào?** Middleware áp dụng cho mọi request ở tầng pipeline. Filter (`IActionFilter`, `IExceptionFilter`) chỉ áp dụng trong MVC và biết action/model nào đang chạy.
- **Vì sao `UseAuthentication` phải đứng trước `UseAuthorization`?** Phải biết "anh là ai" thì mới quyết định được "anh được làm gì".
- **PUT khác PATCH thế nào?** PUT thay thế toàn bộ tài nguyên và idempotent. PATCH chỉ sửa một phần.
