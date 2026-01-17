using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace DataForeman.Web.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly IAuthTokenStorage _tokenStorage;
    private readonly HttpClient _http;
    private string? _cachedToken;
    private ClaimsPrincipal? _cachedPrincipal;

    public AuthStateProvider(IAuthTokenStorage tokenStorage, HttpClient http)
    {
        _tokenStorage = tokenStorage;
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // First check cached token (set during login)
        string? token = _cachedToken;
        
        // Try to get token from storage if not cached
        if (string.IsNullOrEmpty(token))
        {
            try
            {
                token = await _tokenStorage.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _cachedToken = token;
                }
            }
            catch (InvalidOperationException)
            {
                // JS interop not available during prerender - return cached principal if available
                if (_cachedPrincipal != null)
                {
                    return new AuthenticationState(_cachedPrincipal);
                }
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }
        
        if (string.IsNullOrEmpty(token))
        {
            _cachedPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(_cachedPrincipal);
        }

        // Validate token is not expired
        var claims = ParseClaimsFromJwt(token);
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
        if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
        {
            var expTime = DateTimeOffset.FromUnixTimeSeconds(exp);
            if (expTime < DateTimeOffset.UtcNow)
            {
                // Token expired - clear it
                _cachedToken = null;
                _cachedPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
                await _tokenStorage.ClearTokensAsync();
                return new AuthenticationState(_cachedPrincipal);
            }
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        _cachedPrincipal = new ClaimsPrincipal(identity);

        // Set authorization header for HTTP client
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return new AuthenticationState(_cachedPrincipal);
    }

    public void NotifyUserAuthentication(string token)
    {
        // Cache the token to ensure it's available immediately
        _cachedToken = token;
        
        var claims = ParseClaimsFromJwt(token);
        _cachedPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        var authState = Task.FromResult(new AuthenticationState(_cachedPrincipal));
        NotifyAuthenticationStateChanged(authState);
    }

    public void NotifyUserLogout()
    {
        _cachedToken = null;
        _cachedPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var authState = Task.FromResult(new AuthenticationState(_cachedPrincipal));
        NotifyAuthenticationStateChanged(authState);
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        try
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs == null)
                return claims;

            foreach (var kvp in keyValuePairs)
            {
                if (kvp.Value is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in element.EnumerateArray())
                        {
                            claims.Add(new Claim(kvp.Key, item.ToString()));
                        }
                    }
                    else
                    {
                        claims.Add(new Claim(kvp.Key, element.ToString()));
                    }
                }
                else
                {
                    claims.Add(new Claim(kvp.Key, kvp.Value?.ToString() ?? ""));
                }
            }

            // Map standard JWT claims to ASP.NET Core claims
            var subClaim = claims.FirstOrDefault(c => c.Type == "sub");
            if (subClaim != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, subClaim.Value));
            }

            var roleClaim = claims.FirstOrDefault(c => c.Type == "role");
            if (roleClaim != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, roleClaim.Value));
            }

            var emailClaim = claims.FirstOrDefault(c => c.Type == "email");
            if (emailClaim != null)
            {
                claims.Add(new Claim(ClaimTypes.Email, emailClaim.Value));
                claims.Add(new Claim(ClaimTypes.Name, emailClaim.Value));
            }
        }
        catch
        {
            // Invalid JWT - return empty claims
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
