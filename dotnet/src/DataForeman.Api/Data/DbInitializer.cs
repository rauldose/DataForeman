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

        // Seed sample connections (devices) if not exists
        if (!await db.Connections.AnyAsync())
        {
            var connections = new List<Connection>
            {
                new()
                {
                    Name = "Modbus PLC - Line 1",
                    Type = "modbus-tcp",
                    Enabled = true,
                    ConfigData = "{\"host\":\"192.168.1.10\",\"port\":502,\"unitId\":1}",
                    MaxTagsPerGroup = 100,
                    MaxConcurrentConnections = 1
                },
                new()
                {
                    Name = "OPC-UA Server",
                    Type = "opc-ua",
                    Enabled = true,
                    ConfigData = "{\"endpoint\":\"opc.tcp://192.168.1.20:4840\"}",
                    MaxTagsPerGroup = 500,
                    MaxConcurrentConnections = 3
                },
                new()
                {
                    Name = "MQTT Broker",
                    Type = "mqtt",
                    Enabled = true,
                    ConfigData = "{\"broker\":\"192.168.1.30\",\"port\":1883}",
                    MaxTagsPerGroup = 1000,
                    MaxConcurrentConnections = 5
                },
                new()
                {
                    Name = "Siemens S7 - Tank Farm",
                    Type = "s7",
                    Enabled = false,
                    ConfigData = "{\"host\":\"192.168.1.40\",\"rack\":0,\"slot\":1}",
                    MaxTagsPerGroup = 200,
                    MaxConcurrentConnections = 1
                }
            };

            db.Connections.AddRange(connections);
            await db.SaveChangesAsync();

            // Get the admin user for ownership (use same email variable as user creation)
            var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
            var celsiusUnit = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Symbol == "°C");
            var kpaUnit = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Symbol == "kPa");
            var lpmUnit = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Symbol == "L/min");
            var percentUnit = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Symbol == "%");
            var pollGroup = await db.PollGroups.FirstOrDefaultAsync(p => p.GroupId == 5); // Standard (1s)

            // Seed sample tags
            var modbusConnection = connections.First(c => c.Name.Contains("Modbus"));
            var opcuaConnection = connections.First(c => c.Name.Contains("OPC"));
            var defaultPollGroupId = pollGroup?.GroupId ?? 5;

            var tags = new List<TagMetadata>
            {
                new()
                {
                    ConnectionId = modbusConnection.Id,
                    DriverType = "modbus-tcp",
                    TagPath = "PLC1/Temperature/Reactor1",
                    TagName = "Reactor 1 Temperature",
                    IsSubscribed = true,
                    Status = "active",
                    PollGroupId = defaultPollGroupId,
                    DataType = "float",
                    UnitId = celsiusUnit?.Id,
                    Description = "Main reactor temperature sensor"
                },
                new()
                {
                    ConnectionId = modbusConnection.Id,
                    DriverType = "modbus-tcp",
                    TagPath = "PLC1/Pressure/Tank1",
                    TagName = "Tank 1 Pressure",
                    IsSubscribed = true,
                    Status = "active",
                    PollGroupId = defaultPollGroupId,
                    DataType = "float",
                    UnitId = kpaUnit?.Id,
                    Description = "Storage tank pressure sensor"
                },
                new()
                {
                    ConnectionId = modbusConnection.Id,
                    DriverType = "modbus-tcp",
                    TagPath = "PLC1/Flow/Pump1",
                    TagName = "Pump 1 Flow Rate",
                    IsSubscribed = true,
                    Status = "active",
                    PollGroupId = defaultPollGroupId,
                    DataType = "float",
                    UnitId = lpmUnit?.Id,
                    Description = "Main pump flow sensor"
                },
                new()
                {
                    ConnectionId = modbusConnection.Id,
                    DriverType = "modbus-tcp",
                    TagPath = "PLC1/Level/Tank1",
                    TagName = "Tank 1 Level",
                    IsSubscribed = true,
                    Status = "active",
                    PollGroupId = defaultPollGroupId,
                    DataType = "float",
                    UnitId = percentUnit?.Id,
                    Description = "Tank level sensor"
                },
                new()
                {
                    ConnectionId = opcuaConnection.Id,
                    DriverType = "opc-ua",
                    TagPath = "ns=2;s=Line1/Motor1/Speed",
                    TagName = "Motor 1 Speed",
                    IsSubscribed = true,
                    Status = "active",
                    PollGroupId = defaultPollGroupId,
                    DataType = "float",
                    Description = "Conveyor motor speed"
                },
                new()
                {
                    ConnectionId = opcuaConnection.Id,
                    DriverType = "opc-ua",
                    TagPath = "ns=2;s=Line1/Motor1/Current",
                    TagName = "Motor 1 Current",
                    IsSubscribed = true,
                    Status = "active",
                    PollGroupId = defaultPollGroupId,
                    DataType = "float",
                    Description = "Conveyor motor current draw"
                }
            };

            db.TagMetadata.AddRange(tags);
            await db.SaveChangesAsync();

            // Seed sample flows
            if (adminUser != null)
            {
                var flows = new List<Flow>
                {
                    new()
                    {
                        Name = "Temperature Alarm",
                        Description = "Monitors reactor temperature and triggers alarm when exceeding threshold",
                        OwnerUserId = adminUser.Id,
                        Deployed = true,
                        ExecutionMode = "continuous",
                        ScanRateMs = 1000,
                        Definition = "{\"nodes\":[{\"id\":\"start\",\"type\":\"trigger-tag-change\",\"position\":{\"x\":100,\"y\":200}},{\"id\":\"compare\",\"type\":\"logic-compare\",\"position\":{\"x\":300,\"y\":200}},{\"id\":\"alert\",\"type\":\"tag-output\",\"position\":{\"x\":500,\"y\":200}}],\"edges\":[{\"source\":\"start\",\"target\":\"compare\"},{\"source\":\"compare\",\"target\":\"alert\"}]}"
                    },
                    new()
                    {
                        Name = "Tank Level Control",
                        Description = "Automatic tank level control with pump start/stop logic",
                        OwnerUserId = adminUser.Id,
                        Deployed = false,
                        ExecutionMode = "continuous",
                        ScanRateMs = 500,
                        Definition = "{}"
                    },
                    new()
                    {
                        Name = "Data Logger",
                        Description = "Logs process data to historian every minute",
                        OwnerUserId = adminUser.Id,
                        Deployed = true,
                        ExecutionMode = "manual",
                        Definition = "{}"
                    }
                };

                db.Flows.AddRange(flows);
                await db.SaveChangesAsync();

                // Seed sample charts
                var charts = new List<ChartConfig>
                {
                    new()
                    {
                        Name = "Reactor Overview",
                        Description = "Real-time reactor temperature and pressure monitoring",
                        UserId = adminUser.Id,
                        ChartType = "line",
                        TimeMode = "rolling",
                        TimeDuration = 3600000, // 1 hour in ms
                        LiveEnabled = true,
                        Options = "{\"tags\":[\"PLC1/Temperature/Reactor1\",\"PLC1/Pressure/Tank1\"],\"colors\":[\"#2196f3\",\"#4caf50\"]}"
                    },
                    new()
                    {
                        Name = "Pump Performance",
                        Description = "Flow rate and tank level correlation",
                        UserId = adminUser.Id,
                        ChartType = "area",
                        TimeMode = "rolling",
                        TimeDuration = 1800000, // 30 min in ms
                        LiveEnabled = true,
                        Options = "{\"tags\":[\"PLC1/Flow/Pump1\",\"PLC1/Level/Tank1\"],\"colors\":[\"#ff9800\",\"#9c27b0\"]}"
                    },
                    new()
                    {
                        Name = "Motor Analysis",
                        Description = "Motor speed and current draw analysis",
                        UserId = adminUser.Id,
                        ChartType = "spline",
                        TimeMode = "fixed",
                        LiveEnabled = false,
                        Options = "{\"tags\":[\"ns=2;s=Line1/Motor1/Speed\",\"ns=2;s=Line1/Motor1/Current\"],\"colors\":[\"#e91e63\",\"#00bcd4\"]}"
                    }
                };

                db.ChartConfigs.AddRange(charts);
                await db.SaveChangesAsync();

                // Seed sample dashboards
                var dashboards = new List<Dashboard>
                {
                    new()
                    {
                        Name = "Production Overview",
                        Description = "Main production floor monitoring dashboard",
                        UserId = adminUser.Id,
                        IsShared = true,
                        Layout = "{\"widgets\":[{\"type\":\"chart\",\"x\":0,\"y\":0,\"w\":6,\"h\":4},{\"type\":\"gauge\",\"x\":6,\"y\":0,\"w\":3,\"h\":2}]}",
                        Options = "{\"refreshRate\":5000}"
                    },
                    new()
                    {
                        Name = "Alarms & Events",
                        Description = "Active alarms and recent events",
                        UserId = adminUser.Id,
                        IsShared = true,
                        Layout = "{\"widgets\":[{\"type\":\"alarmList\",\"x\":0,\"y\":0,\"w\":12,\"h\":6}]}",
                        Options = "{\"refreshRate\":1000}"
                    }
                };

                db.Dashboards.AddRange(dashboards);
                await db.SaveChangesAsync();
            }
        }
    }
}
