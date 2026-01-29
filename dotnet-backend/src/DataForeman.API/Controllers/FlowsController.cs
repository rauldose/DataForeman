using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DataForeman.Core.Entities;
using DataForeman.Infrastructure.Data;

namespace DataForeman.API.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class FlowsController : ControllerBase
{
    private readonly DataForemanDbContext _context;
    private readonly ILogger<FlowsController> _logger;

    public FlowsController(DataForemanDbContext context, ILogger<FlowsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetFlows(
        [FromQuery] string scope = "all",
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var userId = GetUserIdFromClaims();
        

        IQueryable<Flow> query = _context.Flows;

        query = scope switch
        {
            "mine" => query.Where(f => f.OwnerUserId == userId),
            "shared" => query.Where(f => f.Shared),
            "deployed" => query.Where(f => f.Deployed),
            _ => query.Where(f => f.OwnerUserId == userId || f.Shared)
        };

        var flows = await query
            .OrderByDescending(f => f.UpdatedAt)
            .Skip(offset)
            .Take(Math.Min(limit, 200))
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Description,
                f.Deployed,
                f.Shared,
                f.TestMode,
                f.ExecutionMode,
                f.CreatedAt,
                f.UpdatedAt,
                IsOwner = f.OwnerUserId == userId,
                f.FolderId
            })
            .ToListAsync();

        return Ok(new { items = flows, limit, offset, count = flows.Count });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetFlow(Guid id)
    {
        var userId = GetUserIdFromClaims();
        

        var flow = await _context.Flows
            .Where(f => f.Id == id && (f.OwnerUserId == userId || f.Shared))
            .FirstOrDefaultAsync();

        if (flow == null)
        {
            return NotFound(new { error = "flow_not_found" });
        }

        return Ok(new { flow });
    }

