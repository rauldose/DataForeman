using DataForeman.App.Components;
using DataForeman.App.Services;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Syncfusion Blazor services
builder.Services.AddSyncfusionBlazor();

builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<RealtimeDataService>();
builder.Services.AddSingleton<NodePluginRegistry>();

var app = builder.Build();

// Register Syncfusion license (optional - for licensed use)
var syncfusionKey = builder.Configuration["SyncfusionLicenseKey"];
if (!string.IsNullOrEmpty(syncfusionKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
}

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

app.Run();
