namespace DataForeman.Auth;

/// <summary>
/// JWT authentication configuration options.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Secret key for signing tokens.
    /// </summary>
    public string Key { get; set; } = "DataForeman_Default_Secret_Key_Change_In_Production_123!";

    /// <summary>
    /// Token issuer.
    /// </summary>
    public string Issuer { get; set; } = "DataForeman";

    /// <summary>
    /// Token audience.
    /// </summary>
    public string Audience { get; set; } = "DataForeman";

    /// <summary>
    /// Access token expiration in minutes.
    /// </summary>
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Refresh token expiration in days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

/// <summary>
/// RBAC policy names for authorization.
/// </summary>
public static class Policies
{
    /// <summary>
    /// Policy for admin-only operations.
    /// </summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Policy for dashboard management.
    /// </summary>
    public const string DashboardManagement = "DashboardManagement";

    /// <summary>
    /// Policy for flow management.
    /// </summary>
    public const string FlowManagement = "FlowManagement";

    /// <summary>
    /// Policy for connectivity/device management.
    /// </summary>
    public const string ConnectivityManagement = "ConnectivityManagement";

    /// <summary>
    /// Policy for tag management.
    /// </summary>
    public const string TagManagement = "TagManagement";

    /// <summary>
    /// Policy for chart management.
    /// </summary>
    public const string ChartManagement = "ChartManagement";

    /// <summary>
    /// Policy for user management.
    /// </summary>
    public const string UserManagement = "UserManagement";

    /// <summary>
    /// Policy for read-only access.
    /// </summary>
    public const string ReadOnly = "ReadOnly";
}

/// <summary>
/// Feature permission identifiers.
/// </summary>
public static class Features
{
    public const string Dashboards = "dashboards";
    public const string Flows = "flows";
    public const string ChartComposer = "chart_composer";
    public const string ConnectivityDevices = "connectivity.devices";
    public const string ConnectivityTags = "connectivity.tags";
    public const string Diagnostics = "diagnostics";
    public const string Users = "users";
    public const string Admin = "admin";
}

/// <summary>
/// Permission types for CRUD operations.
/// </summary>
public static class PermissionTypes
{
    public const string Create = "create";
    public const string Read = "read";
    public const string Update = "update";
    public const string Delete = "delete";
}

/// <summary>
/// User roles.
/// </summary>
public static class Roles
{
    /// <summary>
    /// Administrator with full access.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Standard user with default permissions.
    /// </summary>
    public const string User = "User";

    /// <summary>
    /// Read-only viewer.
    /// </summary>
    public const string Viewer = "Viewer";

    /// <summary>
    /// Operator with limited write access.
    /// </summary>
    public const string Operator = "Operator";
}
