namespace DataForeman.Web.Services;

/// <summary>
/// Stores auth tokens in a scoped service for Blazor Server.
/// This simple implementation stores tokens per instance - in Blazor Server,
/// each circuit (user session) gets its own scoped service instance.
/// </summary>
public class ServerAuthTokenStorage : IAuthTokenStorage
{
    private string? _token;
    private string? _refreshToken;

    public Task<string?> GetTokenAsync()
    {
        return Task.FromResult(_token);
    }

    public Task<string?> GetRefreshTokenAsync()
    {
        return Task.FromResult(_refreshToken);
    }

    public Task SetTokenAsync(string token)
    {
        _token = token;
        return Task.CompletedTask;
    }

    public Task SetRefreshTokenAsync(string refreshToken)
    {
        _refreshToken = refreshToken;
        return Task.CompletedTask;
    }

    public Task ClearTokensAsync()
    {
        _token = null;
        _refreshToken = null;
        return Task.CompletedTask;
    }
}
