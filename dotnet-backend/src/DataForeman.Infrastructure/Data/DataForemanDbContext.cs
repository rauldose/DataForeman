using Microsoft.EntityFrameworkCore;
using DataForeman.Core.Entities;

namespace DataForeman.Infrastructure.Data;

public class DataForemanDbContext : DbContext
{
    public DataForemanDbContext(DbContextOptions<DataForemanDbContext> options) : base(options)
    {
    }

    // Authentication & Authorization
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Dashboards
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<DashboardFolder> DashboardFolders => Set<DashboardFolder>();

    // Flows
    public DbSet<Flow> Flows => Set<Flow>();
    public DbSet<FlowFolder> FlowFolders => Set<FlowFolder>();
    public DbSet<FlowExecution> FlowExecutions => Set<FlowExecution>();
    public DbSet<FlowExecutionLog> FlowExecutionLogs => Set<FlowExecutionLog>();
    public DbSet<FlowSession> FlowSessions => Set<FlowSession>();

    // Charts
    public DbSet<ChartConfig> ChartConfigs => Set<ChartConfig>();
    public DbSet<ChartFolder> ChartFolders => Set<ChartFolder>();
    public DbSet<ChartSeries> ChartSeries => Set<ChartSeries>();
    public DbSet<ChartAxis> ChartAxes => Set<ChartAxis>();

    // Connectivity
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<TagMetadata> TagMetadata => Set<TagMetadata>();
    public DbSet<PollGroup> PollGroups => Set<PollGroup>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<TagValue> TagValues => Set<TagValue>();

