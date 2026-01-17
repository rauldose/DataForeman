using Microsoft.EntityFrameworkCore;
using DataForeman.Shared.Models;

namespace DataForeman.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(DataForemanDbContext db)
    {
        // Create database if it doesn't exist
        await db.Database.EnsureCreatedAsync();

        // Seed roles if not exists
        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                new Role { Name = "viewer" },
                new Role { Name = "admin" }
            );
            await db.SaveChangesAsync();
        }

        // Seed admin user if not exists
        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@example.com";
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "password";

        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var adminUser = new User
            {
                Email = adminEmail,
                DisplayName = "Administrator",
                IsActive = true
            };
            db.Users.Add(adminUser);
            await db.SaveChangesAsync();

            // Create auth identity with hashed password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
            var authIdentity = new AuthIdentity
            {
                UserId = adminUser.Id,
                Provider = "local",
                ProviderUserId = adminUser.Id.ToString(),
                SecretHash = passwordHash
            };
            db.AuthIdentities.Add(authIdentity);
            await db.SaveChangesAsync();

            // Assign admin role
            var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "admin");
            if (adminRole != null)
            {
                db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
                await db.SaveChangesAsync();
            }

            // Grant default permissions
            var defaultFeatures = new[]
            {
                "dashboards",
                "connectivity.devices",
                "connectivity.tags",
                "connectivity.poll_groups",
                "connectivity.units",
                "connectivity.internal_tags",
                "chart_composer",
                "diagnostics",
                "diagnostic.system",
                "diagnostic.capacity",
                "diagnostic.logs",
                "diagnostic.network",
                "jobs",
                "logs",
                "flows",
                "users",
                "permissions"
            };

            foreach (var feature in defaultFeatures)
            {
                db.UserPermissions.Add(new UserPermission
                {
                    UserId = adminUser.Id,
                    Feature = feature,
                    CanCreate = true,
                    CanRead = true,
                    CanUpdate = true,
                    CanDelete = true
                });
            }
            await db.SaveChangesAsync();
        }

        // Seed poll groups if not exists
        if (!await db.PollGroups.AnyAsync())
        {
            db.PollGroups.AddRange(
                new PollGroup { GroupId = 1, Name = "Ultra Fast", PollRateMs = 50, Description = "Critical real-time control (50ms)" },
                new PollGroup { GroupId = 2, Name = "Very Fast", PollRateMs = 100, Description = "High-speed monitoring (100ms)" },
                new PollGroup { GroupId = 3, Name = "Fast", PollRateMs = 250, Description = "Fast process control (250ms)" },
                new PollGroup { GroupId = 4, Name = "Normal", PollRateMs = 500, Description = "Standard monitoring (500ms)" },
                new PollGroup { GroupId = 5, Name = "Standard", PollRateMs = 1000, Description = "Default polling rate (1s)" },
                new PollGroup { GroupId = 6, Name = "Slow", PollRateMs = 2000, Description = "Slow changing values (2s)" },
                new PollGroup { GroupId = 7, Name = "Very Slow", PollRateMs = 5000, Description = "Infrequent updates (5s)" },
                new PollGroup { GroupId = 8, Name = "Diagnostic", PollRateMs = 10000, Description = "Equipment diagnostics (10s)" },
                new PollGroup { GroupId = 9, Name = "Minute", PollRateMs = 60000, Description = "Per minute polling (1min)" },
                new PollGroup { GroupId = 10, Name = "Custom", PollRateMs = 30000, Description = "Custom/flexible rate (30s)" }
            );
            await db.SaveChangesAsync();
        }

        // Seed units of measure if not exists
        if (!await db.UnitsOfMeasure.AnyAsync())
        {
            var units = new List<UnitOfMeasure>
            {
                // Temperature
                new() { Name = "Degrees Celsius", Symbol = "°C", Category = "Temperature" },
                new() { Name = "Degrees Fahrenheit", Symbol = "°F", Category = "Temperature" },
                new() { Name = "Kelvin", Symbol = "K", Category = "Temperature" },
                // Pressure
                new() { Name = "Pascal", Symbol = "Pa", Category = "Pressure" },
                new() { Name = "Kilopascal", Symbol = "kPa", Category = "Pressure" },
                new() { Name = "Bar", Symbol = "bar", Category = "Pressure" },
                new() { Name = "PSI", Symbol = "psi", Category = "Pressure" },
                // Flow
                new() { Name = "Liters per second", Symbol = "L/s", Category = "Flow" },
                new() { Name = "Liters per minute", Symbol = "L/min", Category = "Flow" },
                new() { Name = "Cubic meters per hour", Symbol = "m³/h", Category = "Flow" },
                new() { Name = "Gallons per minute", Symbol = "GPM", Category = "Flow" },
                // Level/Distance
                new() { Name = "Millimeter", Symbol = "mm", Category = "Level" },
                new() { Name = "Centimeter", Symbol = "cm", Category = "Level" },
                new() { Name = "Meter", Symbol = "m", Category = "Level" },
                new() { Name = "Percent", Symbol = "%", Category = "Level" },
                // Electrical
                new() { Name = "Volt", Symbol = "V", Category = "Electrical" },
                new() { Name = "Ampere", Symbol = "A", Category = "Electrical" },
                new() { Name = "Watt", Symbol = "W", Category = "Electrical" },
                new() { Name = "Kilowatt", Symbol = "kW", Category = "Electrical" },
                new() { Name = "Hertz", Symbol = "Hz", Category = "Electrical" },
                // Speed
                new() { Name = "Meters per second", Symbol = "m/s", Category = "Speed" },
                new() { Name = "RPM", Symbol = "rpm", Category = "Speed" },
                // Mass
                new() { Name = "Kilogram", Symbol = "kg", Category = "Mass" },
                // Volume
                new() { Name = "Liter", Symbol = "L", Category = "Volume" },
                new() { Name = "Cubic meter", Symbol = "m³", Category = "Volume" },
                // Time
                new() { Name = "Second", Symbol = "s", Category = "Time" },
                new() { Name = "Minute", Symbol = "min", Category = "Time" },
                new() { Name = "Hour", Symbol = "h", Category = "Time" },
                // Dimensionless
                new() { Name = "Count", Symbol = "count", Category = "Dimensionless" },
                new() { Name = "Boolean", Symbol = "bool", Category = "Dimensionless" },
                new() { Name = "Percentage", Symbol = "%", Category = "Dimensionless" }
            };

            db.UnitsOfMeasure.AddRange(units);
            await db.SaveChangesAsync();
        }
    }
}
