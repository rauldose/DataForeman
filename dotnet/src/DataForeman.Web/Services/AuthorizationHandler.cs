using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace DataForeman.Web.Services;

public class AuthorizationHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;

    public AuthorizationHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Don't add auth header to login/register endpoints
        var path = request.RequestUri?.PathAndQuery ?? "";
        if (!path.Contains("/api/auth/login") && !path.Contains("/api/auth/register"))
        {
            try
            {
                var token = await _localStorage.GetItemAsync<string>("authToken");
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch
            {
                // LocalStorage might not be available during pre-rendering
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
