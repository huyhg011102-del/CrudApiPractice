# CRUD / API Practice: đáp án 40 bài

Một Web API **.NET 10 + EF Core + SQL Server LocalDB** chứa đáp án phần 2 (CRUD/API) của `LINQ_CRUD_API_80_bai_tap.docx`.
Mọi chỗ trong code đều gắn nhãn `// Bài N`. Tìm nhanh bằng **Ctrl+Shift+F → "Bài 13"**.

## Chạy

```bash
cd D:\hi\CrudApiPractice\ProductApi
dotnet run
```

- Lần chạy đầu tự tạo database `CrudApiPractice` trong `(localdb)\MSSQLLocalDB` và áp dụng 3 migration kèm dữ liệu mẫu.
- Swagger: http://localhost:5080/swagger. Bấm **Authorize** rồi dán `accessToken` lấy từ `POST /api/auth/login`.
- Hoặc mở [ProductApi/ProductApi.http](ProductApi/ProductApi.http) và bấm **Send Request** (cần extension *REST Client*). Mỗi request đã ghi chú bài tương ứng.
- Tài khoản demo: `admin/admin123` (Admin), `staff1/staff123`, `staff2/staff123` (Staff).
- Muốn reset dữ liệu: `dotnet ef database drop -f` rồi `dotnet run` lại.

## Cấu trúc

```
CrudApiPractice.sln
├── README.md                       ← bạn đang đọc
├── THEORY.md                       ← giải thích + các bài "đọc code chỉ lỗi" (17, 19, 20, 25–28, 31, 33, 34, 36–38, 40)
├── Bai39_ThietKe_API_DatLenh.md    ← bài 39: thiết kế API đặt lệnh chứng khoán
└── ProductApi/
    ├── Program.cs                  ← DI, JWT, policy, rate limit, middleware pipeline
    ├── Entities/                   ← Product, Category, Order, OrderItem
    ├── Data/                       ← AppDbContext, SeedData, Migrations/
    ├── Dtos/                       ← DTO, validation, mapping
    ├── Controllers/
    │   ├── BasicProductsController.cs  ← ⭐ Bài 3–10: CRUD thuần (phần hay phải livecode nhất)
    │   ├── ProductsController.cs       ← Bài 11–20, 24, 29, 30, 32, 40: bản nhiều tầng
    │   └── OtherControllers.cs         ← Auth, Categories, Orders, Reports, Admin
    ├── Services/  Repositories/  Middleware/  Authorization/  BackgroundJobs/  Exceptions/
    └── ProductApi.http             ← gọi thử từng bài
```

## Mục lục: bài nào ở đâu

| Bài | Nội dung | File |
|---|---|---|
| 1 | Entity + DbContext | `Entities/Product.cs`, `Data/AppDbContext.cs` |
| 2 | Đăng ký DbContext với DI | `Program.cs` |
| 3–8 | GET / GET id / POST / PUT / DELETE, async | `Controllers/BasicProductsController.cs` |
| 9–10 | DTO, validation | `Dtos/ProductDtos.cs` |
| 11 | Repository | `Repositories/ProductRepository.cs` |
| 12 | Service layer, chống trùng tên | `Services/ProductService.cs` |
| 13–14 | Phân trang, lọc, sắp xếp động | `ProductRepository.GetPagedAsync` |
| 15 | Quan hệ 1-n, Include | `Entities/Category.cs`, `ProductRepository.GetWithCategoryAsync` |
| 16 | AsNoTracking | `ProductRepository.cs` (so sánh `GetWithCategoryAsync` và `GetByIdAsync`) |
| 17 | Migration | `Data/Migrations/` + THEORY.md |
| 18 | Soft delete, query filter | `AppDbContext.cs`, `ProductService.DeleteAsync/RestoreAsync` |
| 19 | Mapping thủ công | `Dtos/ProductMappings.cs` |
| 20 | Status code 400–422 | `Exceptions/AppExceptions.cs`, `ProductService.cs`, THEORY.md |
| 21 | Global exception handler | `Middleware/GlobalExceptionMiddleware.cs` |
| 22 | Logging có cấu trúc | `ProductService.cs`, `OrderService.cs` |
| 23 | Transaction tạo đơn | `Services/OrderService.cs` |
| 24 | Concurrency, RowVersion | `Product.RowVersion`, `ProductService.UpdateAsync` |
| 25 | N+1 | THEORY.md |
| 26 | Unit of Work | `Repositories/UnitOfWork.cs`, `CategoryService.DeleteAsync` |
| 27–28 | Unit test, integration test | THEORY.md (code mẫu, không có project test) |
| 29 | JWT | `Services/TokenService.cs`, `Program.cs` |
| 30 | Role + policy "chỉ sửa của mình" | `Authorization/Policies.cs`, `ProductService.UpdateAsync` |
| 31 | IMemoryCache + invalidation | `Services/CategoryService.cs` |
| 32 | Rate limiting | `Program.cs`, `[EnableRateLimiting]` trong `ProductsController` |
| 33 | BackgroundService | `BackgroundJobs/OrderExpirationService.cs` |
| 34 | Bulk insert | `Services/BulkInsertService.cs` |
| 35 | Stored procedure, raw SQL | `Migrations/*_AddSalesReportProcedure.cs`, `Services/ReportService.cs` |
| 36 | Dapper | `ReportService.GetTopProductsAsync` |
| 37 | Code chặn thread | THEORY.md + `ReportsController.GetDashboard` |
| 38 | Lỗi DI lifetime | THEORY.md + `Program.cs` |
| 39 | Thiết kế API đặt lệnh | `Bai39_ThietKe_API_DatLenh.md` |
| 40 | Code review | THEORY.md + `ProductsController.Search` |

