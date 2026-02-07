namespace DataForeman.Web.Services;

public interface IAuthTokenStorage
{
    Task<string?> GetTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SetTokenAsync(string token);
    Task SetRefreshTokenAsync(string refreshToken);
    Task ClearTokensAsync();
}
