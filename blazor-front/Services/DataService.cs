using DataForeman.Core.Entities;
using DataForeman.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataForeman.BlazorUI.Services;

/// <summary>
/// Direct data access service for modular monolith architecture.
/// Provides database access without HTTP overhead.
/// </summary>
public class DataService
{
    private readonly DataForemanDbContext _dbContext;
    private readonly ILogger<DataService> _logger;

    public DataService(DataForemanDbContext dbContext, ILogger<DataService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region Authentication

    /// <summary>
    /// Validates user credentials and returns user if valid.
    /// </summary>
    public async Task<User?> ValidateCredentialsAsync(string email, string password)
    {
        try
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

            if (user == null) return null;

            // Verify password using BCrypt
            if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                // Update timestamp
                user.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return user;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials for {Email}", email);
            return null;
        }
    }

    /// <summary>
    /// Gets user by ID.
    /// </summary>
    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        try
        {
            return await _dbContext.Users.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return null;
        }
    }

    /// <summary>
    /// Gets user by email.
    /// </summary>
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        try
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email {Email}", email);
            return null;
        }
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public async Task<User?> CreateUserAsync(string email, string password, string? displayName)
    {
        try
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = displayName ?? email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Email}", email);
            return null;
        }
    }

    /// <summary>
    /// Changes user password.
    /// </summary>
    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        try
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return false;
        }
    }

    #endregion

    #region Users

    /// <summary>
    /// Gets all users with pagination.
    /// </summary>
    public async Task<(List<User> Users, int TotalCount)> GetUsersAsync(int limit = 50, int offset = 0)
    {
        try
        {
            var totalCount = await _dbContext.Users.CountAsync();
            var users = await _dbContext.Users
                .OrderBy(u => u.Email)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return (users, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return (new List<User>(), 0);
        }
    }

    /// <summary>
    /// Updates a user.
    /// </summary>
    public async Task<bool> UpdateUserAsync(Guid id, string? displayName, string? email, bool? isActive, string? password)
    {
        try
        {
            var user = await _dbContext.Users.FindAsync(id);
            if (user == null) return false;

            if (displayName != null) user.DisplayName = displayName;
            if (email != null) user.Email = email;
            if (isActive.HasValue) user.IsActive = isActive.Value;
            if (!string.IsNullOrEmpty(password))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);

            user.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return false;
        }
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    public async Task<bool> DeleteUserAsync(Guid id)
    {
        try
        {
            var user = await _dbContext.Users.FindAsync(id);
            if (user == null) return false;

            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return false;
        }
    }

    #endregion

    #region Flows

    /// <summary>
    /// Gets all flows with pagination.
    /// </summary>
    public async Task<(List<Flow> Flows, int TotalCount)> GetFlowsAsync(int limit = 50, int offset = 0)
    {
        try
        {
            var totalCount = await _dbContext.Flows.CountAsync();
            var flows = await _dbContext.Flows
                .OrderByDescending(f => f.UpdatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return (flows, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flows");
            return (new List<Flow>(), 0);
        }
    }

    /// <summary>
    /// Gets a flow by ID.
    /// </summary>
    public async Task<Flow?> GetFlowAsync(Guid id)
    {
        try
        {
            return await _dbContext.Flows.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flow {FlowId}", id);
            return null;
        }
    }

    /// <summary>
    /// Creates a new flow.
    /// </summary>
    public async Task<Flow?> CreateFlowAsync(string name, string? description, string? definition, Guid ownerId)
    {
        try
        {
            var flow = new Flow
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                Definition = definition ?? "{}",
                OwnerUserId = ownerId,
                Deployed = false,
                Shared = false,
                TestMode = false,
                ExecutionMode = "continuous",
                ScanRateMs = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Flows.Add(flow);
            await _dbContext.SaveChangesAsync();
            return flow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating flow");
            return null;
        }
    }

    /// <summary>
    /// Updates a flow.
    /// </summary>
    public async Task<bool> UpdateFlowAsync(Guid id, string? name, string? description, string? definition, 
        string? executionMode, int? scanRateMs, bool? shared, bool? testMode)
    {
        try
        {
            var flow = await _dbContext.Flows.FindAsync(id);
            if (flow == null) return false;

            if (name != null) flow.Name = name;
            if (description != null) flow.Description = description;
            if (definition != null) flow.Definition = definition;
            if (executionMode != null) flow.ExecutionMode = executionMode;
            if (scanRateMs.HasValue) flow.ScanRateMs = scanRateMs.Value;
            if (shared.HasValue) flow.Shared = shared.Value;
            if (testMode.HasValue) flow.TestMode = testMode.Value;

            flow.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating flow {FlowId}", id);
            return false;
        }
    }

    /// <summary>
    /// Deploys or undeploys a flow.
    /// </summary>
    public async Task<bool> DeployFlowAsync(Guid id, bool deploy)
    {
        try
        {
            var flow = await _dbContext.Flows.FindAsync(id);
            if (flow == null) return false;

            flow.Deployed = deploy;
            flow.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying flow {FlowId}", id);
            return false;
        }
    }

    /// <summary>
    /// Deletes a flow.
    /// </summary>
    public async Task<bool> DeleteFlowAsync(Guid id)
    {
        try
        {
            var flow = await _dbContext.Flows.FindAsync(id);
            if (flow == null) return false;

            _dbContext.Flows.Remove(flow);
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting flow {FlowId}", id);
            return false;
        }
    }

    #endregion

    #region Dashboards

    /// <summary>
    /// Gets all dashboards.
    /// </summary>
    public async Task<List<Dashboard>> GetDashboardsAsync()
    {
        try
        {
            return await _dbContext.Dashboards
                .OrderByDescending(d => d.UpdatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboards");
            return new List<Dashboard>();
        }
    }

    /// <summary>
    /// Gets a dashboard by ID.
    /// </summary>
    public async Task<Dashboard?> GetDashboardAsync(Guid id)
    {
        try
        {
            return await _dbContext.Dashboards.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard {DashboardId}", id);
            return null;
        }
    }

    /// <summary>
    /// Creates a new dashboard.
    /// </summary>
    public async Task<Dashboard?> CreateDashboardAsync(string name, string? description, string? definition, Guid ownerId)
    {
        try
        {
            var dashboard = new Dashboard
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                Layout = definition ?? "{}",
                UserId = ownerId,
                IsShared = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Dashboards.Add(dashboard);
            await _dbContext.SaveChangesAsync();
            return dashboard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dashboard");
            return null;
        }
    }

    #endregion

    #region Charts

    /// <summary>
    /// Gets all charts.
    /// </summary>
    public async Task<List<ChartConfig>> GetChartsAsync()
    {
        try
        {
            return await _dbContext.ChartConfigs
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting charts");
            return new List<ChartConfig>();
        }
    }

    /// <summary>
    /// Gets a chart by ID.
    /// </summary>
    public async Task<ChartConfig?> GetChartAsync(Guid id)
    {
        try
        {
            return await _dbContext.ChartConfigs.FindAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chart {ChartId}", id);
            return null;
        }
    }

    /// <summary>
    /// Creates a new chart.
    /// </summary>
    public async Task<ChartConfig?> CreateChartAsync(string name, string chartType, string? definition, Guid ownerId)
    {
        try
        {
            var chart = new ChartConfig
            {
                Id = Guid.NewGuid(),
                Name = name,
                ChartType = chartType,
                Options = definition ?? "{}",
                UserId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.ChartConfigs.Add(chart);
            await _dbContext.SaveChangesAsync();
            return chart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chart");
            return null;
        }
    }

    /// <summary>
    /// Deletes a chart.
    /// </summary>
    public async Task<bool> DeleteChartAsync(Guid id)
    {
        try
        {
            var chart = await _dbContext.ChartConfigs.FindAsync(id);
            if (chart == null) return false;

            _dbContext.ChartConfigs.Remove(chart);
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chart {ChartId}", id);
            return false;
        }
    }

    #endregion

    #region Connections

    /// <summary>
    /// Gets all connections.
    /// </summary>
    public async Task<List<Connection>> GetConnectionsAsync()
    {
        try
        {
            return await _dbContext.Connections
                .Include(c => c.Tags)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connections");
            return new List<Connection>();
        }
    }

    /// <summary>
    /// Gets a connection by ID.
    /// </summary>
    public async Task<Connection?> GetConnectionAsync(Guid id)
    {
        try
        {
            return await _dbContext.Connections
                .Include(c => c.Tags)
                .FirstOrDefaultAsync(c => c.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection {ConnectionId}", id);
            return null;
        }
    }

    /// <summary>
    /// Creates a new connection.
    /// </summary>
    public async Task<Connection?> CreateConnectionAsync(string name, string type, string? config)
    {
        try
        {
            var connection = new Connection
            {
                Id = Guid.NewGuid(),
                Name = name,
                Type = type,
                ConfigData = config ?? "{}",
                Enabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Connections.Add(connection);
            await _dbContext.SaveChangesAsync();
            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connection");
            return null;
        }
    }

    /// <summary>
    /// Deletes a connection.
    /// </summary>
    public async Task<bool> DeleteConnectionAsync(Guid id)
    {
        try
        {
            var connection = await _dbContext.Connections.FindAsync(id);
            if (connection == null) return false;

            _dbContext.Connections.Remove(connection);
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting connection {ConnectionId}", id);
            return false;
        }
    }

    #endregion

    #region Tags

    /// <summary>
    /// Gets all tags, optionally filtered by connection.
    /// </summary>
    public async Task<List<TagMetadata>> GetTagsAsync(Guid? connectionId = null)
    {
        try
        {
            var query = _dbContext.TagMetadata.AsQueryable();
            if (connectionId.HasValue)
            {
                query = query.Where(t => t.ConnectionId == connectionId);
            }
            return await query.OrderBy(t => t.TagPath).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tags");
            return new List<TagMetadata>();
        }
    }

    #endregion

    #region Poll Groups

    /// <summary>
    /// Gets all poll groups.
    /// </summary>
    public async Task<List<PollGroup>> GetPollGroupsAsync()
    {
        try
        {
            return await _dbContext.PollGroups.OrderBy(p => p.Name).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting poll groups");
            return new List<PollGroup>();
        }
    }

    #endregion

    #region Units

    /// <summary>
    /// Gets all units.
    /// </summary>
    public async Task<List<UnitOfMeasure>> GetUnitsAsync()
    {
        try
        {
            return await _dbContext.UnitsOfMeasure.OrderBy(u => u.Name).ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting units");
            return new List<UnitOfMeasure>();
        }
    }

    #endregion

    #region Dashboard Stats

    /// <summary>
    /// Gets dashboard statistics.
    /// </summary>
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        try
        {
            return new DashboardStats
            {
                ActiveConnections = await _dbContext.Connections.CountAsync(c => c.Enabled),
                RunningFlows = await _dbContext.Flows.CountAsync(f => f.Deployed),
                SubscribedTags = await _dbContext.TagMetadata.CountAsync(t => t.IsSubscribed),
                TotalDashboards = await _dbContext.Dashboards.CountAsync()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return new DashboardStats();
        }
    }

    /// <summary>
    /// Gets recent activity items.
    /// </summary>
    public async Task<List<ActivityItem>> GetRecentActivityAsync(int limit = 10)
    {
        var activities = new List<ActivityItem>();

        try
        {
            // Get recent flows
            var recentFlows = await _dbContext.Flows
                .OrderByDescending(f => f.UpdatedAt)
                .Take(limit / 2)
                .ToListAsync();

            foreach (var flow in recentFlows)
            {
                activities.Add(new ActivityItem
                {
                    Type = "flow",
                    Title = $"Flow '{flow.Name}' saved",
                    Timestamp = flow.UpdatedAt
                });
            }

            // Get recent connections
            var recentConnections = await _dbContext.Connections
                .OrderByDescending(c => c.UpdatedAt)
                .Take(limit / 2)
                .ToListAsync();

            foreach (var conn in recentConnections)
            {
                activities.Add(new ActivityItem
                {
                    Type = "connection",
                    Title = $"Connection '{conn.Name}'",
                    Status = conn.Enabled ? "Active" : "Inactive",
                    Timestamp = conn.UpdatedAt
                });
            }

            return activities.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent activity");
            return new List<ActivityItem>();
        }
    }

    #endregion
}

/// <summary>
/// Dashboard statistics model.
/// </summary>
public class DashboardStats
{
    public int ActiveConnections { get; set; }
    public int RunningFlows { get; set; }
    public int SubscribedTags { get; set; }
    public int TotalDashboards { get; set; }
}

/// <summary>
/// Activity item model for dashboard.
/// </summary>
public class ActivityItem
{
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Status { get; set; }
    public DateTime Timestamp { get; set; }
}