---

## ⏱️ Lộ trình ôn trong 2 tiếng

Trước khi bắt đầu, chạy `dotnet run` và mở `ProductApi.http`: **đọc code đến đâu, bấm gửi request đến đó**.

| Thời gian | Đọc | Mục tiêu |
|---|---|---|
| **0:00 – 0:35** | **Bài 1–10**: `Product.cs` → `AppDbContext.cs` → `Program.cs` (phần DbContext) → `BasicProductsController.cs` → `ProductDtos.cs` | **Gõ lại được** controller CRUD async trong 15 phút không cần nhìn. Đây là phần chắc chắn bị livecode |
| **0:35 – 0:55** | **Bài 11–16, 20**: `ProductRepository.cs` → `ProductService.cs` → `ProductsController.cs`, cùng bảng status code trong THEORY.md | Hiểu vì sao tách tầng, phân trang/lọc động bằng `IQueryable`, `Include`, `AsNoTracking`, 409 khác 422 |
| **0:55 – 1:20** | **Bẫy hay hỏi**: THEORY.md bài **25, 38, 37** → `OrderService.cs` (**23**) → `ProductService.UpdateAsync` (**24**) → `GlobalExceptionMiddleware.cs` (**21**) | Trả lời trơn tru: N+1, captive dependency, `.Result`, transaction, RowVersion |
| **1:20 – 1:35** | **Bài 39**: `Bai39_ThietKe_API_DatLenh.md` | Nói được 6 ý: endpoint, state machine, sức mua nguyên tử, khớp lệnh tuần tự theo key, idempotency key, outbox |
| **1:35 – 1:50** | **Bài 40**: đọc đoạn code lỗi trong THEORY.md, **tự liệt kê lỗi trước** rồi mới xem bảng | Bài tổng ôn, gom gần hết các lỗi ở trên |
| **1:50 – 2:00** | Đọc lướt **17, 18, 29, 30, 31** | Biết lệnh migration, soft delete, JWT, policy, cache |

**Bỏ qua nếu hết giờ:** 19, 22, 26, 27, 28, 32–36. Những bài này ít gặp ở livecode junior; nếu bị hỏi, nói được hướng làm là đủ (đều có trong THEORY.md).

### Checklist 5 phút trước khi vào phỏng vấn

- [ ] Viết được `CreatedAtAction(nameof(GetById), new { id }, dto)` và biết vì sao `Id` có sau `SaveChangesAsync`
- [ ] Trả lời được: vì sao DbContext là Scoped, và chuyện gì xảy ra nếu inject vào Singleton
- [ ] Nói được N+1 là gì, sửa bằng `Include` hoặc projection
- [ ] Phân biệt được 400 / 401 / 403 / 404 / 409 / 422
- [ ] Giải thích được `.Result` gây thread pool starvation thế nào
- [ ] Nêu được 3 cách chống race condition: UPDATE nguyên tử, RowVersion, unique constraint
