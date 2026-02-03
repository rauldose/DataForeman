using DataForeman.App.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
