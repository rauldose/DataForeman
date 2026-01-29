using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop;

namespace DataForeman.BlazorUI.Services;

/// <summary>
/// Custom authentication state provider for modular monolith.
/// Uses direct database access via DataService instead of HTTP calls.
/// </summary>
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly DataService _dataService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomAuthStateProvider> _logger;
    
    private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
    private const string TokenKey = "df_token";
    private const string RefreshTokenKey = "df_refresh_token";
    private const string UserKey = "df_user";
    
    // Flag to track if JS interop is available (after first render)
    private bool _jsInteropAvailable = false;
    
    public CustomAuthStateProvider(
        IJSRuntime jsRuntime, 
        DataService dataService,
        IConfiguration configuration,
        ILogger<CustomAuthStateProvider> logger)
    {
        _jsRuntime = jsRuntime;
        _dataService = dataService;
        _configuration = configuration;
        _logger = logger;
    }
    
    /// <summary>
    /// Mark JS interop as available. Should be called from OnAfterRenderAsync.
    /// </summary>
    public void MarkJsInteropAvailable()
    {
        _jsInteropAvailable = true;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // During prerendering, JS interop is not available - return anonymous
            if (!_jsInteropAvailable)
            {
                return new AuthenticationState(_anonymous);
            }
            
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationState(_anonymous);
            }

            var claims = ParseClaimsFromJwt(token);
            
            // Check if token is expired
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim != null && long.TryParse(expClaim.Value, out var expValue))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(expValue).UtcDateTime;
                if (expDate < DateTime.UtcNow)
                {
                    // Token expired - clear and return anonymous
                    await ClearTokensAsync();
                    return new AuthenticationState(_anonymous);
                }
            }
            
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            
            return new AuthenticationState(user);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
        {
            // JS interop not available during prerendering - this is expected
            _logger.LogDebug("JS interop not available during prerendering");
            return new AuthenticationState(_anonymous);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authentication state");
            return new AuthenticationState(_anonymous);
        }
    }

    /// <summary>
    /// Login user using direct database validation.
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            // Validate credentials directly against database
            var user = await _dataService.ValidateCredentialsAsync(email, password);
            
            if (user == null)
            {
                return new LoginResult { Success = false, Error = "Invalid email or password" };
            }

            // Generate JWT token
            var token = GenerateJwtToken(user.Id, user.Email, user.DisplayName);
            
            var userInfo = new UserInfo
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName
            };
            
            await SetTokenAsync(token);
            await SetUserAsync(userInfo);
            
            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var principal = new ClaimsPrincipal(identity);
            
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
            
            return new LoginResult
            {
                Success = true,
                Token = token,
                User = userInfo
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return new LoginResult { Success = false, Error = "Login failed. Please try again." };
        }
    }

    /// <summary>
    /// Generate a JWT token for the authenticated user.
    /// </summary>
    private string GenerateJwtToken(Guid userId, string email, string? displayName)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "DataForemanSecretKey_ChangeInProduction_MinLength32Chars!";
        
        // Ensure minimum key length for HMAC-SHA256 (32 bytes / 256 bits)
        if (jwtKey.Length < 32)
        {
            throw new InvalidOperationException("JWT key must be at least 32 characters for secure HMAC-SHA256 signing.");
        }
        
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "DataForeman";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "DataForeman";
        
        if (!int.TryParse(_configuration["Jwt:ExpirationHours"], out var expirationHours))
        {
            expirationHours = 24;
        }
        
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.UniqueName, displayName ?? email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("role", "User") // TODO: Retrieve role from database when RBAC is implemented
        };
        
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: credentials
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Logout current user.
    /// </summary>
    public async Task LogoutAsync()
    {
        await ClearTokensAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    /// <summary>
    /// Get current user info from storage.
    /// </summary>
    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        if (!_jsInteropAvailable)
        {
            return null;
        }
        
        try
        {
            var userJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", UserKey);
            if (string.IsNullOrEmpty(userJson))
            {
                return null;
            }
            return System.Text.Json.JsonSerializer.Deserialize<UserInfo>(userJson);
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetTokenAsync()
    {
        if (!_jsInteropAvailable)
        {
            return null;
        }
        
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get token from localStorage");
            return null;
        }
    }

    private async Task SetTokenAsync(string token)
    {
        if (!_jsInteropAvailable) return;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
    }

    private async Task SetUserAsync(UserInfo user)
    {
        if (!_jsInteropAvailable) return;
        var json = System.Text.Json.JsonSerializer.Serialize(user);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UserKey, json);
    }

    private async Task ClearTokensAsync()
    {
        if (!_jsInteropAvailable) return;
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserKey);
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                // Invalid JWT format - must have 3 parts
                return claims;
            }
            
            var payload = parts[1];
            
            // Pad base64 string
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            
            var jsonBytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            var keyValuePairs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
            
            if (keyValuePairs != null)
            {
                foreach (var kvp in keyValuePairs)
                {
                    var claimType = kvp.Key switch
                    {
                        "sub" => ClaimTypes.NameIdentifier,
                        "email" => ClaimTypes.Email,
                        "unique_name" => ClaimTypes.Name,
                        "role" => ClaimTypes.Role,
                        _ => kvp.Key
                    };
                    
                    claims.Add(new Claim(claimType, kvp.Value?.ToString() ?? ""));
                }
            }
        }
        catch (FormatException)
        {
            // Invalid base64 - return empty claims
        }
        catch (System.Text.Json.JsonException)
        {
            // Invalid JSON - return empty claims
        }
        
        return claims;
    }
}

/// <summary>
/// Result of a login attempt.
/// </summary>
public class LoginResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public UserInfo? User { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// User information.
/// </summary>
public class UserInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
}
