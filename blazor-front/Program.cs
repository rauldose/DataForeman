using DataForeman.BlazorUI.Components;
using DataForeman.BlazorUI.Services;
using DataForeman.Infrastructure.Data;
using DataForeman.Auth;
using DataForeman.RedisStreams;
using DataForeman.FlowEngine;
using DataForeman.Drivers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Blazor;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// MODULAR MONOLITH ARCHITECTURE
// All backend services integrated directly into Blazor Server
// ============================================================

// Add Razor components with interactive server mode
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Syncfusion Blazor services
builder.Services.AddSyncfusionBlazor();

// Add Controllers for API endpoints (optional - for external API access)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// DATA LAYER - SQLite via EF Core
// ============================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=dataforeman.db";
builder.Services.AddDbContext<DataForemanDbContext>(options =>
    options.UseSqlite(connectionString));

// ============================================================
// AUTHENTICATION - JWT with RBAC
// ============================================================
builder.Services.AddDataForemanAuth(builder.Configuration);

// ============================================================
// REDIS STREAMS - Optional telemetry messaging
// ============================================================
builder.Services.AddRedisStreams(builder.Configuration);

// ============================================================
// FLOW ENGINE - Node-based processing
// ============================================================
builder.Services.AddFlowEngine(builder.Configuration);

// ============================================================
// PROTOCOL DRIVERS - OPC UA, EtherNet/IP, S7
// ============================================================
builder.Services.AddSingleton<IDriverFactory, DriverFactory>();

// ============================================================
// BLAZOR SERVICES
// ============================================================

// Direct data service (replaces HTTP API calls)
builder.Services.AddScoped<DataService>();

// App State service for global state management
builder.Services.AddScoped<AppStateService>();

// Node Plugin Registry (singleton for shared node definitions)
builder.Services.AddSingleton<NodePluginRegistry>();

// Simulator Service (singleton for simulated connections/tags)
builder.Services.AddSingleton<SimulatorService>();

// Blazor Authentication state provider
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => 
    provider.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Register Syncfusion license
var syncfusionKey = builder.Configuration["SyncfusionLicenseKey"];
if (!string.IsNullOrEmpty(syncfusionKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DataForeman API v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Map API controllers
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck");

// Map Blazor components
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataForemanDbContext>();
    db.Database.EnsureCreated();
}

app.Run();
