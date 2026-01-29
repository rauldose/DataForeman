using DataForeman.Core.Entities;

namespace DataForeman.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, string? ipAddress = null, string? userAgent = null);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, string? ipAddress = null, string? userAgent = null);
    Task LogoutAsync(string refreshToken);
    Task<User> RegisterAsync(string email, string password, string? displayName = null);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task ResetPasswordAsync(Guid userId, string newPassword);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserInfo? User { get; set; }
}

public class UserInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsAdmin { get; set; }
}

public interface IJwtService
{
    string GenerateAccessToken(User user, IEnumerable<string>? permissions = null);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    Guid? GetUserIdFromToken(string token);
}

// Note: This needs System.Security.Claims namespace
public class ClaimsPrincipal
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public IEnumerable<string> Permissions { get; set; } = new List<string>();
}
