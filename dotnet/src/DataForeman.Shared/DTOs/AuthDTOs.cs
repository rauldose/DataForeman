namespace DataForeman.Shared.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, string Refresh, string Role);

public record RefreshRequest(string Refresh);

public record RefreshResponse(string Token, string Refresh);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserResponse(Guid Id, string Email, string? DisplayName, bool IsActive);

public record CreateUserRequest(string Email, string? DisplayName, bool? IsActive);

public record UpdateUserRequest(string? DisplayName, bool? IsActive);

public record SetPasswordRequest(string Password);

public record SessionResponse(int Id, Guid Jti, DateTime CreatedAt, DateTime ExpiresAt, DateTime? RevokedAt, Guid? ReplacedByJti, string? UserAgent, string? Ip);
