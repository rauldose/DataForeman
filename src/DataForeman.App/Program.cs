using DataForeman.App.Services;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// Register Syncfusion license (get free community license from syncfusion.com)
var licenseKey = builder.Configuration.GetValue<string>("SyncfusionLicenseKey");
if (!string.IsNullOrEmpty(licenseKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
}

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Syncfusion Blazor services
builder.Services.AddSyncfusionBlazor();

// Configuration service (JSON file storage)
builder.Services.AddSingleton<ConfigService>();

// MQTT client service
builder.Services.AddSingleton<MqttService>();

// Real-time data service
builder.Services.AddSingleton<RealtimeDataService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<DataForeman.App.Components.App>()
    .AddInteractiveServerRenderMode();

// Initialize services
var configService = app.Services.GetRequiredService<ConfigService>();
await configService.InitializeAsync();

var mqttService = app.Services.GetRequiredService<MqttService>();
await mqttService.ConnectAsync();

app.Run();
