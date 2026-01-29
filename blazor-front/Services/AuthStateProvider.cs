using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace DataForeman.BlazorUI.Services;

/// <summary>
/// Custom authentication state provider for JWT-based authentication.
/// Manages auth state and persists tokens in browser storage.
/// </summary>
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ApiService _apiService;
    private readonly ILogger<CustomAuthStateProvider> _logger;
    
    private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
    private const string TokenKey = "df_token";
    private const string RefreshTokenKey = "df_refresh_token";
    private const string UserKey = "df_user";
    
    // Flag to track if JS interop is available (after first render)
    private bool _jsInteropAvailable = false;
    
    public CustomAuthStateProvider(IJSRuntime jsRuntime, ApiService apiService, ILogger<CustomAuthStateProvider> logger)
    {
        _jsRuntime = jsRuntime;
        _apiService = apiService;
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
                    // Token expired, try to refresh
                    var refreshed = await TryRefreshTokenAsync();
                    if (!refreshed)
                    {
                        return new AuthenticationState(_anonymous);
                    }
                    
                    // Get new token and claims
                    token = await GetTokenAsync();
                    if (string.IsNullOrEmpty(token))
                    {
                        return new AuthenticationState(_anonymous);
                    }
                    claims = ParseClaimsFromJwt(token);
                }
            }
            
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);
            
            // Update API service with token
            _apiService.SetAuthToken(token);
            
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
    /// Login user and store tokens.
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var result = await _apiService.LoginAsync(email, password);
            
            if (result.Success && !string.IsNullOrEmpty(result.Token))
            {
                await SetTokenAsync(result.Token);
                await SetRefreshTokenAsync(result.RefreshToken ?? "");
                
                if (result.User != null)
                {
                    await SetUserAsync(result.User);
                }
                
                _apiService.SetAuthToken(result.Token);
                
                var claims = ParseClaimsFromJwt(result.Token);
                var identity = new ClaimsIdentity(claims, "jwt");
                var user = new ClaimsPrincipal(identity);
                
                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return new LoginResult { Success = false, Error = "Login failed. Please try again." };
        }
    }

    /// <summary>
    /// Logout current user.
    /// </summary>
    public async Task LogoutAsync()
    {
        try
        {
            var refreshToken = await GetRefreshTokenAsync();
            await _apiService.LogoutAsync(refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout API call failed");
        }
        
        await ClearTokensAsync();
        _apiService.ClearAuthToken();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    /// <summary>
    /// Try to refresh the access token.
    /// </summary>
    public async Task<bool> TryRefreshTokenAsync()
    {
        try
        {
            var refreshToken = await GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
            {
                return false;
            }

            var result = await _apiService.RefreshTokenAsync(refreshToken);
            
            if (result.Success && !string.IsNullOrEmpty(result.Token))
            {
                await SetTokenAsync(result.Token);
                await SetRefreshTokenAsync(result.RefreshToken ?? "");
                _apiService.SetAuthToken(result.Token);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return false;
        }
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

    private async Task<string?> GetRefreshTokenAsync()
    {
        if (!_jsInteropAvailable)
        {
            return null;
        }
        
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", RefreshTokenKey);
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get refresh token from localStorage");
            return null;
        }
    }

    private async Task SetTokenAsync(string token)
    {
        if (!_jsInteropAvailable) return;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
    }

    private async Task SetRefreshTokenAsync(string refreshToken)
    {
        if (!_jsInteropAvailable) return;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);
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
