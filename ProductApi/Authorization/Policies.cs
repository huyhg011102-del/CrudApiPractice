using Microsoft.AspNetCore.Authorization;
using ProductApi.Entities;

namespace ProductApi.Authorization;

// Bài 30: Authorization theo role + policy tùy chỉnh.
//   - Ai cũng được đọc            → GET không gắn [Authorize]
//   - Staff (và Admin) tạo + sửa  → [Authorize(Policy = Policies.CanWrite)]
//   - Chỉ Admin được xóa          → [Authorize(Policy = Policies.CanDelete)]
//   - Chỉ sửa sản phẩm do mình tạo → policy SameCreator (kiểm tra trong ProductService.UpdateAsync)
public static class Policies
{
    public const string CanWrite = "CanWrite";
    public const string CanDelete = "CanDelete";
    public const string SameCreator = "SameCreator";
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
}

/// <summary>Requirement rỗng — chỉ là "nhãn" để gắn handler.</summary>
public class SameCreatorRequirement : IAuthorizationRequirement;

/// <summary>
/// Handler cho resource-based authorization: nhận cả user lẫn resource (Product) để quyết định.
/// Đăng ký Singleton được vì không phụ thuộc gì Scoped.
/// </summary>
public class SameCreatorHandler : AuthorizationHandler<SameCreatorRequirement, Product>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SameCreatorRequirement requirement, Product resource)
    {
        if (context.User.IsInRole(Roles.Admin) || resource.CreatedBy == context.User.Identity?.Name)
            context.Succeed(requirement);

        return Task.CompletedTask;    // không gọi Succeed = thất bại
    }
}
