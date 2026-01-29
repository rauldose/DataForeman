using Microsoft.EntityFrameworkCore;
using DataForeman.Infrastructure.Data;
using DataForeman.Auth;
using DataForeman.RedisStreams;
using DataForeman.FlowEngine;
using DataForeman.Drivers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure SQLite database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=dataforeman.db";
builder.Services.AddDbContext<DataForemanDbContext>(options =>
    options.UseSqlite(connectionString));

// Configure JWT authentication using the Auth module
builder.Services.AddDataForemanAuth(builder.Configuration);

// Add Redis Streams services (optional - will gracefully handle if Redis is not available)
builder.Services.AddRedisStreams(builder.Configuration);

// Add Flow Engine services
builder.Services.AddFlowEngine(builder.Configuration);

// Add Driver Factory
builder.Services.AddSingleton<IDriverFactory, DriverFactory>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5000",
                "http://localhost:5001",
                "https://localhost:5001",
                "http://localhost:5174" // React dev server for compatibility
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

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

app.UseCors("AllowBlazorApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck");

// Ensure database is created and optionally seed historical data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataForemanDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    db.Database.EnsureCreated();
    
    // Seed historical data if enabled via configuration
    var seedHistoricalData = builder.Configuration.GetValue<bool>("SeedHistoricalData", false);
    if (seedHistoricalData)
    {
        var seeder = new HistoricalDataSeeder(db, scope.ServiceProvider.GetRequiredService<ILogger<HistoricalDataSeeder>>());
        var hasData = await seeder.HasHistoricalDataAsync();
        
        if (!hasData)
        {
            logger.LogInformation("Seeding historical tag value data...");
            await seeder.SeedHistoricalDataAsync(daysOfHistory: 7, samplesPerHour: 60);
            logger.LogInformation("Historical data seeding completed");
        }
        else
        {
            logger.LogInformation("Historical data already exists, skipping seeding");
        }
    }
}

app.Run();
