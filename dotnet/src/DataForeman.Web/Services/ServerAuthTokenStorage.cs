using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace DataForeman.Web.Services;

/// <summary>
/// Stores auth tokens using browser localStorage for Blazor Server.
/// Uses ProtectedLocalStorage to persist tokens across page refreshes.
/// </summary>
public class ServerAuthTokenStorage : IAuthTokenStorage
{
    private readonly ProtectedLocalStorage _localStorage;
    private const string TokenKey = "auth_token";
    private const string RefreshTokenKey = "auth_refresh_token";
    
    // In-memory cache to avoid repeated JS interop calls
    private string? _cachedToken;
    private string? _cachedRefreshToken;
    private bool _initialized;

    public ServerAuthTokenStorage(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<string?> GetTokenAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
        return _cachedToken;
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
        return _cachedRefreshToken;
    }

    public async Task SetTokenAsync(string token)
    {
        _cachedToken = token;
        try
        {
            await _localStorage.SetAsync(TokenKey, token);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected, token is still in memory cache
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerender
        }
    }

    public async Task SetRefreshTokenAsync(string refreshToken)
    {
        _cachedRefreshToken = refreshToken;
        try
        {
            await _localStorage.SetAsync(RefreshTokenKey, refreshToken);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected, token is still in memory cache
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerender
        }
    }

    public async Task ClearTokensAsync()
    {
        _cachedToken = null;
        _cachedRefreshToken = null;
        try
        {
            await _localStorage.DeleteAsync(TokenKey);
            await _localStorage.DeleteAsync(RefreshTokenKey);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected
        }
        catch (InvalidOperationException)
        {
            // JS interop not available
        }
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;
        
        try
        {
            var tokenResult = await _localStorage.GetAsync<string>(TokenKey);
            _cachedToken = tokenResult.Success ? tokenResult.Value : null;

            var refreshResult = await _localStorage.GetAsync<string>(RefreshTokenKey);
            _cachedRefreshToken = refreshResult.Success ? refreshResult.Value : null;
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerender
        }
        
        _initialized = true;
    }
}
