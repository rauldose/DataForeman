using DataForeman.Engine;
using DataForeman.Engine.Services;

var builder = Host.CreateApplicationBuilder(args);

// Core services
builder.Services.AddSingleton<ConfigWatcher>();
builder.Services.AddSingleton<MqttPublisher>();
builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddSingleton<PollEngine>();

// Hosted services
builder.Services.AddHostedService<EngineWorker>();

var host = builder.Build();
host.Run();
