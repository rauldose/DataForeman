using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DataForeman.Api.Data;
using DataForeman.Shared.Models;
using DataForeman.Shared.DTOs;

namespace DataForeman.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string email, string password, string? userAgent, string? ip);
    Task<RefreshResponse?> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken, Guid? userId);
    string GenerateAccessToken(User user, Guid jti);
    string GenerateRefreshToken();
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<bool> ValidateRefreshTokenAsync(string refreshToken, Guid userId);
}

public class AuthService : IAuthService
{
    private readonly DataForemanDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(DataForemanDbContext db, IConfiguration config, ILogger<AuthService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password, string? userAgent, string? ip)
    {
        var user = await _db.Users
            .Include(u => u.AuthIdentities)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent or inactive user: {Email}", email);
            return null;
        }

        var authIdentity = user.AuthIdentities?.FirstOrDefault(ai => ai.Provider == "local");
        if (authIdentity == null || string.IsNullOrEmpty(authIdentity.SecretHash))
        {
            _logger.LogWarning("No local auth identity found for user: {UserId}", user.Id);
            return null;
        }

        // Check if locked
        if (authIdentity.LockedUntil != null && authIdentity.LockedUntil > DateTime.UtcNow)
        {
            _logger.LogWarning("Login attempt for locked user: {UserId}", user.Id);
            return null;
        }

        // Verify password using BCrypt
        if (!BCrypt.Net.BCrypt.Verify(password, authIdentity.SecretHash))
        {
            // Increment failed attempts
            authIdentity.FailedAttempts++;
            if (authIdentity.FailedAttempts >= 5)
            {
                authIdentity.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            }
            await _db.SaveChangesAsync();
            
            _logger.LogWarning("Failed login attempt for user: {UserId}, attempts: {Attempts}", user.Id, authIdentity.FailedAttempts);
            return null;
        }

        // Reset failed attempts
        authIdentity.FailedAttempts = 0;
        authIdentity.LockedUntil = null;
        authIdentity.LastLoginAt = DateTime.UtcNow;

        // Generate tokens
        var jti = Guid.NewGuid();
        var refreshToken = GenerateRefreshToken();
        var accessToken = GenerateAccessToken(user, jti);

        // Create session
        var session = new Session
        {
            UserId = user.Id,
            Jti = jti,
            RefreshHash = HashRefreshToken(refreshToken),
            UserAgent = userAgent,
            Ip = ip != null ? System.Net.IPAddress.TryParse(ip, out var ipAddr) ? ipAddr : null : null,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            LastActivityAt = DateTime.UtcNow
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        var role = user.UserRoles?.FirstOrDefault()?.Role?.Name ?? "viewer";
        return new LoginResponse(accessToken, refreshToken, role);
    }

    public async Task<RefreshResponse?> RefreshAsync(string refreshToken)
    {
        var refreshHash = HashRefreshToken(refreshToken);
        var session = await _db.Sessions
            .Include(s => s.User)
                .ThenInclude(u => u!.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(s => s.RefreshHash == refreshHash && s.RevokedAt == null);

        if (session == null || session.ExpiresAt < DateTime.UtcNow || session.User == null)
        {
            return null;
        }

        // Revoke old session and create new one
        session.RevokedAt = DateTime.UtcNow;

        var newJti = Guid.NewGuid();
        var newRefreshToken = GenerateRefreshToken();
        var newAccessToken = GenerateAccessToken(session.User, newJti);

        session.ReplacedByJti = newJti;

        var newSession = new Session
        {
            UserId = session.UserId,
            Jti = newJti,
            RefreshHash = HashRefreshToken(newRefreshToken),
            UserAgent = session.UserAgent,
            Ip = session.Ip,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            LastActivityAt = DateTime.UtcNow
        };

        _db.Sessions.Add(newSession);
        await _db.SaveChangesAsync();

        return new RefreshResponse(newAccessToken, newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken, Guid? userId)
    {
        var refreshHash = HashRefreshToken(refreshToken);
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.RefreshHash == refreshHash);
        
        if (session != null)
        {
            session.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public string GenerateAccessToken(User user, Guid jti)
    {
        var jwtSecret = _config["Jwt:Secret"] ?? "dev-secret-change-me-in-production-please";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var role = user.UserRoles?.FirstOrDefault()?.Role?.Name ?? "viewer";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, jti.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim("role", role)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "dataforeman",
            audience: _config["Jwt:Audience"] ?? "dataforeman",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<bool> ValidateRefreshTokenAsync(string refreshToken, Guid userId)
    {
        var refreshHash = HashRefreshToken(refreshToken);
        return await _db.Sessions.AnyAsync(s => 
            s.RefreshHash == refreshHash && 
            s.UserId == userId && 
            s.RevokedAt == null && 
            s.ExpiresAt > DateTime.UtcNow);
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }
}
