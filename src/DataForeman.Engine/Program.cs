using DataForeman.Engine;
using DataForeman.Engine.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<MqttPublisher>();
builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddSingleton<PollEngine>();
builder.Services.AddSingleton<ConfigWatcher>();

// Add the worker
builder.Services.AddHostedService<Worker>();

// Configure logging
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();
await host.RunAsync();
