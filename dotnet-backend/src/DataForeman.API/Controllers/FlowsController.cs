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
                f.FolderId,
                f.IsTemplate,
                f.TemplateFlowId,
                DeploymentStatus = !f.Deployed ? "not-deployed" :
                    (f.DeployedDefinition == null ? "up-to-date" :
                    (f.DeployedDefinition != f.Definition ? "modified" : "up-to-date")),
                HasChanges = f.Deployed && f.DeployedDefinition != null && f.DeployedDefinition != f.Definition
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
            // Store snapshot of definition when deploying
            flow.DeployedDefinition = flow.Definition;
            flow.DeployedAt = DateTime.UtcNow;
            
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

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] int limit = 50)
    {
        var userId = GetUserIdFromClaims();

        var templates = await _context.Flows
            .Where(f => f.IsTemplate && (f.OwnerUserId == userId || f.Shared))
            .OrderByDescending(f => f.UpdatedAt)
            .Take(Math.Min(limit, 100))
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Description,
                f.ExposedParameters,
                f.TemplateInputs,
                f.TemplateOutputs,
                f.CreatedAt,
                f.UpdatedAt,
                IsOwner = f.OwnerUserId == userId,
                UsedByCount = _context.Flows.Count(flow => flow.TemplateFlowId == f.Id)
            })
            .ToListAsync();

        return Ok(new { templates, count = templates.Count });
    }

    [HttpPost("{id}/mark-template")]
    public async Task<IActionResult> MarkAsTemplate(Guid id, [FromBody] MarkTemplateRequest request)
    {
        var userId = GetUserIdFromClaims();

        var flow = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && f.OwnerUserId == userId);

        if (flow == null)
        {
            return NotFound(new { error = "flow_not_found" });
        }

        flow.IsTemplate = request.IsTemplate;
        if (request.TemplateInputs != null) flow.TemplateInputs = request.TemplateInputs;
        if (request.TemplateOutputs != null) flow.TemplateOutputs = request.TemplateOutputs;
        if (request.ExposedParameters != null) flow.ExposedParameters = request.ExposedParameters;
        flow.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Flow {Id} marked as template: {IsTemplate}", id, request.IsTemplate);

        return Ok(new { ok = true, isTemplate = flow.IsTemplate });
    }

    [HttpPost("from-template")]
    public async Task<IActionResult> CreateFromTemplate([FromBody] CreateFromTemplateRequest request)
    {
        var userId = GetUserIdFromClaims();

        var template = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == request.TemplateFlowId && f.IsTemplate && (f.OwnerUserId == userId || f.Shared));

        if (template == null)
        {
            return NotFound(new { error = "template_not_found" });
        }

        var flow = new Flow
        {
            OwnerUserId = userId,
            Name = request.Name ?? $"{template.Name} (Copy)",
            Description = request.Description ?? template.Description,
            ExecutionMode = template.ExecutionMode,
            ScanRateMs = template.ScanRateMs,
            Definition = template.Definition, // Copy the template definition
            TemplateFlowId = template.Id,
            ExposedParameters = request.Parameters ?? template.ExposedParameters,
            FolderId = request.FolderId
        };

        _context.Flows.Add(flow);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Flow {Id} created from template {TemplateId} by user {UserId}", flow.Id, template.Id, userId);

        return Ok(new { id = flow.Id, templateId = template.Id });
    }

    [HttpGet("templates/{id}/usage")]
    public async Task<IActionResult> GetTemplateUsage(Guid id)
    {
        var userId = GetUserIdFromClaims();

        var template = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && f.IsTemplate && (f.OwnerUserId == userId || f.Shared));

        if (template == null)
        {
            return NotFound(new { error = "template_not_found" });
        }

        var usedByFlows = await _context.Flows
            .Where(f => f.TemplateFlowId == id)
            .Include(f => f.Owner)
            .OrderByDescending(f => f.UpdatedAt)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.Description,
                Owner = f.Owner != null ? f.Owner.Email : "Unknown",
                f.Deployed,
                DeploymentStatus = f.Deployed
                    ? (f.DeployedDefinition != null && f.DeployedDefinition != f.Definition ? "modified" : "up-to-date")
                    : "not-deployed",
                HasChanges = f.Deployed && f.DeployedDefinition != null && f.DeployedDefinition != f.Definition,
                f.DeployedAt,
                f.UpdatedAt,
                f.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            template = new { id = template.Id, name = template.Name },
            usedBy = usedByFlows,
            count = usedByFlows.Count
        });
    }

    [HttpPut("templates/{id}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateRequest request)
    {
        var userId = GetUserIdFromClaims();

        var template = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && f.IsTemplate && f.OwnerUserId == userId);

        if (template == null)
        {
            return NotFound(new { error = "template_not_found" });
        }

        if (request.Name != null) template.Name = request.Name;
        if (request.Description != null) template.Description = request.Description;
        if (request.Definition != null) template.Definition = request.Definition;
        if (request.TemplateInputs != null) template.TemplateInputs = request.TemplateInputs;
        if (request.TemplateOutputs != null) template.TemplateOutputs = request.TemplateOutputs;
        if (request.ExposedParameters != null) template.ExposedParameters = request.ExposedParameters;
        if (request.Shared.HasValue) template.Shared = request.Shared.Value;

        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Template {Id} updated by user {UserId}", id, userId);

        return Ok(new { ok = true });
    }

    [HttpGet("{id}/deployment-status")]
    public async Task<IActionResult> GetDeploymentStatus(Guid id)
    {
        var userId = GetUserIdFromClaims();

        var flow = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && (f.OwnerUserId == userId || f.Shared));

        if (flow == null)
        {
            return NotFound(new { error = "flow_not_found" });
        }

        string deploymentStatus;
        bool hasChanges = false;
        int changeCount = 0;

        if (!flow.Deployed)
        {
            deploymentStatus = "not-deployed";
        }
        else if (flow.DeployedDefinition == null)
        {
            deploymentStatus = "up-to-date"; // Backward compatibility
        }
        else if (flow.DeployedDefinition != flow.Definition)
        {
            deploymentStatus = "modified";
            hasChanges = true;
            // Simple change detection: compare string lengths as proxy
            changeCount = Math.Abs(flow.Definition.Length - flow.DeployedDefinition.Length);
        }
        else
        {
            deploymentStatus = "up-to-date";
        }

        return Ok(new
        {
            flowId = flow.Id,
            name = flow.Name,
            deployed = flow.Deployed,
            deploymentStatus,
            hasChanges,
            deployedAt = flow.DeployedAt,
            lastModified = flow.UpdatedAt,
            changeCount,
            isTemplate = flow.IsTemplate,
            templateFlowId = flow.TemplateFlowId
        });
    }

    [HttpGet("{id}/deployment-diff")]
    public async Task<IActionResult> GetDeploymentDiff(Guid id)
    {
        var userId = GetUserIdFromClaims();

        var flow = await _context.Flows
            .FirstOrDefaultAsync(f => f.Id == id && (f.OwnerUserId == userId || f.Shared));

        if (flow == null)
        {
            return NotFound(new { error = "flow_not_found" });
        }

        if (!flow.Deployed || flow.DeployedDefinition == null)
        {
            return Ok(new
            {
                hasDiff = false,
                message = flow.Deployed ? "No deployed definition snapshot available" : "Flow is not deployed"
            });
        }

        var hasDiff = flow.DeployedDefinition != flow.Definition;

        return Ok(new
        {
            hasDiff,
            deployedDefinition = flow.DeployedDefinition,
            currentDefinition = flow.Definition,
            deployedAt = flow.DeployedAt,
            lastModified = flow.UpdatedAt
        });
    }

    [HttpGet("node-types")]
    public async Task<IActionResult> GetNodeTypes()
    {
        // Get template flows to add them as node types
        var userId = GetUserIdFromClaims();
        var templates = await _context.Flows
            .Where(f => f.IsTemplate && (f.OwnerUserId == userId || f.Shared))
            .Select(f => new NodeTypeInfo
            {
                Type = $"template-{f.Id}",
                DisplayName = f.Name,
                Category = "TEMPLATES",
                Section = "CUSTOM",
                Icon = "📦",
                Color = "#00bcd4",
                TemplateId = f.Id,
                Description = f.Description,
                Inputs = f.TemplateInputs,
                Outputs = f.TemplateOutputs,
                Parameters = f.ExposedParameters
            })
            .ToListAsync();

        // Return the available node types for the Flow Studio
        var baseNodeTypes = new List<NodeTypeInfo>
        {
            new() { Type = "trigger-manual", DisplayName = "Manual Trigger", Category = "TRIGGERS", Section = "BASIC", Icon = "▶️", Color = "#4caf50" },
            new() { Type = "trigger-schedule", DisplayName = "Schedule Trigger", Category = "TRIGGERS", Section = "BASIC", Icon = "⏰", Color = "#4caf50" },
            new() { Type = "tag-input", DisplayName = "Tag Input", Category = "TAG_OPERATIONS", Section = "INPUT", Icon = "📥", Color = "#2196f3" },
            new() { Type = "tag-output", DisplayName = "Tag Output", Category = "TAG_OPERATIONS", Section = "OUTPUT", Icon = "📤", Color = "#ff9800" },
            new() { Type = "math-add", DisplayName = "Add", Category = "DATA_PROCESSING", Section = "MATH", Icon = "➕", Color = "#9c27b0" },
            new() { Type = "math-subtract", DisplayName = "Subtract", Category = "DATA_PROCESSING", Section = "MATH", Icon = "➖", Color = "#9c27b0" },
            new() { Type = "math-multiply", DisplayName = "Multiply", Category = "DATA_PROCESSING", Section = "MATH", Icon = "✖️", Color = "#9c27b0" },
            new() { Type = "math-divide", DisplayName = "Divide", Category = "DATA_PROCESSING", Section = "MATH", Icon = "➗", Color = "#9c27b0" },
            new() { Type = "compare-equal", DisplayName = "Equal", Category = "LOGIC", Section = "COMPARISON", Icon = "=", Color = "#607d8b" },
            new() { Type = "compare-greater", DisplayName = "Greater Than", Category = "LOGIC", Section = "COMPARISON", Icon = ">", Color = "#607d8b" },
            new() { Type = "compare-less", DisplayName = "Less Than", Category = "LOGIC", Section = "COMPARISON", Icon = "<", Color = "#607d8b" },
            new() { Type = "logic-if", DisplayName = "If", Category = "LOGIC", Section = "CONTROL", Icon = "🔀", Color = "#795548" },
            new() { Type = "csharp", DisplayName = "C# Script", Category = "DATA_PROCESSING", Section = "SCRIPTING", Icon = "💻", Color = "#673ab7" },
            new() { Type = "debug-log", DisplayName = "Debug Log", Category = "OUTPUT", Section = "BASIC", Icon = "📝", Color = "#f44336" }
        };

        var allNodeTypes = baseNodeTypes.Concat(templates).ToList();

        return Ok(new { nodeTypes = allNodeTypes, count = allNodeTypes.Count });
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

public record MarkTemplateRequest(
    bool IsTemplate,
    string? TemplateInputs,
    string? TemplateOutputs,
    string? ExposedParameters
);

public record CreateFromTemplateRequest(
    Guid TemplateFlowId,
    string? Name,
    string? Description,
    string? Parameters,
    Guid? FolderId
);

public record UpdateTemplateRequest(
    string? Name,
    string? Description,
    string? Definition,
    string? TemplateInputs,
    string? TemplateOutputs,
    string? ExposedParameters,
    bool? Shared
);

public class NodeTypeInfo
{
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string? Description { get; set; }
    public string? Inputs { get; set; }
    public string? Outputs { get; set; }
    public string? Parameters { get; set; }
}
