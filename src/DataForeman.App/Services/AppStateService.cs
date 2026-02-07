namespace DataForeman.App.Services;

/// <summary>
/// Service for managing global application state including current user and permissions.
/// Used by PermissionGuard and pages to check permissions.
/// </summary>
public class AppStateService
{
    private readonly UserService _userService;

    public AppStateService(UserService userService)
    {
        _userService = userService;
    }

    /// <summary>Event triggered when state changes.</summary>
    public event Action? OnChange;

    /// <summary>Current logged-in user.</summary>
    public AppUser? CurrentUser { get; private set; }

    /// <summary>Cached user permissions.</summary>
    public List<FeaturePermission> Permissions { get; private set; } = new();

    /// <summary>Set the current user and load their permissions.</summary>
    public void SetCurrentUser(AppUser? user)
    {
        CurrentUser = user;
        if (user != null)
        {
            Permissions = _userService.GetUserPermissions(user.Id);
        }
        else
        {
            Permissions.Clear();
        }
        NotifyStateChanged();
    }

    /// <summary>
    /// Check if user has permission for a feature and operation.
    /// Mirrors the React can(feature, operation) pattern.
    /// </summary>
    public bool HasPermission(string feature, string operation = "read")
    {
        var permission = Permissions.FirstOrDefault(p => p.Feature == feature);
        if (permission == null) return false;

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
    /// Check if the current user is the owner of an item.
    /// Items with no OwnerId are considered owned by everyone (legacy items).
    /// </summary>
    public bool IsOwner(string? ownerId)
    {
        if (string.IsNullOrEmpty(ownerId)) return true; // Legacy items with no owner
        return CurrentUser?.Id == ownerId;
    }

    /// <summary>Clear all state (on logout).</summary>
    public void Clear()
    {
        CurrentUser = null;
        Permissions.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
