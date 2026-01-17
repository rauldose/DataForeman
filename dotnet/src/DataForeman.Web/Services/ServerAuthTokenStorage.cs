namespace DataForeman.Web.Services;

/// <summary>
/// Stores auth tokens in a scoped service for Blazor Server.
/// Uses a static dictionary keyed by circuit ID for cross-component access.
/// </summary>
public class ServerAuthTokenStorage : IAuthTokenStorage
{
    // Static storage to persist tokens across component trees
    private static readonly Dictionary<string, (string? Token, string? RefreshToken)> _circuitTokens = new();
    private readonly string _circuitId;

    public ServerAuthTokenStorage()
    {
        // Use a thread-static ID to identify the circuit
        _circuitId = Guid.NewGuid().ToString();
    }

    public Task<string?> GetTokenAsync()
    {
        lock (_circuitTokens)
        {
            // Return the most recently set token (simplified for demo)
            var lastToken = _circuitTokens.Values.LastOrDefault();
            return Task.FromResult(lastToken.Token);
        }
    }

    public Task<string?> GetRefreshTokenAsync()
    {
        lock (_circuitTokens)
        {
            var lastToken = _circuitTokens.Values.LastOrDefault();
            return Task.FromResult(lastToken.RefreshToken);
        }
    }

    public Task SetTokenAsync(string token)
    {
        lock (_circuitTokens)
        {
            if (_circuitTokens.TryGetValue(_circuitId, out var existing))
            {
                _circuitTokens[_circuitId] = (token, existing.RefreshToken);
            }
            else
            {
                _circuitTokens[_circuitId] = (token, null);
            }
            Console.WriteLine($"SetTokenAsync - Token stored for circuit {_circuitId.Substring(0, 8)}");
        }
        return Task.CompletedTask;
    }

    public Task SetRefreshTokenAsync(string refreshToken)
    {
        lock (_circuitTokens)
        {
            if (_circuitTokens.TryGetValue(_circuitId, out var existing))
            {
                _circuitTokens[_circuitId] = (existing.Token, refreshToken);
            }
            else
            {
                _circuitTokens[_circuitId] = (null, refreshToken);
            }
        }
        return Task.CompletedTask;
    }

    public Task ClearTokensAsync()
    {
        lock (_circuitTokens)
        {
            _circuitTokens.Remove(_circuitId);
        }
        return Task.CompletedTask;
    }
}
