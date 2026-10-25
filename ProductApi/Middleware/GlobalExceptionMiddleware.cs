using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProductApi.Exceptions;

namespace ProductApi.Middleware;

// Bài 21: Middleware bắt MỌI exception chưa xử lý → log → trả JSON thống nhất (ProblemDetails, RFC 7807).
// Không lộ stack trace ở production: chỉ thêm "detail" khi đang Development.
//
// Phải đăng ký SỚM NHẤT trong pipeline (app.UseMiddleware<...>() đầu tiên) để bọc được mọi middleware phía sau.
// Cách khác từ .NET 8: implement IExceptionHandler + builder.Services.AddExceptionHandler<T>() + app.UseExceptionHandler().
public class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)   // đã gửi header rồi thì không sửa response được nữa
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            AppException app => (app.StatusCode, app.Message),
            // Bài 24: bị người khác sửa trước
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                "Dữ liệu đã bị người khác thay đổi. Hãy tải lại rồi thử lại."),
            // Vi phạm unique index / khóa ngoại...
            //  - qua SaveChanges: EF bọc thành DbUpdateException
            //  - qua ExecuteDelete/ExecuteUpdate/raw SQL: nhận thẳng SqlException (547 = khóa ngoại, 2601/2627 = trùng unique)
            DbUpdateException or SqlException { Number: 547 or 2601 or 2627 } => (StatusCodes.Status409Conflict,
                "Không lưu được do ràng buộc dữ liệu (trùng giá trị hoặc đang được tham chiếu)."),
            OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "Client đã hủy request."),
            _ => (StatusCodes.Status500InternalServerError, "Đã có lỗi xảy ra phía server."),
        };

        // Bài 22: đúng level — 5xx là lỗi của mình (Error, kèm exception), 4xx là lỗi phía client (Warning)
        if (status >= 500)
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogWarning("{ExceptionType} on {Method} {Path}: {Message}",
                ex.GetType().Name, context.Request.Method, context.Request.Path, ex.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = context.Request.Path,
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;     // để đối chiếu với log

        if (env.IsDevelopment() && status >= 500)
            problem.Detail = ex.ToString();                           // chỉ Development mới thấy stack trace

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
