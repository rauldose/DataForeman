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
            entity.HasIndex(e => e.PollRateMs).IsUnique();
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

        // Seed Node Categories
        modelBuilder.Entity<NodeCategory>().HasData(
            new NodeCategory { CategoryKey = "TRIGGERS", DisplayName = "Triggers", Icon = "⚡", DisplayOrder = 1 },
            new NodeCategory { CategoryKey = "TAG_OPERATIONS", DisplayName = "Tag Operations", Icon = "🏷️", DisplayOrder = 2 },
            new NodeCategory { CategoryKey = "DATA_PROCESSING", DisplayName = "Data Processing", Icon = "⚙️", DisplayOrder = 3 },
            new NodeCategory { CategoryKey = "LOGIC", DisplayName = "Logic", Icon = "🔀", DisplayOrder = 4 },
            new NodeCategory { CategoryKey = "OUTPUT", DisplayName = "Output", Icon = "📤", DisplayOrder = 5 }
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
    }
}