    [HttpPost]
    public async Task<IActionResult> CreateFlow([FromBody] CreateFlowRequest request)
    {
        var userId = GetUserIdFromClaims();
        

        var flow = new Flow
        {
            OwnerUserId = userId,
            Name = request.Name ?? "New Flow",
            Description = request.Description,
            ExecutionMode = request.ExecutionMode ?? "continuous",
            ScanRateMs = request.ScanRateMs ?? 1000,
            Definition = request.Definition ?? "{}",
            FolderId = request.FolderId
        };

        _context.Flows.Add(flow);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Flow {Id} created by user {UserId}", flow.Id, userId);

        return Ok(new { id = flow.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFlow(Guid id, [FromBody] UpdateFlowRequest request)
    {
        var userId = GetUserIdFromClaims();
        

        var flow = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerUserId == userId);

        if (flow == null)
        {
            return NotFound(new { error = "flow_not_found" });
        }

        if (request.Name != null) flow.Name = request.Name;
        if (request.Description != null) flow.Description = request.Description;
        if (request.Definition != null) flow.Definition = request.Definition;
        if (request.ExecutionMode != null) flow.ExecutionMode = request.ExecutionMode;
        if (request.ScanRateMs.HasValue) flow.ScanRateMs = request.ScanRateMs.Value;
        if (request.Shared.HasValue) flow.Shared = request.Shared.Value;
        if (request.TestMode.HasValue) flow.TestMode = request.TestMode.Value;
        if (request.TestDisableWrites.HasValue) flow.TestDisableWrites = request.TestDisableWrites.Value;
        if (request.TestAutoExit.HasValue) flow.TestAutoExit = request.TestAutoExit.Value;
        if (request.TestAutoExitMinutes.HasValue) flow.TestAutoExitMinutes = request.TestAutoExitMinutes.Value;
        if (request.LogsEnabled.HasValue) flow.LogsEnabled = request.LogsEnabled.Value;
        if (request.LogsRetentionDays.HasValue) flow.LogsRetentionDays = request.LogsRetentionDays.Value;
        if (request.ExposedParameters != null) flow.ExposedParameters = request.ExposedParameters;
        if (request.FolderId.HasValue) flow.FolderId = request.FolderId.Value;

        flow.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpPost("{id}/deploy")]
    public async Task<IActionResult> DeployFlow(Guid id, [FromBody] DeployRequest request)
    {
        var userId = GetUserIdFromClaims();
        

        var flow = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerUserId == userId);

        if (flow == null)
        {
            return NotFound(new { error = "flow_not_found" });
        }

        flow.Deployed = request.Deploy;
        flow.UpdatedAt = DateTime.UtcNow;

        if (request.Deploy)
        {
            // Create a new session when deploying
            var session = new FlowSession
            {
                FlowId = flow.Id,
                Status = "active"
            };
            _context.FlowSessions.Add(session);

            _logger.LogInformation("Flow {Id} deployed by user {UserId}", id, userId);
        }
        else
        {
            // Stop active session when undeploying
            var activeSession = await _context.FlowSessions
                .FirstOrDefaultAsync(s => s.FlowId == id && s.Status == "active");

            if (activeSession != null)
            {
                activeSession.Status = "stopped";
                activeSession.StoppedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Flow {Id} undeployed by user {UserId}", id, userId);
        }

        await _context.SaveChangesAsync();

        return Ok(new { ok = true, deployed = flow.Deployed });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFlow(Guid id)
    {
        var userId = GetUserIdFromClaims();
        

        var flow = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerUserId == userId);

        if (flow == null)
        {
            return NotFound(new { error = "flow_not_found" });
        }

        _context.Flows.Remove(flow);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Flow {Id} deleted by user {UserId}", id, userId);

        return Ok(new { ok = true });
    }

    [HttpGet("{id}/executions")]
    public async Task<IActionResult> GetFlowExecutions(Guid id, [FromQuery] int limit = 50)
    {
        var userId = GetUserIdFromClaims();
        

        var flow = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && (f.OwnerUserId == userId || f.Shared));

        if (flow == null)
        {
            return NotFound(new { error = "flow_not_found" });
        }

        var executions = await _context.FlowExecutions
            .Where(e => e.FlowId == id)
            .OrderByDescending(e => e.StartedAt)
            .Take(Math.Min(limit, 100))
            .Select(e => new
            {
                e.Id,
                e.Status,
                e.StartedAt,
                e.CompletedAt,
                e.ExecutionTimeMs,
                e.TriggerNodeId
            })
            .ToListAsync();

        return Ok(new { executions });
    }

    [HttpGet("node-types")]
    public IActionResult GetNodeTypes()
    {
        // Return the available node types for the Flow Studio
        var nodeTypes = new[]
        {
            new { type = "trigger-manual", displayName = "Manual Trigger", category = "TRIGGERS", section = "BASIC", icon = "▶️", color = "#4caf50" },
            new { type = "trigger-schedule", displayName = "Schedule Trigger", category = "TRIGGERS", section = "BASIC", icon = "⏰", color = "#4caf50" },
            new { type = "tag-input", displayName = "Tag Input", category = "TAG_OPERATIONS", section = "INPUT", icon = "📥", color = "#2196f3" },
            new { type = "tag-output", displayName = "Tag Output", category = "TAG_OPERATIONS", section = "OUTPUT", icon = "📤", color = "#ff9800" },
            new { type = "math-add", displayName = "Add", category = "DATA_PROCESSING", section = "MATH", icon = "➕", color = "#9c27b0" },
            new { type = "math-subtract", displayName = "Subtract", category = "DATA_PROCESSING", section = "MATH", icon = "➖", color = "#9c27b0" },
            new { type = "math-multiply", displayName = "Multiply", category = "DATA_PROCESSING", section = "MATH", icon = "✖️", color = "#9c27b0" },
            new { type = "math-divide", displayName = "Divide", category = "DATA_PROCESSING", section = "MATH", icon = "➗", color = "#9c27b0" },
            new { type = "compare-equal", displayName = "Equal", category = "LOGIC", section = "COMPARISON", icon = "=", color = "#607d8b" },
            new { type = "compare-greater", displayName = "Greater Than", category = "LOGIC", section = "COMPARISON", icon = ">", color = "#607d8b" },
            new { type = "compare-less", displayName = "Less Than", category = "LOGIC", section = "COMPARISON", icon = "<", color = "#607d8b" },
            new { type = "logic-if", displayName = "If", category = "LOGIC", section = "CONTROL", icon = "🔀", color = "#795548" },
            new { type = "debug-log", displayName = "Debug Log", category = "OUTPUT", section = "BASIC", icon = "📝", color = "#f44336" }
        };

        return Ok(new { nodeTypes, count = nodeTypes.Length });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _context.NodeCategories
            .Include(c => c.Sections)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                key = c.CategoryKey,
                displayName = c.DisplayName,
                icon = c.Icon,
                description = c.Description,
                sections = c.Sections.OrderBy(s => s.DisplayOrder).Select(s => new
                {
                    key = s.SectionKey,
                    displayName = s.DisplayName,
                    description = s.Description
                })
            })
            .ToListAsync();

        return Ok(new { categories });
    }

    // Default user ID for anonymous access (single-user local application)
    private static readonly Guid DefaultUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private Guid GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        // Return default user ID for anonymous access
        return DefaultUserId;
    }
}

public record CreateFlowRequest(
    string? Name,
    string? Description,
    string? Definition,
    string? ExecutionMode,
    int? ScanRateMs,
    Guid? FolderId
);

public record UpdateFlowRequest(
    string? Name,
    string? Description,
    string? Definition,
    string? ExecutionMode,
    int? ScanRateMs,
    bool? Shared,
    bool? TestMode,
    bool? TestDisableWrites,
    bool? TestAutoExit,
    int? TestAutoExitMinutes,
    bool? LogsEnabled,
    int? LogsRetentionDays,
    string? ExposedParameters,
    Guid? FolderId
);

public record DeployRequest(bool Deploy);
