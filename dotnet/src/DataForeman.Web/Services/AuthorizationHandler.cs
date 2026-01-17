using System.Net.Http.Headers;
using Blazored.LocalStorage;

namespace DataForeman.Web.Services;

public class AuthorizationHandler : DelegatingHandler
{
    private static readonly HashSet<string> AnonymousEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/refresh"
    };

    private readonly ILocalStorageService _localStorage;

    public AuthorizationHandler(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        
        if (!AnonymousEndpoints.Contains(path))
        {
            try
            {
                var token = await _localStorage.GetItemAsync<string>("authToken");
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch (InvalidOperationException)
            {
                // LocalStorage might not be available during pre-rendering
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
