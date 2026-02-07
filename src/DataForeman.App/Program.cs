using DataForeman.App.Components;
using DataForeman.App.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Syncfusion Blazor services
builder.Services.AddSyncfusionBlazor();

// Authentication & Authorization
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<UserService>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddScoped<AppStateService>();

builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<RealtimeDataService>();
builder.Services.AddSingleton<NodePluginRegistry>();
builder.Services.AddSingleton<HistoryService>();
builder.Services.AddSingleton<ScriptValidationService>();

var app = builder.Build();

// Register Syncfusion license (optional - for licensed use)
 
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(@"Ngo9BigBOggjHTQxAR8/V1JGaF5cXGpCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH1dcXRcQ2lcUUR0XkZWYEs=");
 
// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Start MQTT service
var mqttService = app.Services.GetRequiredService<MqttService>();
_ = mqttService.ConnectAsync();

// Load configuration
var configService = app.Services.GetRequiredService<ConfigService>();
await configService.LoadAllAsync();

// Register saved subflows with NodePluginRegistry
var nodeRegistry = app.Services.GetRequiredService<NodePluginRegistry>();
foreach (var subflow in configService.Subflows)
{
    nodeRegistry.RegisterSubflow(subflow);
}

app.Run();