    // System
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<NodeLibrary> NodeLibraries => Set<NodeLibrary>();
    public DbSet<NodeCategory> NodeCategories => Set<NodeCategory>();
    public DbSet<NodeSection> NodeSections => Set<NodeSection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(256);
        });

        // UserPermission configuration (composite key)
        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.Feature });
            entity.Property(e => e.Feature).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Permissions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // RefreshToken configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Dashboard configuration
        modelBuilder.Entity<Dashboard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Dashboards)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Folder)
                  .WithMany(f => f.Dashboards)
                  .HasForeignKey(e => e.FolderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // DashboardFolder configuration
        modelBuilder.Entity<DashboardFolder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.HasOne(e => e.ParentFolder)
                  .WithMany(f => f.ChildFolders)
                  .HasForeignKey(e => e.ParentFolderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Flow configuration
        modelBuilder.Entity<Flow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ExecutionMode).HasMaxLength(20);
            entity.HasOne(e => e.Owner)
                  .WithMany(u => u.Flows)
                  .HasForeignKey(e => e.OwnerUserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Folder)
                  .WithMany(f => f.Flows)
                  .HasForeignKey(e => e.FolderId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ResourceChart)
                  .WithMany()
                  .HasForeignKey(e => e.ResourceChartId)
                  .OnDelete(DeleteBehavior.SetNull);
            // Template flow relationship
            entity.HasOne(e => e.TemplateFlow)
                  .WithMany(f => f.InstantiatedFlows)
                  .HasForeignKey(e => e.TemplateFlowId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.IsTemplate);
        });

        // FlowFolder configuration
        modelBuilder.Entity<FlowFolder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.HasOne(e => e.ParentFolder)
                  .WithMany(f => f.ChildFolders)
                  .HasForeignKey(e => e.ParentFolderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // FlowExecution configuration
        modelBuilder.Entity<FlowExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasOne(e => e.Flow)
                  .WithMany(f => f.Executions)
                  .HasForeignKey(e => e.FlowId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.FlowId, e.StartedAt });
        });

        // FlowExecutionLog configuration
        modelBuilder.Entity<FlowExecutionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LogLevel).HasMaxLength(10);
            entity.HasOne(e => e.Execution)
                  .WithMany(ex => ex.Logs)
                  .HasForeignKey(e => e.ExecutionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.FlowId, e.Timestamp });
        });

        // FlowSession configuration
        modelBuilder.Entity<FlowSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasOne(e => e.Flow)
                  .WithMany(f => f.Sessions)
                  .HasForeignKey(e => e.FlowId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.FlowId, e.StartedAt });
        });

        // ChartConfig configuration
        modelBuilder.Entity<ChartConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ChartType).HasMaxLength(50);
            entity.Property(e => e.TimeMode).HasMaxLength(20);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Charts)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Folder)
                  .WithMany(f => f.Charts)
                  .HasForeignKey(e => e.FolderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ChartFolder configuration
        modelBuilder.Entity<ChartFolder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.HasOne(e => e.ParentFolder)
                  .WithMany(f => f.ChildFolders)
                  .HasForeignKey(e => e.ParentFolderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ChartSeries configuration
        modelBuilder.Entity<ChartSeries>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.SeriesType).HasMaxLength(20);
            entity.HasOne(e => e.Chart)
                  .WithMany(c => c.Series)
                  .HasForeignKey(e => e.ChartId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tag)
                  .WithMany()
                  .HasForeignKey(e => e.TagId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Axis)
                  .WithMany(a => a.Series)
                  .HasForeignKey(e => e.AxisIndex)
                  .HasPrincipalKey(a => a.AxisIndex)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ChartId, e.DisplayOrder });
        });

        // ChartAxis configuration
        modelBuilder.Entity<ChartAxis>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AxisType).HasMaxLength(10);
            entity.Property(e => e.Position).HasMaxLength(20);
            entity.Property(e => e.Label).HasMaxLength(255);
            entity.Property(e => e.GridLineStyle).HasMaxLength(20);
            entity.HasOne(e => e.Chart)
                  .WithMany(c => c.Axes)
                  .HasForeignKey(e => e.ChartId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.ChartId, e.AxisIndex }).IsUnique();
        });

        // Connection configuration
        modelBuilder.Entity<Connection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
        });

        // TagMetadata configuration
        modelBuilder.Entity<TagMetadata>(entity =>
        {
            entity.HasKey(e => e.TagId);
            entity.Property(e => e.TagId).ValueGeneratedOnAdd();
            entity.Property(e => e.DriverType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.TagPath).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.OnChangeDeadbandType).HasMaxLength(20);
            entity.HasIndex(e => new { e.ConnectionId, e.TagPath, e.DriverType }).IsUnique();
            entity.HasOne(e => e.Connection)
                  .WithMany(c => c.Tags)
                  .HasForeignKey(e => e.ConnectionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PollGroup)
                  .WithMany(pg => pg.Tags)
                  .HasForeignKey(e => e.PollGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Unit)
                  .WithMany(u => u.Tags)
                  .HasForeignKey(e => e.UnitId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // PollGroup configuration
        modelBuilder.Entity<PollGroup>(entity =>
        {
            entity.HasKey(e => e.GroupId);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
        });

        // UnitOfMeasure configuration
        modelBuilder.Entity<UnitOfMeasure>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(50);
        });

        // TagValue configuration
        modelBuilder.Entity<TagValue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TagId, e.Timestamp });
        });

        // Job configuration
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
        });

        // AuditEvent configuration
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Outcome).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.Timestamp);
        });

        // SystemSetting configuration
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(255);
        });

        // NodeLibrary configuration
        modelBuilder.Entity<NodeLibrary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LibraryId).IsUnique();
            entity.Property(e => e.LibraryId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(50);
        });

        // NodeCategory configuration
        modelBuilder.Entity<NodeCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryKey);
            entity.Property(e => e.CategoryKey).HasMaxLength(50);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Icon).HasMaxLength(50);
        });

        // NodeSection configuration
        modelBuilder.Entity<NodeSection>(entity =>
        {
            entity.HasKey(e => new { e.CategoryKey, e.SectionKey });
            entity.Property(e => e.CategoryKey).HasMaxLength(50);
            entity.Property(e => e.SectionKey).HasMaxLength(50);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Sections)
                  .HasForeignKey(e => e.CategoryKey)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed data
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // Seed admin user with default credentials (admin@local / admin123)
        var adminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        // BCrypt hash of "admin123"
        var adminPasswordHash = "$2a$11$wBN7FfKoDqY3lCIQVXoqve7Z20u8XDJrysq/69WOFDCfUYhW43Lku";
        modelBuilder.Entity<User>().HasData(
            new User 
            { 
                Id = adminUserId, 
                Email = "admin@local", 
                DisplayName = "Administrator",
                PasswordHash = adminPasswordHash, // Password: admin123
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed PollGroups
        modelBuilder.Entity<PollGroup>().HasData(
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

        // Seed Units of Measure
        modelBuilder.Entity<UnitOfMeasure>().HasData(
            // Temperature
            new UnitOfMeasure { Id = 1, Name = "Degrees Celsius", Symbol = "°C", Category = "Temperature" },
            new UnitOfMeasure { Id = 2, Name = "Degrees Fahrenheit", Symbol = "°F", Category = "Temperature" },
            new UnitOfMeasure { Id = 3, Name = "Kelvin", Symbol = "K", Category = "Temperature" },
            // Pressure
            new UnitOfMeasure { Id = 4, Name = "Pascal", Symbol = "Pa", Category = "Pressure" },
            new UnitOfMeasure { Id = 5, Name = "Kilopascal", Symbol = "kPa", Category = "Pressure" },
            new UnitOfMeasure { Id = 6, Name = "Bar", Symbol = "bar", Category = "Pressure" },
            new UnitOfMeasure { Id = 7, Name = "PSI", Symbol = "psi", Category = "Pressure" },
            // Electrical
            new UnitOfMeasure { Id = 8, Name = "Volt", Symbol = "V", Category = "Electrical" },
            new UnitOfMeasure { Id = 9, Name = "Ampere", Symbol = "A", Category = "Electrical" },
            new UnitOfMeasure { Id = 10, Name = "Watt", Symbol = "W", Category = "Electrical" },
            new UnitOfMeasure { Id = 11, Name = "Kilowatt", Symbol = "kW", Category = "Electrical" },
            // Flow
            new UnitOfMeasure { Id = 12, Name = "Liters per minute", Symbol = "L/min", Category = "Flow" },
            new UnitOfMeasure { Id = 13, Name = "Cubic meters per hour", Symbol = "m³/h", Category = "Flow" },
            // Level
            new UnitOfMeasure { Id = 14, Name = "Meter", Symbol = "m", Category = "Level" },
            new UnitOfMeasure { Id = 15, Name = "Percent", Symbol = "%", Category = "Level" },
            // Speed
            new UnitOfMeasure { Id = 16, Name = "RPM", Symbol = "rpm", Category = "Speed" },
            new UnitOfMeasure { Id = 17, Name = "Meters per second", Symbol = "m/s", Category = "Speed" },
            // Mass
            new UnitOfMeasure { Id = 18, Name = "Kilogram", Symbol = "kg", Category = "Mass" },
            // Time
            new UnitOfMeasure { Id = 19, Name = "Second", Symbol = "s", Category = "Time" },
            new UnitOfMeasure { Id = 20, Name = "Minute", Symbol = "min", Category = "Time" },
            // Dimensionless
            new UnitOfMeasure { Id = 21, Name = "Count", Symbol = "count", Category = "Dimensionless" },
            new UnitOfMeasure { Id = 22, Name = "Boolean", Symbol = "bool", Category = "Dimensionless" }
        );

        // Seed Node Categories (using icon names instead of emojis)
        modelBuilder.Entity<NodeCategory>().HasData(
            new NodeCategory { CategoryKey = "TRIGGERS", DisplayName = "Triggers", Icon = "bolt", DisplayOrder = 1 },
            new NodeCategory { CategoryKey = "TAG_OPERATIONS", DisplayName = "Tag Operations", Icon = "tag", DisplayOrder = 2 },
            new NodeCategory { CategoryKey = "DATA_PROCESSING", DisplayName = "Data Processing", Icon = "cog", DisplayOrder = 3 },
            new NodeCategory { CategoryKey = "LOGIC", DisplayName = "Logic", Icon = "branch", DisplayOrder = 4 },
            new NodeCategory { CategoryKey = "OUTPUT", DisplayName = "Output", Icon = "export", DisplayOrder = 5 }
        );

        // Seed Node Sections
        modelBuilder.Entity<NodeSection>().HasData(
            new NodeSection { CategoryKey = "TRIGGERS", SectionKey = "BASIC", DisplayName = "Basic", DisplayOrder = 1 },
            new NodeSection { CategoryKey = "TAG_OPERATIONS", SectionKey = "INPUT", DisplayName = "Input", DisplayOrder = 1 },
            new NodeSection { CategoryKey = "TAG_OPERATIONS", SectionKey = "OUTPUT", DisplayName = "Output", DisplayOrder = 2 },
            new NodeSection { CategoryKey = "DATA_PROCESSING", SectionKey = "MATH", DisplayName = "Math", DisplayOrder = 1 },
            new NodeSection { CategoryKey = "DATA_PROCESSING", SectionKey = "TRANSFORM", DisplayName = "Transform", DisplayOrder = 2 },
            new NodeSection { CategoryKey = "LOGIC", SectionKey = "COMPARISON", DisplayName = "Comparison", DisplayOrder = 1 },
            new NodeSection { CategoryKey = "LOGIC", SectionKey = "CONTROL", DisplayName = "Control Flow", DisplayOrder = 2 },
            new NodeSection { CategoryKey = "OUTPUT", SectionKey = "BASIC", DisplayName = "Basic", DisplayOrder = 1 }
        );
        
        // Seed sample Connections
        var connection1Id = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var connection2Id = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var simulatorConnectionId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        modelBuilder.Entity<Connection>().HasData(
            new Connection 
            { 
                Id = connection1Id, 
                Name = "Production-PLC-01", 
                Type = "EtherNet/IP", 
                ConfigData = "{\"host\":\"192.168.1.100\",\"port\":44818}",
                Enabled = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Connection 
            { 
                Id = connection2Id, 
                Name = "OPC-Server-Main", 
                Type = "OPC UA", 
                ConfigData = "{\"host\":\"192.168.1.50\",\"port\":4840}",
                Enabled = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Connection 
            { 
                Id = simulatorConnectionId, 
                Name = "Demo Simulator", 
                Type = "Simulator", 
                ConfigData = "{\"description\":\"Simulated connection for testing\"}",
                Enabled = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        
        // Seed sample tags for the Simulator connection
        modelBuilder.Entity<TagMetadata>().HasData(
            // Tank 1 Tags
            new TagMetadata
            {
                TagId = 1,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Tank1/Temperature",
                TagName = "Tank1_Temperature",
                DataType = "Float",
                Description = "Tank 1 Temperature Sensor",
                DriverType = "SIMULATOR",
                PollGroupId = 5,
                UnitId = 1, // Celsius
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 2,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Tank1/Pressure",
                TagName = "Tank1_Pressure",
                DataType = "Float",
                Description = "Tank 1 Pressure Sensor",
                DriverType = "SIMULATOR",
                PollGroupId = 5,
                UnitId = 5, // kPa
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 3,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Tank1/Level",
                TagName = "Tank1_Level",
                DataType = "Float",
                Description = "Tank 1 Level Sensor",
                DriverType = "SIMULATOR",
                PollGroupId = 5,
                UnitId = 15, // Percent
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 4,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Tank1/Flow_Inlet",
                TagName = "Tank1_Flow_Inlet",
                DataType = "Float",
                Description = "Tank 1 Inlet Flow Rate",
                DriverType = "SIMULATOR",
                PollGroupId = 5,
                UnitId = 12, // L/min
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 5,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Tank1/Pump_Status",
                TagName = "Tank1_Pump_Status",
                DataType = "Boolean",
                Description = "Tank 1 Pump Running Status",
                DriverType = "SIMULATOR",
                PollGroupId = 5,
                UnitId = 22, // Boolean
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            // Tank 2 Tags
            new TagMetadata
            {
                TagId = 6,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Tank2/Temperature",
                TagName = "Tank2_Temperature",
                DataType = "Float",
                Description = "Tank 2 Temperature Sensor",
                DriverType = "SIMULATOR",
                PollGroupId = 5,
                UnitId = 1, // Celsius
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 7,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Tank2/Pressure",
                TagName = "Tank2_Pressure",
                DataType = "Float",
                Description = "Tank 2 Pressure Sensor",
                DriverType = "SIMULATOR",
                PollGroupId = 5,
                UnitId = 5, // kPa
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 8,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Tank2/Level",
                TagName = "Tank2_Level",
                DataType = "Float",
                Description = "Tank 2 Level Sensor",
                DriverType = "SIMULATOR",
                PollGroupId = 5,
                UnitId = 15, // Percent
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            // Motor Tags
            new TagMetadata
            {
                TagId = 9,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Motor1/Speed",
                TagName = "Motor1_Speed",
                DataType = "Float",
                Description = "Motor 1 Speed",
                DriverType = "SIMULATOR",
                PollGroupId = 4,
                UnitId = 16, // RPM
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 10,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Motor1/Current",
                TagName = "Motor1_Current",
                DataType = "Float",
                Description = "Motor 1 Current Draw",
                DriverType = "SIMULATOR",
                PollGroupId = 4,
                UnitId = 9, // Ampere
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 11,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Motor1/Power",
                TagName = "Motor1_Power",
                DataType = "Float",
                Description = "Motor 1 Power Consumption",
                DriverType = "SIMULATOR",
                PollGroupId = 4,
                UnitId = 11, // kW
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            // Process Tags
            new TagMetadata
            {
                TagId = 12,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Process/Production_Rate",
                TagName = "Production_Rate",
                DataType = "Float",
                Description = "Production Rate",
                DriverType = "SIMULATOR",
                PollGroupId = 6,
                UnitId = 21, // Count
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 13,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Process/Quality_Index",
                TagName = "Quality_Index",
                DataType = "Float",
                Description = "Quality Index (0-100)",
                DriverType = "SIMULATOR",
                PollGroupId = 6,
                UnitId = 15, // Percent
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 14,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Process/Efficiency",
                TagName = "Process_Efficiency",
                DataType = "Float",
                Description = "Overall Process Efficiency",
                DriverType = "SIMULATOR",
                PollGroupId = 6,
                UnitId = 15, // Percent
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TagMetadata
            {
                TagId = 15,
                ConnectionId = simulatorConnectionId,
                TagPath = "Simulator/Process/Alarm_Count",
                TagName = "Alarm_Count",
                DataType = "Float",
                Description = "Active Alarm Count",
                DriverType = "SIMULATOR",
                PollGroupId = 7,
                UnitId = 21, // Count
                IsSubscribed = true,
                Status = "active",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        
        // Seed sample Flows
        var flow1Id = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var flow2Id = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var flow3Id = Guid.Parse("20000000-0000-0000-0000-000000000003");
        
        // Flow 1: Temperature Alert System
        var flow1Definition = @"{
            ""nodes"": [
                {
                    ""id"": ""node_1"",
                    ""type"": ""tag-input"",
                    ""label"": ""Tank 1 Temp"",
                    ""position"": { ""x"": 100, ""y"": 100 },
                    ""config"": {
                        ""tagId"": 1,
                        ""maxDataAge"": -1
                    }
                },
                {
                    ""id"": ""node_2"",
                    ""type"": ""tag-input"",
                    ""label"": ""Tank 2 Temp"",
                    ""position"": { ""x"": 100, ""y"": 250 },
                    ""config"": {
                        ""tagId"": 6,
                        ""maxDataAge"": -1
                    }
                },
                {
                    ""id"": ""node_3"",
                    ""type"": ""math"",
                    ""label"": ""Calculate Average"",
                    ""position"": { ""x"": 400, ""y"": 175 },
                    ""config"": {
                        ""operation"": ""average"",
                        ""inputs"": [""input1"", ""input2""]
                    }
                },
                {
                    ""id"": ""node_4"",
                    ""type"": ""comparison"",
                    ""label"": ""High Temp Alert"",
                    ""position"": { ""x"": 400, ""y"": 50 },
                    ""config"": {
                        ""operator"": "">"",
                        ""threshold"": 75
                    }
                },
                {
                    ""id"": ""node_5"",
                    ""type"": ""comparison"",
                    ""label"": ""Low Temp Alert"",
                    ""position"": { ""x"": 400, ""y"": 300 },
                    ""config"": {
                        ""operator"": ""<"",
                        ""threshold"": 30
                    }
                },
                {
                    ""id"": ""node_6"",
                    ""type"": ""csharp"",
                    ""label"": ""Alert Logic"",
                    ""position"": { ""x"": 700, ""y"": 175 },
                    ""config"": {
                        ""code"": ""// Determine alert status\nvar highAlert = input.highTemp as bool? ?? false;\nvar lowAlert = input.lowTemp as bool? ?? false;\nvar avgTemp = input.average as double? ?? 0.0;\n\nif (highAlert)\n{\n    return new { alert = true, level = \""high\"", message = \""Temperature too high!\"", value = avgTemp };\n}\nelse if (lowAlert)\n{\n    return new { alert = true, level = \""low\"", message = \""Temperature too low!\"", value = avgTemp };\n}\nelse\n{\n    return new { alert = false, level = \""normal\"", message = \""Temperature normal\"", value = avgTemp };\n}""
                    }
                }
            ],
            ""edges"": [
                {
                    ""id"": ""edge_1"",
                    ""source"": ""node_1"",
                    ""target"": ""node_3"",
                    ""targetHandle"": ""input1""
                },
                {
                    ""id"": ""edge_2"",
                    ""source"": ""node_2"",
                    ""target"": ""node_3"",
                    ""targetHandle"": ""input2""
                },
                {
                    ""id"": ""edge_3"",
                    ""source"": ""node_3"",
                    ""target"": ""node_6"",
                    ""targetHandle"": ""average""
                },
                {
                    ""id"": ""edge_4"",
                    ""source"": ""node_1"",
                    ""target"": ""node_4"",
                    ""targetHandle"": ""value""
                },
                {
                    ""id"": ""edge_5"",
                    ""source"": ""node_4"",
                    ""target"": ""node_6"",
                    ""targetHandle"": ""highTemp""
                },
                {
                    ""id"": ""edge_6"",
                    ""source"": ""node_2"",
                    ""target"": ""node_5"",
                    ""targetHandle"": ""value""
                },
                {
                    ""id"": ""edge_7"",
                    ""source"": ""node_5"",
                    ""target"": ""node_6"",
                    ""targetHandle"": ""lowTemp""
                }
            ]
        }";
        
        // Flow 2: Production Efficiency Calculator
        var flow2Definition = @"{
            ""nodes"": [
                {
                    ""id"": ""node_1"",
                    ""type"": ""tag-input"",
                    ""label"": ""Production Rate"",
                    ""position"": { ""x"": 100, ""y"": 100 },
                    ""config"": {
                        ""tagId"": 12,
                        ""maxDataAge"": -1
                    }
                },
                {
                    ""id"": ""node_2"",
                    ""type"": ""tag-input"",
                    ""label"": ""Motor Power"",
                    ""position"": { ""x"": 100, ""y"": 200 },
                    ""config"": {
                        ""tagId"": 11,
                        ""maxDataAge"": -1
                    }
                },
                {
                    ""id"": ""node_3"",
                    ""type"": ""math"",
                    ""label"": ""Efficiency"",
                    ""position"": { ""x"": 400, ""y"": 150 },
                    ""config"": {
                        ""operation"": ""divide"",
                        ""description"": ""Calculate units per kW""
                    }
                },
                {
                    ""id"": ""node_4"",
                    ""type"": ""csharp"",
                    ""label"": ""Format Result"",
                    ""position"": { ""x"": 650, ""y"": 150 },
                    ""config"": {
                        ""code"": ""// Calculate efficiency percentage\nvar efficiency = input.value as double? ?? 0.0;\nvar percentage = Math.Min(100, Math.Max(0, efficiency * 10));\n\nreturn new\n{\n    efficiency = efficiency.ToString(\""F2\""),\n    percentage = percentage.ToString(\""F1\""),\n    rating = percentage > 80 ? \""Excellent\"" : percentage > 60 ? \""Good\"" : \""Poor\""\n};""
                    }
                }
            ],
            ""edges"": [
                {
                    ""id"": ""edge_1"",
                    ""source"": ""node_1"",
                    ""target"": ""node_3"",
                    ""targetHandle"": ""numerator""
                },
                {
                    ""id"": ""edge_2"",
                    ""source"": ""node_2"",
                    ""target"": ""node_3"",
                    ""targetHandle"": ""denominator""
                },
                {
                    ""id"": ""edge_3"",
                    ""source"": ""node_3"",
                    ""target"": ""node_4"",
                    ""targetHandle"": ""value""
                }
            ]
        }";
        
        modelBuilder.Entity<Flow>().HasData(
            new Flow 
            { 
                Id = flow1Id, 
                Name = "Temperature Alert System", 
                Description = "Monitors tank temperatures, calculates average, and generates alerts when out of normal range (30-75°C)",
                OwnerUserId = adminUserId,
                Deployed = false,
                Shared = true,
                TestMode = false,
                ExecutionMode = "continuous",
                ScanRateMs = 2000,
                Definition = flow1Definition,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Flow 
            { 
                Id = flow2Id, 
                Name = "Production Efficiency Calculator", 
                Description = "Calculates production efficiency as units produced per kW of power consumed",
                OwnerUserId = adminUserId,
                Deployed = false,
                Shared = true,
                TestMode = false,
                ExecutionMode = "continuous",
                ScanRateMs = 5000,
                Definition = flow2Definition,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Flow 
            { 
                Id = flow3Id, 
                Name = "Simple Math Example", 
                Description = "Basic example: reads two tags and calculates their sum",
                OwnerUserId = adminUserId,
                Deployed = false,
                Shared = true,
                TestMode = false,
                ExecutionMode = "continuous",
                ScanRateMs = 1000,
                Definition = @"{""nodes"":[{""id"":""n1"",""type"":""tag-input"",""label"":""Tag 1"",""position"":{""x"":100,""y"":100},""config"":{""tagId"":1}},{""id"":""n2"",""type"":""tag-input"",""label"":""Tag 2"",""position"":{""x"":100,""y"":200},""config"":{""tagId"":2}},{""id"":""n3"",""type"":""math"",""label"":""Add"",""position"":{""x"":350,""y"":150},""config"":{""operation"":""add""}}],""edges"":[{""id"":""e1"",""source"":""n1"",""target"":""n3""},{""id"":""e2"",""source"":""n2"",""target"":""n3""}]}",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        
        // Seed Template Flow - Reusable Threshold Monitor
        var templateFlowId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var templateFlowDefinition = @"{
            ""nodes"": [
                {
                    ""id"": ""input_value"",
                    ""type"": ""tag-input"",
                    ""label"": ""Value Input"",
                    ""position"": { ""x"": 100, ""y"": 150 },
                    ""config"": { ""tagId"": 1, ""maxDataAge"": -1 }
                },
                {
                    ""id"": ""high_check"",
                    ""type"": ""compare"",
                    ""label"": ""Check High"",
                    ""position"": { ""x"": 350, ""y"": 100 },
                    ""config"": { ""operation"": "">"", ""threshold"": 75.0 }
                },
                {
                    ""id"": ""low_check"",
                    ""type"": ""compare"",
                    ""label"": ""Check Low"",
                    ""position"": { ""x"": 350, ""y"": 200 },
                    ""config"": { ""operation"": ""<"", ""threshold"": 30.0 }
                },
                {
                    ""id"": ""alert_logic"",
                    ""type"": ""csharp"",
                    ""label"": ""Alert Logic"",
                    ""position"": { ""x"": 600, ""y"": 150 },
                    ""config"": {
                        ""code"": ""var high = input.GetBool(\""highAlert\"") ?? false; var low = input.GetBool(\""lowAlert\"") ?? false; var value = input.GetDouble(\""value\"") ?? 0.0; var highThreshold = flow.parameters.GetValueOrDefault(\""highThreshold\"", 75.0); var lowThreshold = flow.parameters.GetValueOrDefault(\""lowThreshold\"", 30.0); if (high) { return new { alert = true, level = \""high\"", message = $\""Value {value:F1} exceeds high threshold {highThreshold}\"", value = value }; } else if (low) { return new { alert = true, level = \""low\"", message = $\""Value {value:F1} below low threshold {lowThreshold}\"", value = value }; } return new { alert = false, level = \""normal\"", message = \""Value within range\"", value = value };""
                    }
                }
            ],
            ""edges"": [
                { ""id"": ""e1"", ""source"": ""input_value"", ""target"": ""high_check"", ""targetHandle"": ""value"" },
                { ""id"": ""e2"", ""source"": ""input_value"", ""target"": ""low_check"", ""targetHandle"": ""value"" },
                { ""id"": ""e3"", ""source"": ""high_check"", ""target"": ""alert_logic"", ""targetHandle"": ""highAlert"" },
                { ""id"": ""e4"", ""source"": ""low_check"", ""target"": ""alert_logic"", ""targetHandle"": ""lowAlert"" },
                { ""id"": ""e5"", ""source"": ""input_value"", ""target"": ""alert_logic"", ""targetHandle"": ""value"" }
            ]
        }";
        
        modelBuilder.Entity<Flow>().HasData(
            new Flow 
            { 
                Id = templateFlowId, 
                Name = "Threshold Monitor Template", 
                Description = "Reusable template for monitoring a value against high and low thresholds with configurable limits",
                OwnerUserId = adminUserId,
                Deployed = false,
                Shared = true,
                TestMode = false,
                IsTemplate = true,
                TemplateInputs = @"[""value""]",
                TemplateOutputs = @"[""alert"", ""level"", ""message"", ""value""]",
                ExposedParameters = @"[{""name"":""highThreshold"",""type"":""double"",""default"":75.0,""description"":""High threshold limit""},{""name"":""lowThreshold"",""type"":""double"",""default"":30.0,""description"":""Low threshold limit""}]",
                ExecutionMode = "continuous",
                ScanRateMs = 2000,
                Definition = templateFlowDefinition,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        
        // Seed sample Charts
        var chart1Id = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var chart2Id = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var chart3Id = Guid.Parse("30000000-0000-0000-0000-000000000003");
        modelBuilder.Entity<ChartConfig>().HasData(
            new ChartConfig 
            { 
                Id = chart1Id, 
                Name = "Temperature Trends", 
                ChartType = "line",
                UserId = adminUserId,
                TimeMode = "rolling",
                TimeDuration = 3600000, // 1 hour
                LiveEnabled = true,
                RefreshInterval = 5000,
                Options = "{}",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ChartConfig 
            { 
                Id = chart2Id, 
                Name = "Pressure Overview", 
                ChartType = "area",
                UserId = adminUserId,
                TimeMode = "rolling",
                TimeDuration = 7200000, // 2 hours
                LiveEnabled = true,
                Options = "{}",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ChartConfig 
            { 
                Id = chart3Id, 
                Name = "Multi-Axis Process Monitor", 
                Description = "Temperature, Pressure, and Motor Speed on multiple axes",
                ChartType = "line",
                UserId = adminUserId,
                TimeMode = "rolling",
                TimeDuration = 3600000, // 1 hour
                LiveEnabled = true,
                RefreshInterval = 5000,
                EnableLegend = true,
                LegendPosition = "bottom",
                EnableTooltip = true,
                EnableZoom = true,
                EnablePan = true,
                Options = "{}",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        
        // Seed Chart Axes for multi-axis chart
        var axis1Id = Guid.Parse("31000000-0000-0000-0000-000000000001");
        var axis2Id = Guid.Parse("31000000-0000-0000-0000-000000000002");
        var axis3Id = Guid.Parse("31000000-0000-0000-0000-000000000003");
        modelBuilder.Entity<ChartAxis>().HasData(
            new ChartAxis
            {
                Id = axis1Id,
                ChartId = chart3Id,
                AxisIndex = 0,
                AxisType = "Y",
                Position = "left",
                Label = "Temperature (°C)",
                AutoScale = true,
                ShowGridLines = true,
                GridLineStyle = "solid",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ChartAxis
            {
                Id = axis2Id,
                ChartId = chart3Id,
                AxisIndex = 1,
                AxisType = "Y",
                Position = "right",
                Label = "Pressure (kPa)",
                AutoScale = true,
                ShowGridLines = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ChartAxis
            {
                Id = axis3Id,
                ChartId = chart3Id,
                AxisIndex = 2,
                AxisType = "Y",
                Position = "right",
                Label = "Motor Speed (RPM)",
                AutoScale = true,
                ShowGridLines = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        
        // Seed Chart Series for multi-axis chart
        modelBuilder.Entity<ChartSeries>().HasData(
            new ChartSeries
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000001"),
                ChartId = chart3Id,
                TagId = 1, // Tank1 Temperature
                Label = "Tank 1 Temperature",
                Color = "#ff6384",
                SeriesType = "line",
                AxisIndex = 0,
                DisplayOrder = 0,
                Visible = true,
                LineWidth = 2,
                ShowMarkers = true,
                MarkerSize = 6,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ChartSeries
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000002"),
                ChartId = chart3Id,
                TagId = 6, // Tank2 Temperature
                Label = "Tank 2 Temperature",
                Color = "#ff9f40",
                SeriesType = "line",
                AxisIndex = 0,
                DisplayOrder = 1,
                Visible = true,
                LineWidth = 2,
                ShowMarkers = true,
                MarkerSize = 6,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ChartSeries
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000003"),
                ChartId = chart3Id,
                TagId = 2, // Tank1 Pressure
                Label = "Tank 1 Pressure",
                Color = "#36a2eb",
                SeriesType = "line",
                AxisIndex = 1,
                DisplayOrder = 2,
                Visible = true,
                LineWidth = 2,
                ShowMarkers = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ChartSeries
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000004"),
                ChartId = chart3Id,
                TagId = 9, // Motor Speed
                Label = "Motor 1 Speed",
                Color = "#4bc0c0",
                SeriesType = "line",
                AxisIndex = 2,
                DisplayOrder = 3,
                Visible = true,
                LineWidth = 2.5,
                ShowMarkers = false,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
        
        // Seed sample Dashboard
        var dashboard1Id = Guid.Parse("40000000-0000-0000-0000-000000000001");
        modelBuilder.Entity<Dashboard>().HasData(
            new Dashboard 
            { 
                Id = dashboard1Id, 
                Name = "Production Overview", 
                Description = "Main production monitoring dashboard",
                UserId = adminUserId,
                Layout = "{}",
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
