using System.Net.Http.Json;
using System.Text.Json;

namespace DataForeman.BlazorUI.Services;

/// <summary>
/// Service for communicating with the DataForeman backend API
/// </summary>
public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private string? _authToken;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public void SetAuthToken(string token)
    {
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAuthToken()
    {
        _authToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    #region Authentication

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { email, password });
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (data != null)
                {
                    return new LoginResult
                    {
                        Success = true,
                        Token = data.Token,
                        RefreshToken = data.Refresh,
                        User = data.User != null ? new UserInfo
                        {
                            Id = data.User.Id,
                            Email = data.User.Email,
                            DisplayName = data.User.DisplayName
                        } : null
                    };
                }
            }
            
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return new LoginResult { Success = false, Error = error?.Error ?? "Login failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return new LoginResult { Success = false, Error = "Login failed. Please check your connection." };
        }
    }

    public async Task<TokenRefreshResult> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/refresh", new { refresh = refreshToken });
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<RefreshResponse>();
                if (data != null)
                {
                    return new TokenRefreshResult
                    {
                        Success = true,
                        Token = data.Token,
                        RefreshToken = data.Refresh
                    };
                }
            }
            
            return new TokenRefreshResult { Success = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
            return new TokenRefreshResult { Success = false };
        }
    }

    public async Task LogoutAsync(string? refreshToken)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("api/auth/logout", new { refresh = refreshToken });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout API call failed");
        }
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CurrentUserResponse>("api/auth/me");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current user");
            return null;
        }
    }

    public async Task<RegisterResult> RegisterAsync(string email, string password, string? displayName)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", 
                new { email, password, displayName });
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (data != null)
                {
                    return new RegisterResult
                    {
                        Success = true,
                        Token = data.Token,
                        RefreshToken = data.Refresh,
                        User = data.User != null ? new UserInfo
                        {
                            Id = data.User.Id,
                            Email = data.User.Email,
                            DisplayName = data.User.DisplayName
                        } : null
                    };
                }
            }
            
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return new RegisterResult { Success = false, Error = error?.Error ?? "Registration failed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed");
            return new RegisterResult { Success = false, Error = "Registration failed. Please try again." };
        }
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/password", 
                new { currentPassword, newPassword });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password change failed");
            return false;
        }
    }

    #endregion

    #region Users

    public async Task<UsersListResponse?> GetUsersAsync(int limit = 50, int offset = 0)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UsersListResponse>(
                $"api/users?limit={limit}&offset={offset}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users");
            return null;
        }
    }

    public async Task<UserDetailResponse?> GetUserAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserDetailResponse>($"api/users/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user {UserId}", id);
            return null;
        }
    }

    public async Task<bool> UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/users/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {UserId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/users/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId}", id);
            return false;
        }
    }

    public async Task<UserPermissionsResponse?> GetUserPermissionsAsync(Guid userId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserPermissionsResponse>(
                $"api/users/{userId}/permissions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permissions for user {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> UpdateUserPermissionsAsync(Guid userId, List<UserPermissionInfo> permissions)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/users/{userId}/permissions", 
                new { permissions });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update permissions for user {UserId}", userId);
            return false;
        }
    }

    #endregion

    #region Flows

    public async Task<FlowsResponse?> GetFlowsAsync(string scope = "all", int limit = 50, int offset = 0)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<FlowsResponse>(
                $"api/flows?scope={scope}&limit={limit}&offset={offset}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get flows");
            return null;
        }
    }

    public async Task<FlowDetailResponse?> GetFlowAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<FlowDetailResponse>($"api/flows/{id}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get flow {FlowId}", id);
            return null;
        }
    }

    public async Task<CreateFlowResponse?> CreateFlowAsync(CreateFlowRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/flows", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CreateFlowResponse>();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create flow");
            return null;
        }
    }

    public async Task<bool> UpdateFlowAsync(Guid id, UpdateFlowRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/flows/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update flow {FlowId}", id);
            return false;
        }
    }

    public async Task<DeployResponse?> DeployFlowAsync(Guid id, bool deploy)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/flows/{id}/deploy", new { deploy });
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DeployResponse>();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy flow {FlowId}", id);
            return null;
        }
    }

    public async Task<bool> DeleteFlowAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/flows/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete flow {FlowId}", id);
            return false;
        }
    }

    public async Task<NodeTypesResponse?> GetNodeTypesAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<NodeTypesResponse>("api/flows/node-types");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get node types");
            return null;
        }
    }

    #endregion

    #region Charts

    public async Task<ChartsResponse?> GetChartsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ChartsResponse>("api/charts");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get charts");
            return null;
        }
    }

    public async Task<ChartDetailResponse?> GetChartAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ChartDetailResponse>($"api/charts/{id}");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chart {ChartId}", id);
            return null;
        }
    }

    public async Task<CreateChartResponse?> CreateChartAsync(CreateChartRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/charts", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<CreateChartResponse>();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chart");
            return null;
        }
    }

    public async Task<bool> UpdateChartAsync(Guid id, UpdateChartRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/charts/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update chart {ChartId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteChartAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/charts/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chart {ChartId}", id);
            return false;
        }
    }

    #endregion

    #region Connectivity

    public async Task<ConnectionsResponse?> GetConnectionsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ConnectionsResponse>("api/connectivity/connections");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connections");
            return null;
        }
    }

    public async Task<TagsResponse?> GetTagsAsync(Guid? connectionId = null)
    {
        try
        {
            var url = connectionId.HasValue 
                ? $"api/connectivity/tags?connectionId={connectionId}" 
                : "api/connectivity/tags";
            var response = await _httpClient.GetFromJsonAsync<TagsResponse>(url);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tags");
            return null;
        }
    }

    public async Task<PollGroupsResponse?> GetPollGroupsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<PollGroupsResponse>("api/connectivity/poll-groups");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get poll groups");
            return null;
        }
    }

    public async Task<UnitsResponse?> GetUnitsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<UnitsResponse>("api/connectivity/units");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get units");
            return null;
        }
    }

    #endregion

    #region Dashboards

    public async Task<DashboardsResponse?> GetDashboardsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<DashboardsResponse>("api/dashboards");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboards");
            return null;
        }
    }

    #endregion
}

