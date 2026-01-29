using DataForeman.Auth;
using DataForeman.Core.Entities;
using Microsoft.Extensions.Options;

namespace DataForeman.API.Tests;

/// <summary>
/// Unit tests for authentication services.
/// </summary>
public class AuthServiceTests
{
    private readonly JwtTokenService _tokenService;

    public AuthServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Key = "TestKeyForUnitTestsThatNeedsToBeAtLeast32BytesLong!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        });

        _tokenService = new JwtTokenService(options);
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateAccessToken_WithRoles_IncludesRolesInToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            DisplayName = "Admin User"
        };
        var roles = new[] { Roles.Admin, Roles.User };

        // Act
        var token = _tokenService.GenerateAccessToken(user, roles);
        var principal = _tokenService.ValidateToken(token);

        // Assert
        Assert.NotNull(principal);
        Assert.True(principal.IsInRole(Roles.Admin));
        Assert.True(principal.IsInRole(Roles.User));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokens()
    {
        // Act
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        // Assert
        Assert.NotEqual(token1, token2);
        Assert.NotEmpty(token1);
        Assert.NotEmpty(token2);
    }

    [Fact]
    public void ValidateToken_WithValidToken_ReturnsPrincipal()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        var token = _tokenService.GenerateAccessToken(user);

        // Act
        var principal = _tokenService.ValidateToken(token);

        // Assert
        Assert.NotNull(principal);
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ReturnsNull()
    {
        // Act
        var principal = _tokenService.ValidateToken("invalid.token.here");

        // Assert
        Assert.Null(principal);
    }

    [Fact]
    public void GetUserIdFromToken_ReturnsCorrectUserId()
    {
        // Arrange
        var expectedUserId = Guid.NewGuid();
        var user = new User
        {
            Id = expectedUserId,
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        var token = _tokenService.GenerateAccessToken(user);

        // Act
        var userId = _tokenService.GetUserIdFromToken(token);

        // Assert
        Assert.NotNull(userId);
        Assert.Equal(expectedUserId, userId);
    }

    [Fact]
    public void PasswordService_HashAndVerify_Works()
    {
        // Arrange
        var passwordService = new PasswordService();
        var password = "MySecurePassword123!";

        // Act
        var hash = passwordService.HashPassword(password);
        var isValid = passwordService.VerifyPassword(password, hash);
        var isInvalid = passwordService.VerifyPassword("WrongPassword", hash);

        // Assert
        Assert.True(isValid);
        Assert.False(isInvalid);
    }

    [Fact]
    public void PasswordService_HashesAreDifferent()
    {
        // Arrange
        var passwordService = new PasswordService();
        var password = "MySecurePassword123!";

        // Act
        var hash1 = passwordService.HashPassword(password);
        var hash2 = passwordService.HashPassword(password);

        // Assert - BCrypt uses salts, so same password should give different hashes
        Assert.NotEqual(hash1, hash2);
        // But both should verify correctly
        Assert.True(passwordService.VerifyPassword(password, hash1));
        Assert.True(passwordService.VerifyPassword(password, hash2));
    }
}
