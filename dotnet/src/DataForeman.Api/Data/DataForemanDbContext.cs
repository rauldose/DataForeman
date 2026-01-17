using Microsoft.EntityFrameworkCore;
using DataForeman.Shared.Models;

namespace DataForeman.Api.Data;

public class DataForemanDbContext : DbContext
{
    public DataForemanDbContext(DbContextOptions<DataForemanDbContext> options) : base(options) { }

    // Authentication & Authorization
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthIdentity> AuthIdentities => Set<AuthIdentity>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Session> Sessions => Set<Session>();

    // Connectivity
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<PollGroup> PollGroups => Set<PollGroup>();
    public DbSet<TagMetadata> TagMetadata => Set<TagMetadata>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();

    // Dashboards & Charts
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<DashboardFolder> DashboardFolders => Set<DashboardFolder>();
    public DbSet<ChartConfig> ChartConfigs => Set<ChartConfig>();

    // Flows
    public DbSet<Flow> Flows => Set<Flow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Users
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Email).HasColumnName("email").IsRequired();
            e.Property(u => u.DisplayName).HasColumnName("display_name");
            e.Property(u => u.IsActive).HasColumnName("is_active");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.Property(u => u.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(u => u.Email).IsUnique();
        });

        // Auth Identities
        modelBuilder.Entity<AuthIdentity>(e =>
        {
            e.ToTable("auth_identities");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id");
            e.Property(a => a.UserId).HasColumnName("user_id");
            e.Property(a => a.Provider).HasColumnName("provider");
            e.Property(a => a.ProviderUserId).HasColumnName("provider_user_id");
            e.Property(a => a.SecretHash).HasColumnName("secret_hash");
            e.Property(a => a.FailedAttempts).HasColumnName("failed_attempts");
            e.Property(a => a.LockedUntil).HasColumnName("locked_until");
            e.Property(a => a.LastLoginAt).HasColumnName("last_login_at");
            e.Property(a => a.CreatedAt).HasColumnName("created_at");
            e.Property(a => a.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(a => a.User).WithMany(u => u.AuthIdentities).HasForeignKey(a => a.UserId);
            e.HasIndex(a => new { a.Provider, a.ProviderUserId }).IsUnique();
        });

        // Roles
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.Name).HasColumnName("name").IsRequired();
            e.HasIndex(r => r.Name).IsUnique();
        });

        // User Roles
        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
            e.Property(ur => ur.UserId).HasColumnName("user_id");
            e.Property(ur => ur.RoleId).HasColumnName("role_id");
            e.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
            e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
        });

        // User Permissions
        modelBuilder.Entity<UserPermission>(e =>
        {
            e.ToTable("user_permissions");
            e.HasKey(up => new { up.UserId, up.Feature });
            e.Property(up => up.UserId).HasColumnName("user_id");
            e.Property(up => up.Feature).HasColumnName("feature");
            e.Property(up => up.CanCreate).HasColumnName("can_create");
            e.Property(up => up.CanRead).HasColumnName("can_read");
            e.Property(up => up.CanUpdate).HasColumnName("can_update");
            e.Property(up => up.CanDelete).HasColumnName("can_delete");
            e.Property(up => up.CreatedAt).HasColumnName("created_at");
            e.Property(up => up.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(up => up.User).WithMany(u => u.UserPermissions).HasForeignKey(up => up.UserId);
        });

        // Sessions
        modelBuilder.Entity<Session>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id");
            e.Property(s => s.UserId).HasColumnName("user_id");
            e.Property(s => s.Jti).HasColumnName("jti");
            e.Property(s => s.RefreshHash).HasColumnName("refresh_hash");
            e.Property(s => s.UserAgent).HasColumnName("user_agent");
            e.Property(s => s.Ip).HasColumnName("ip");
            e.Property(s => s.CreatedAt).HasColumnName("created_at");
            e.Property(s => s.ExpiresAt).HasColumnName("expires_at");
            e.Property(s => s.RevokedAt).HasColumnName("revoked_at");
            e.Property(s => s.ReplacedByJti).HasColumnName("replaced_by_jti");
            e.Property(s => s.LastActivityAt).HasColumnName("last_activity_at");
            e.HasOne(s => s.User).WithMany(u => u.Sessions).HasForeignKey(s => s.UserId);
            e.HasIndex(s => s.Jti).IsUnique();
        });

        // Connections
        modelBuilder.Entity<Connection>(e =>
        {
            e.ToTable("connections");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.Name).HasColumnName("name").IsRequired();
            e.Property(c => c.Type).HasColumnName("type").IsRequired();
            e.Property(c => c.Enabled).HasColumnName("enabled");
            e.Property(c => c.ConfigData).HasColumnName("config_data");
            e.Property(c => c.IsSystemConnection).HasColumnName("is_system_connection");
            e.Property(c => c.MaxTagsPerGroup).HasColumnName("max_tags_per_group");
            e.Property(c => c.MaxConcurrentConnections).HasColumnName("max_concurrent_connections");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.Property(c => c.UpdatedAt).HasColumnName("updated_at");
            e.Property(c => c.DeletedAt).HasColumnName("deleted_at");
            e.HasIndex(c => c.Name).IsUnique();
        });

        // Poll Groups
        modelBuilder.Entity<PollGroup>(e =>
        {
            e.ToTable("poll_groups");
            e.HasKey(p => p.GroupId);
            e.Property(p => p.GroupId).HasColumnName("group_id");
            e.Property(p => p.Name).HasColumnName("name").IsRequired();
            e.Property(p => p.PollRateMs).HasColumnName("poll_rate_ms");
            e.Property(p => p.Description).HasColumnName("description");
            e.Property(p => p.IsActive).HasColumnName("is_active");
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
        });

        // Units of Measure
        modelBuilder.Entity<UnitOfMeasure>(e =>
        {
            e.ToTable("units_of_measure");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.Name).HasColumnName("name").IsRequired();
            e.Property(u => u.Symbol).HasColumnName("symbol").IsRequired();
            e.Property(u => u.Category).HasColumnName("category").IsRequired();
            e.Property(u => u.IsSystem).HasColumnName("is_system");
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.Property(u => u.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(u => u.Name).IsUnique();
        });

        // Tag Metadata
        modelBuilder.Entity<TagMetadata>(e =>
        {
            e.ToTable("tag_metadata");
            e.HasKey(t => t.TagId);
            e.Property(t => t.TagId).HasColumnName("tag_id");
            e.Property(t => t.ConnectionId).HasColumnName("connection_id");
            e.Property(t => t.DriverType).HasColumnName("driver_type");
            e.Property(t => t.TagPath).HasColumnName("tag_path");
            e.Property(t => t.TagName).HasColumnName("tag_name");
            e.Property(t => t.IsSubscribed).HasColumnName("is_subscribed");
            e.Property(t => t.IsDeleted).HasColumnName("is_deleted");
            e.Property(t => t.Status).HasColumnName("status");
            e.Property(t => t.OriginalSubscribed).HasColumnName("original_subscribed");
            e.Property(t => t.DeleteJobId).HasColumnName("delete_job_id");
            e.Property(t => t.DeleteStartedAt).HasColumnName("delete_started_at");
            e.Property(t => t.DeletedAt).HasColumnName("deleted_at");
            e.Property(t => t.PollGroupId).HasColumnName("poll_group_id");
            e.Property(t => t.DataType).HasColumnName("data_type");
            e.Property(t => t.UnitId).HasColumnName("unit_id");
            e.Property(t => t.Description).HasColumnName("description");
            e.Property(t => t.Metadata).HasColumnName("metadata");
            e.Property(t => t.OnChangeEnabled).HasColumnName("on_change_enabled");
            e.Property(t => t.OnChangeDeadband).HasColumnName("on_change_deadband");
            e.Property(t => t.OnChangeDeadbandType).HasColumnName("on_change_deadband_type");
            e.Property(t => t.OnChangeHeartbeatMs).HasColumnName("on_change_heartbeat_ms");
            e.Property(t => t.CreatedAt).HasColumnName("created_at");
            e.Property(t => t.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(t => t.Connection).WithMany(c => c.Tags).HasForeignKey(t => t.ConnectionId);
            e.HasOne(t => t.PollGroup).WithMany(p => p.Tags).HasForeignKey(t => t.PollGroupId);
            e.HasOne(t => t.Unit).WithMany().HasForeignKey(t => t.UnitId);
            e.HasIndex(t => new { t.ConnectionId, t.TagPath, t.DriverType }).IsUnique();
        });

        // Dashboards
        modelBuilder.Entity<Dashboard>(e =>
        {
            e.ToTable("dashboard_configs");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id");
            e.Property(d => d.UserId).HasColumnName("user_id");
            e.Property(d => d.FolderId).HasColumnName("folder_id");
            e.Property(d => d.Name).HasColumnName("name").IsRequired();
            e.Property(d => d.Description).HasColumnName("description");
            e.Property(d => d.IsShared).HasColumnName("is_shared");
            e.Property(d => d.IsDeleted).HasColumnName("is_deleted");
            e.Property(d => d.Layout).HasColumnName("layout");
            e.Property(d => d.Options).HasColumnName("options");
            e.Property(d => d.CreatedAt).HasColumnName("created_at");
            e.Property(d => d.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId);
            e.HasOne(d => d.Folder).WithMany(f => f.Dashboards).HasForeignKey(d => d.FolderId);
        });

        // Dashboard Folders
        modelBuilder.Entity<DashboardFolder>(e =>
        {
            e.ToTable("dashboard_folders");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id");
            e.Property(f => f.UserId).HasColumnName("user_id");
            e.Property(f => f.Name).HasColumnName("name").IsRequired();
            e.Property(f => f.Description).HasColumnName("description");
            e.Property(f => f.ParentFolderId).HasColumnName("parent_folder_id");
            e.Property(f => f.SortOrder).HasColumnName("sort_order");
            e.Property(f => f.CreatedAt).HasColumnName("created_at");
            e.Property(f => f.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId);
            e.HasOne(f => f.ParentFolder).WithMany(f => f.ChildFolders).HasForeignKey(f => f.ParentFolderId);
        });

        // Chart Configs
        modelBuilder.Entity<ChartConfig>(e =>
        {
            e.ToTable("chart_configs");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.UserId).HasColumnName("user_id");
            e.Property(c => c.FolderId).HasColumnName("folder_id");
            e.Property(c => c.Name).HasColumnName("name").IsRequired();
            e.Property(c => c.Description).HasColumnName("description");
            e.Property(c => c.ChartType).HasColumnName("chart_type");
            e.Property(c => c.IsSystemChart).HasColumnName("is_system_chart");
            e.Property(c => c.IsDeleted).HasColumnName("is_deleted");
            e.Property(c => c.IsShared).HasColumnName("is_shared");
            e.Property(c => c.TimeMode).HasColumnName("time_mode");
            e.Property(c => c.TimeDuration).HasColumnName("time_duration");
            e.Property(c => c.TimeOffset).HasColumnName("time_offset");
            e.Property(c => c.LiveEnabled).HasColumnName("live_enabled");
            e.Property(c => c.ShowTimeBadge).HasColumnName("show_time_badge");
            e.Property(c => c.TimeFrom).HasColumnName("time_from");
            e.Property(c => c.TimeTo).HasColumnName("time_to");
            e.Property(c => c.TimeRangeMs).HasColumnName("time_range_ms");
            e.Property(c => c.Options).HasColumnName("options");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.Property(c => c.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId);
        });

        // Flows
        modelBuilder.Entity<Flow>(e =>
        {
            e.ToTable("flows");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id");
            e.Property(f => f.Name).HasColumnName("name").IsRequired();
            e.Property(f => f.Description).HasColumnName("description");
            e.Property(f => f.OwnerUserId).HasColumnName("owner_user_id");
            e.Property(f => f.FolderId).HasColumnName("folder_id");
            e.Property(f => f.Deployed).HasColumnName("deployed");
            e.Property(f => f.Shared).HasColumnName("shared");
            e.Property(f => f.TestMode).HasColumnName("test_mode");
            e.Property(f => f.TestDisableWrites).HasColumnName("test_disable_writes");
            e.Property(f => f.TestAutoExit).HasColumnName("test_auto_exit");
            e.Property(f => f.TestAutoExitMinutes).HasColumnName("test_auto_exit_minutes");
            e.Property(f => f.ExecutionMode).HasColumnName("execution_mode");
            e.Property(f => f.ScanRateMs).HasColumnName("scan_rate_ms");
            e.Property(f => f.LiveValuesUseScanRate).HasColumnName("live_values_use_scan_rate");
            e.Property(f => f.LogsEnabled).HasColumnName("logs_enabled");
            e.Property(f => f.LogsRetentionDays).HasColumnName("logs_retention_days");
            e.Property(f => f.SaveUsageData).HasColumnName("save_usage_data");
            e.Property(f => f.ExposedParameters).HasColumnName("exposed_parameters");
            e.Property(f => f.ResourceChartId).HasColumnName("resource_chart_id");
            e.Property(f => f.Definition).HasColumnName("definition");
            e.Property(f => f.StaticData).HasColumnName("static_data");
            e.Property(f => f.CreatedAt).HasColumnName("created_at");
            e.Property(f => f.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(f => f.Owner).WithMany().HasForeignKey(f => f.OwnerUserId);
        });
    }
}
