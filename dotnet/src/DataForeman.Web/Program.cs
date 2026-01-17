using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;
using Syncfusion.Blazor;
using DataForeman.Web;
using DataForeman.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Add Blazored LocalStorage for auth token storage (must be added before HttpClient)
builder.Services.AddBlazoredLocalStorage();

// Configure API base address with authorization handler
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
builder.Services.AddScoped<AuthorizationHandler>();
builder.Services.AddScoped(sp =>
{
    var localStorage = sp.GetRequiredService<ILocalStorageService>();
    var handler = new AuthorizationHandler(localStorage)
    {
        InnerHandler = new HttpClientHandler()
    };
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

// Add Syncfusion Blazor services
builder.Services.AddSyncfusionBlazor();

// Add application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
    provider => provider.GetRequiredService<AuthStateProvider>());
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