#region DTOs

// Flow DTOs
public record FlowsResponse(List<FlowItem> Items, int Limit, int Offset, int Count);
public record FlowDetailResponse(FlowItem Flow);
public record CreateFlowResponse(Guid Id);
public record DeployResponse(bool Ok, bool Deployed);
public record NodeTypesResponse(List<NodeTypeItem> NodeTypes, int Count);

public class FlowItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool Deployed { get; set; }
    public bool Shared { get; set; }
    public bool TestMode { get; set; }
    public string ExecutionMode { get; set; } = "continuous";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsOwner { get; set; }
    public string Definition { get; set; } = "{}";
}

public class NodeTypeItem
{
    public string Type { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Section { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";
}

public record CreateFlowRequest(
    string? Name,
    string? Description,
    string? Definition,
    string? ExecutionMode,
    int? ScanRateMs
);

public record UpdateFlowRequest(
    string? Name,
    string? Description,
    string? Definition,
    string? ExecutionMode,
    int? ScanRateMs,
    bool? Shared,
    bool? TestMode
);

// Chart DTOs
public record ChartsResponse(List<ChartItem> Items, int Count);
public record ChartDetailResponse(ChartItem Chart);
public record CreateChartResponse(Guid Id);

public class ChartItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ChartType { get; set; } = "Line";
    public string? Definition { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record CreateChartRequest(string Name, string ChartType, string? Definition);
public record UpdateChartRequest(string? Name, string? ChartType, string? Definition);

// Connectivity DTOs
public record ConnectionsResponse(List<ConnectionItem> Connections, int Count);
public record TagsResponse(List<TagItem> Tags, int Count);
public record PollGroupsResponse(List<PollGroupItem> PollGroups, int Count);
public record UnitsResponse(List<UnitItem> Units, int Count);

public class ConnectionItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public int TagCount { get; set; }
}

public class TagItem
{
    public Guid Id { get; set; }
    public string Path { get; set; } = "";
    public string? DataType { get; set; }
    public string? Unit { get; set; }
    public object? CurrentValue { get; set; }
}

public class PollGroupItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int PollRateMs { get; set; }
}

public class UnitItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Symbol { get; set; }
}

// Dashboard DTOs
public record DashboardsResponse(List<DashboardItem> Dashboards, int Count);

public class DashboardItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Definition { get; set; }
}

// Auth DTOs
public class LoginResponse
{
    public string Token { get; set; } = "";
    public string Refresh { get; set; } = "";
    public UserResponse? User { get; set; }
}

public class RefreshResponse
{
    public string Token { get; set; } = "";
    public string Refresh { get; set; } = "";
}

public class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class CurrentUserResponse
{
    public Guid Sub { get; set; }
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class ErrorResponse
{
    public string? Error { get; set; }
}

public class TokenRefreshResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
}

public class RegisterResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public UserInfo? User { get; set; }
    public string? Error { get; set; }
}

// User DTOs
public record UsersListResponse(List<UserListItem> Users, int Count, int Limit, int Offset);
public record UserDetailResponse(UserListItem User);
public record UserPermissionsResponse(List<UserPermissionInfo> Permissions);

public class UserListItem
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public record UpdateUserRequest(
    string? DisplayName,
    string? Email,
    bool? IsActive,
    string? Password
);

#endregion
