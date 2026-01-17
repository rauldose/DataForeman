using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Blazored.LocalStorage;
using DataForeman.Shared.DTOs;

namespace DataForeman.Web.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(string email, string password);
    Task<RefreshResponse?> RefreshAsync();
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
    Task<bool> IsAuthenticatedAsync();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private const string TokenKey = "authToken";
    private const string RefreshKey = "refreshToken";

    public AuthService(HttpClient http, ILocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", new { email, password });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResult>();
                if (result != null)
                {
                    await _localStorage.SetItemAsync(TokenKey, result.Token);
                    await _localStorage.SetItemAsync(RefreshKey, result.Refresh);
                    return new LoginResponse(result.Token, result.Refresh, result.Role);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
        }
        return null;
    }

    public async Task<RefreshResponse?> RefreshAsync()
    {
        try
        {
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshKey);
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            var response = await _http.PostAsJsonAsync("api/auth/refresh", new { refresh = refreshToken });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RefreshResult>();
                if (result != null)
                {
                    await _localStorage.SetItemAsync(TokenKey, result.Token);
                    await _localStorage.SetItemAsync(RefreshKey, result.Refresh);
                    return new RefreshResponse(result.Token, result.Refresh);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Refresh error: {ex.Message}");
        }
        return null;
    }

    public async Task LogoutAsync()
    {
        try
        {
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshKey);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _http.PostAsJsonAsync("api/auth/logout", new { refresh = refreshToken });
            }
        }
        catch
        {
            // Ignore logout errors
        }
        finally
        {
            await _localStorage.RemoveItemAsync(TokenKey);
            await _localStorage.RemoveItemAsync(RefreshKey);
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(TokenKey);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    private record LoginResult(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("refresh")] string Refresh,
        [property: JsonPropertyName("role")] string Role);
    private record RefreshResult(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("refresh")] string Refresh);
}
