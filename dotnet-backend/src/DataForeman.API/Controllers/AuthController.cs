using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DataForeman.Core.Entities;
using DataForeman.Infrastructure.Data;

namespace DataForeman.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DataForemanDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(DataForemanDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { error = "Email and password are required" });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower() && u.IsActive);

        if (user == null)
        {
            return Unauthorized(new { error = "Invalid email or password" });
        }

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid email or password" });
        }

        var (accessToken, refreshToken) = await GenerateTokens(user);

        // Store refresh token
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays())
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Email} logged in successfully", user.Email);

        return Ok(new
        {
            token = accessToken,
            refresh = refreshToken,
            user = new
            {
                id = user.Id,
                email = user.Email,
                displayName = user.DisplayName
            }
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrEmpty(request.Refresh))
        {
            return BadRequest(new { error = "Refresh token is required" });
        }

        var refreshTokenEntity = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.Refresh);

        if (refreshTokenEntity == null || !refreshTokenEntity.IsActive)
        {
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        var user = refreshTokenEntity.User;
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { error = "User not found or inactive" });
        }

        // Revoke old refresh token
        refreshTokenEntity.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var (accessToken, newRefreshToken) = await GenerateTokens(user);

        // Store new refresh token
        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays())
        };
        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            token = accessToken,
            refresh = newRefreshToken
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        if (!string.IsNullOrEmpty(request.Refresh))
        {
            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == request.Refresh);

            if (refreshToken != null)
            {
                refreshToken.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        return Ok(new { ok = true });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            sub = user.Id,
            email = user.Email,
            displayName = user.DisplayName
        });
    }

    [Authorize]
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid current password" });
        }

        user.PasswordHash = HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke all other refresh tokens
        var tokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { error = "Email and password are required" });
        }

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (existingUser != null)
        {
            return Conflict(new { error = "Email already registered" });
        }

        var user = new User
        {
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = HashPassword(request.Password),
            IsActive = true
        };

        _context.Users.Add(user);

        // Add default permissions
        var defaultFeatures = new[]
        {
            "dashboards", "flows", "chart_composer", "connectivity.devices",
            "connectivity.tags", "diagnostics"
        };

        foreach (var feature in defaultFeatures)
        {
            _context.UserPermissions.Add(new UserPermission
            {
                UserId = user.Id,
                Feature = feature,
                CanCreate = true,
                CanRead = true,
                CanUpdate = true,
                CanDelete = true
            });
        }

        await _context.SaveChangesAsync();

        var (accessToken, refreshToken) = await GenerateTokens(user);

        // Store refresh token
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            UserAgent = Request.Headers.UserAgent.ToString(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays())
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation("New user registered: {Email}", user.Email);

        return Ok(new
        {
            token = accessToken,
            refresh = refreshToken,
            user = new
            {
                id = user.Id,
                email = user.Email,
                displayName = user.DisplayName
            }
        });
    }

    private Task<(string accessToken, string refreshToken)> GenerateTokens(User user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "DataForeman_Default_Secret_Key_Change_In_Production_123!";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "DataForeman";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "DataForeman";
        var expirationMinutes = int.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "60");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("displayName", user.DisplayName ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        return Task.FromResult((accessToken, refreshToken));
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }

    private int GetRefreshTokenExpirationDays()
    {
        return int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
    }

    private Guid? GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string Refresh);
public record LogoutRequest(string? Refresh);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record RegisterRequest(string Email, string Password, string? DisplayName);
