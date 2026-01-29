namespace DataForeman.BlazorUI.Services;

/// <summary>
/// Service for managing global application state.
/// Provides centralized state management for user, permissions, and telemetry cache.
/// </summary>
public class AppStateService
{
    private readonly ApiService _apiService;
    private readonly ILogger<AppStateService> _logger;

    public AppStateService(ApiService apiService, ILogger<AppStateService> logger)
    {
        _apiService = apiService;
        _logger = logger;
    }

    /// <summary>
    /// Event triggered when state changes.
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// Current logged-in user.
    /// </summary>
    public UserInfo? CurrentUser { get; private set; }

    /// <summary>
    /// User permissions cache.
    /// </summary>
    public List<UserPermissionInfo> Permissions { get; private set; } = new();

    /// <summary>
    /// Telemetry data cache for real-time values.
    /// </summary>
    public Dictionary<string, TelemetryValue> TelemetryCache { get; private set; } = new();

    /// <summary>
    /// Active connections count.
    /// </summary>
    public int ActiveConnectionsCount { get; private set; }

    /// <summary>
    /// Running flows count.
    /// </summary>
    public int RunningFlowsCount { get; private set; }

    /// <summary>
    /// Subscribed tags count.
    /// </summary>
    public int SubscribedTagsCount { get; private set; }

    /// <summary>
    /// Set the current user.
    /// </summary>
    public void SetCurrentUser(UserInfo? user)
    {
        CurrentUser = user;
        NotifyStateChanged();
    }

    /// <summary>
    /// Load user permissions from API.
    /// </summary>
    public async Task LoadPermissionsAsync(Guid userId)
    {
        try
        {
            var response = await _apiService.GetUserPermissionsAsync(userId);
            if (response != null)
            {
                Permissions = response.Permissions;
                NotifyStateChanged();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load permissions for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Check if user has permission for a feature and operation.
    /// </summary>
    public bool HasPermission(string feature, string operation = "read")
    {
        var permission = Permissions.FirstOrDefault(p => p.Feature == feature);
        if (permission == null)
        {
            return false;
        }

        return operation.ToLower() switch
        {
            "create" => permission.CanCreate,
            "read" => permission.CanRead,
            "update" => permission.CanUpdate,
            "delete" => permission.CanDelete,
            _ => false
        };
    }

    /// <summary>
    /// Update telemetry value in cache.
    /// </summary>
    public void UpdateTelemetry(string tagPath, object? value, DateTime timestamp)
    {
        TelemetryCache[tagPath] = new TelemetryValue
        {
            Value = value,
            Timestamp = timestamp
        };
        NotifyStateChanged();
    }

    /// <summary>
    /// Update system statistics.
    /// </summary>
    public void UpdateStats(int connections, int flows, int tags)
    {
        ActiveConnectionsCount = connections;
        RunningFlowsCount = flows;
        SubscribedTagsCount = tags;
        NotifyStateChanged();
    }

    /// <summary>
    /// Clear all state (e.g., on logout).
    /// </summary>
    public void Clear()
    {
        CurrentUser = null;
        Permissions.Clear();
        TelemetryCache.Clear();
        ActiveConnectionsCount = 0;
        RunningFlowsCount = 0;
        SubscribedTagsCount = 0;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

/// <summary>
/// User permission information.
/// </summary>
public class UserPermissionInfo
{
    public string Feature { get; set; } = "";
    public bool CanCreate { get; set; }
    public bool CanRead { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
}

/// <summary>
/// Real-time telemetry value.
/// </summary>
public class TelemetryValue
{
    public object? Value { get; set; }
    public DateTime Timestamp { get; set; }
}
