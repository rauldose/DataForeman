using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using DataForeman.Core.Entities;

namespace DataForeman.Auth;

/// <summary>
/// Service for JWT token generation and validation.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generate an access token for a user.
    /// </summary>
    string GenerateAccessToken(User user, IEnumerable<string>? roles = null, IEnumerable<string>? permissions = null);

    /// <summary>
    /// Generate a refresh token.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Validate a token and return the principal.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// Get user ID from token.
    /// </summary>
    Guid? GetUserIdFromToken(string token);

    /// <summary>
    /// Get access token expiration time.
    /// </summary>
    DateTime GetAccessTokenExpiration();

    /// <summary>
    /// Get refresh token expiration time.
    /// </summary>
    DateTime GetRefreshTokenExpiration();
}

/// <summary>
/// Default JWT token service implementation.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _securityKey;
    private readonly SigningCredentials _credentials;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    /// <summary>
    /// Initializes a new instance of the JWT token service.
    /// </summary>
    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        _credentials = new SigningCredentials(_securityKey, SecurityAlgorithms.HmacSha256);
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <inheritdoc />
    public string GenerateAccessToken(User user, IEnumerable<string>? roles = null, IEnumerable<string>? permissions = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("displayName", user.DisplayName ?? "")
        };

        // Add role claims
        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        // Add permission claims
        if (permissions != null)
        {
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: GetAccessTokenExpiration(),
            signingCredentials: _credentials);

        return _tokenHandler.WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <inheritdoc />
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = _securityKey
            };

            return _tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public Guid? GetUserIdFromToken(string token)
    {
        var principal = ValidateToken(token);
        if (principal == null) return null;

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }

    /// <inheritdoc />
    public DateTime GetAccessTokenExpiration()
    {
        return DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);
    }

    /// <inheritdoc />
    public DateTime GetRefreshTokenExpiration()
    {
        return DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);
    }
}
