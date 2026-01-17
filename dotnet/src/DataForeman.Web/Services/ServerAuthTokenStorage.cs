using Microsoft.AspNetCore.Http;

namespace DataForeman.Web.Services;

public class ServerAuthTokenStorage : IAuthTokenStorage
{
    private const string TokenKey = "AuthToken";
    private const string RefreshKey = "RefreshToken";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServerAuthTokenStorage(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<string?> GetTokenAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var token = session?.GetString(TokenKey);
        return Task.FromResult(token);
    }

    public Task<string?> GetRefreshTokenAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var token = session?.GetString(RefreshKey);
        return Task.FromResult(token);
    }

    public Task SetTokenAsync(string token)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.SetString(TokenKey, token);
        return Task.CompletedTask;
    }

    public Task SetRefreshTokenAsync(string refreshToken)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.SetString(RefreshKey, refreshToken);
        return Task.CompletedTask;
    }

    public Task ClearTokensAsync()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.Remove(TokenKey);
        session?.Remove(RefreshKey);
        return Task.CompletedTask;
    }
}
