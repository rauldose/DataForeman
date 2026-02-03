using DataForeman.App.Components;
using DataForeman.App.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<RealtimeDataService>();

var app = builder.Build();

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
