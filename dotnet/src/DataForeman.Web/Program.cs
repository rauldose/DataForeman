using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Blazored.LocalStorage;
using Syncfusion.Blazor;
using DataForeman.Web;
using DataForeman.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API base address
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Add Blazored LocalStorage for auth token storage
builder.Services.AddBlazoredLocalStorage();

// Add Syncfusion Blazor services
builder.Services.AddSyncfusionBlazor();

// Add application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
    provider => provider.GetRequiredService<AuthStateProvider>());
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
