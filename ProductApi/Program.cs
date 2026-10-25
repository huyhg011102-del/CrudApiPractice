using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ProductApi.Authorization;
using ProductApi.BackgroundJobs;
using ProductApi.Controllers;
using ProductApi.Data;
using ProductApi.Middleware;
using ProductApi.Repositories;
using ProductApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Bài 2: Đăng ký DbContext, connection string lấy từ appsettings.json ("ConnectionStrings:Default").
// AddDbContext mặc định đăng ký vòng đời SCOPED: mỗi HTTP request một instance.
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers();

// ---------------------------------------------------------------------------
// Bài 11, 12, 26: Repository, Service, Unit of Work — đều SCOPED vì phụ thuộc DbContext (Scoped).
// Bài 38: KHÔNG được đăng ký Singleton cho class phụ thuộc DbContext (xem THEORY.md).
//         Ở môi trường Development, ASP.NET Core bật ValidateScopes → đăng ký sai sẽ báo lỗi ngay lúc khởi động.
// ---------------------------------------------------------------------------
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IOrderService, OrderService>();          // Bài 38: Scoped, không phải Singleton
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<BulkInsertService>();

// Bài 37: client gọi "API ngoài" (giả lập). Không có state → Singleton được.
builder.Services.AddSingleton<IExchangeRateClient, FakeExchangeRateClient>();

// Bài 31: IMemoryCache — Singleton (dùng chung cho mọi request)
builder.Services.AddMemoryCache();

// Bài 33: job chạy nền
builder.Services.AddHostedService<OrderExpirationService>();

// ---------------------------------------------------------------------------
// Bài 29: JWT authentication — validate issuer, audience, lifetime, signing key.
// ---------------------------------------------------------------------------
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<ITokenService, TokenService>();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Mặc định JwtBearer đổi tên claim ngắn sang URI dài ("role" → ".../claims/role", "sub" → ".../nameidentifier").
        // Tắt đi để giữ nguyên tên claim như trong token → RoleClaimType = "role" khớp.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),           // mặc định 5 phút — token hết hạn vẫn dùng được thêm 5 phút
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",                         // User.Identity.Name đọc từ claim "name"
            RoleClaimType = "role",                         // [Authorize(Roles=...)] / IsInRole đọc từ claim "role"
        };
    });

// ---------------------------------------------------------------------------
// Bài 30: Authorization theo role + policy tùy chỉnh
// ---------------------------------------------------------------------------
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.CanWrite, p => p.RequireRole(Roles.Admin, Roles.Staff))
    .AddPolicy(Policies.CanDelete, p => p.RequireRole(Roles.Admin))
    .AddPolicy(Policies.SameCreator, p => p.AddRequirements(new SameCreatorRequirement()));
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SameCreatorHandler>();

// ---------------------------------------------------------------------------
// Bài 32: Rate limiting có sẵn từ .NET 7 — mỗi client tối đa 100 request/phút cho nhóm endpoint đọc.
// "Client" = user đã đăng nhập, nếu chưa đăng nhập thì theo IP.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Read, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// Swagger UI có nút "Authorize" để dán JWT
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CRUD API Practice", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Gọi POST /api/auth/login, copy accessToken dán vào đây (không cần chữ Bearer).",
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Tự apply migration khi chạy ở máy dev (tạo DB CrudApiPractice trong LocalDB nếu chưa có).
    // Production: chạy migration riêng trong pipeline deploy (dotnet ef database update / migration bundle), không làm lúc app khởi động.
    using (var scope = app.Services.CreateScope())
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

    app.UseSwagger();
    app.UseSwaggerUI();
}

// Bài 21: middleware bắt exception đứng ĐẦU pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();     // Bài 29: "anh là ai" — phải trước UseAuthorization
app.UseAuthorization();      // Bài 30: "anh được làm gì"
app.UseRateLimiter();        // Bài 32: sau Authentication để phân vùng theo user

app.MapControllers();

app.Run();
