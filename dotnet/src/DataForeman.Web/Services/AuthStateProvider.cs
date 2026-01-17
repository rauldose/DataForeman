using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace DataForeman.Web.Services;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly IAuthTokenStorage _tokenStorage;
    private readonly HttpClient _http;

    public AuthStateProvider(IAuthTokenStorage tokenStorage, HttpClient http)
    {
        _tokenStorage = tokenStorage;
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStorage.GetTokenAsync();
        
        if (string.IsNullOrEmpty(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        // Set authorization header for HTTP client
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return new AuthenticationState(user);
    }

    public void NotifyUserAuthentication(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        var authState = Task.FromResult(new AuthenticationState(authenticatedUser));
        NotifyAuthenticationStateChanged(authState);
    }

    public void NotifyUserLogout()
    {
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var authState = Task.FromResult(new AuthenticationState(anonymousUser));
        NotifyAuthenticationStateChanged(authState);
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
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
