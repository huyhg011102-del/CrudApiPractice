using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProductApi.Authorization;
using ProductApi.Dtos;

namespace ProductApi.Services;

// Bài 29: JWT authentication.
public class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";         // HS256 cần khóa ≥ 32 byte. Thực tế để trong user-secrets / Key Vault, không commit.
    public int ExpiryMinutes { get; set; } = 60;
}

public interface ITokenService
{
    LoginResponse? Login(LoginRequest request);
}

public class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    // Tài khoản demo để luyện tập. Thực tế: bảng Users, mật khẩu băm bằng PasswordHasher / BCrypt.
    private static readonly (string User, string Password, string Role)[] DemoUsers =
    [
        ("admin",  "admin123", Roles.Admin),
        ("staff1", "staff123", Roles.Staff),
        ("staff2", "staff123", Roles.Staff),
    ];

    public LoginResponse? Login(LoginRequest request)
    {
        var user = DemoUsers.FirstOrDefault(u => u.User == request.UserName && u.Password == request.Password);
        if (user == default) return null;

        var opt = options.Value;
        var expires = DateTime.UtcNow.AddMinutes(opt.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = opt.Issuer,
            Audience = opt.Audience,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.User),
                new Claim("name", user.User),
                new Claim("role", user.Role),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Key)),
                SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new LoginResponse(token, expires, user.Role);
    }
}
