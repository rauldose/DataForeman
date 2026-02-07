using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace DataForeman.App.Services;

/// <summary>
/// Custom authentication state provider for Blazor Server.
/// Uses JSON file-based user storage with BCrypt password hashing.
/// </summary>
public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedLocalStorage _localStorage;
    private readonly UserService _userService;
    private readonly ILogger<AuthStateProvider> _logger;
    private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public AuthStateProvider(
        ProtectedLocalStorage localStorage,
        UserService userService,
        ILogger<AuthStateProvider> logger)
    {
        _localStorage = localStorage;
        _userService = userService;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>("df_user_id");
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                var user = _userService.GetUserById(result.Value);
                if (user != null && user.IsActive)
                {
                    var claims = BuildClaims(user);
                    var identity = new ClaimsIdentity(claims, "DataForeman");
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve auth state (expected during prerendering)");
        }

        return new AuthenticationState(_anonymous);
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var user = _userService.ValidateCredentials(email, password);
        if (user == null) return false;

        await _localStorage.SetAsync("df_user_id", user.Id);
        var claims = BuildClaims(user);
        var identity = new ClaimsIdentity(claims, "DataForeman");
        var principal = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
        return true;
    }

    public async Task LogoutAsync()
    {
        await _localStorage.DeleteAsync("df_user_id");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
    }

    public async Task<AppUser?> GetCurrentUserAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>("df_user_id");
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                return _userService.GetUserById(result.Value);
            }
        }
        catch
        {
            // Expected during prerendering
        }
        return null;
    }

    private static List<Claim> BuildClaims(AppUser user)
    {
        return new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName ?? user.Email),
            new("IsActive", user.IsActive.ToString())
        };
    }
}
