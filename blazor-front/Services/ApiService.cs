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

#endregion
