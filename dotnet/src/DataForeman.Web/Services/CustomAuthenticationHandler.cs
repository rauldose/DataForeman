using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace DataForeman.Web.Services;

public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAuthTokenStorage _tokenStorage;
    private readonly IConfiguration _configuration;

    public CustomAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAuthTokenStorage tokenStorage,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _tokenStorage = tokenStorage;
        _configuration = configuration;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            
            // Get JWT settings from configuration (same as API)
            var jwtKey = _configuration["Jwt:Key"] ?? "DataForemanSecretKey12345678901234567890";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "DataForeman";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "DataForeman";
            
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
            var ticket = new AuthenticationTicket(principal, "Custom");
            
            return AuthenticateResult.Success(ticket);
        }
        catch (SecurityTokenExpiredException)
        {
            return AuthenticateResult.Fail("Token has expired");
        }
        catch (SecurityTokenException ex)
        {
            return AuthenticateResult.Fail($"Token validation failed: {ex.Message}");
        }
        catch (Exception)
        {
            return AuthenticateResult.Fail("Invalid token");
        }
    }
}
