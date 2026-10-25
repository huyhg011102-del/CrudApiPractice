namespace ProductApi.Exceptions;

// Bài 20 + 21: Service ném exception "có nghĩa nghiệp vụ", middleware đổi sang status code tương ứng.
// Nhờ vậy service không phụ thuộc HTTP, controller không phải try/catch.
public abstract class AppException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>404 — không tìm thấy tài nguyên.</summary>
public class NotFoundException(string message) : AppException(message, StatusCodes.Status404NotFound);

/// <summary>409 — xung đột trạng thái: trùng tên, hết hàng, bị người khác sửa trước...</summary>
public class ConflictException(string message) : AppException(message, StatusCodes.Status409Conflict);

/// <summary>422 — request đúng cú pháp nhưng vi phạm quy tắc nghiệp vụ.</summary>
public class BusinessRuleException(string message) : AppException(message, StatusCodes.Status422UnprocessableEntity);

/// <summary>403 — đã đăng nhập nhưng không có quyền với tài nguyên này.</summary>
public class ForbiddenException(string message) : AppException(message, StatusCodes.Status403Forbidden);
