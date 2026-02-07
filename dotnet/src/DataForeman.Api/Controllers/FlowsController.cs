using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataForeman.Api.Data;
using DataForeman.Shared.DTOs;
using DataForeman.Shared.Models;

namespace DataForeman.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FlowsController : ControllerBase
{
    private readonly DataForemanDbContext _db;

    public FlowsController(DataForemanDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetFlows()
    {
        var flows = await _db.Flows
            .OrderByDescending(f => f.UpdatedAt)
            .Select(f => new FlowDto(
                f.Id,
                f.Name,
                f.Description,
                f.OwnerUserId,
                f.FolderId,
                f.Deployed,
                f.Shared,
                f.TestMode,
                f.ExecutionMode,
                f.ScanRateMs,
                f.LogsEnabled,
                f.LogsRetentionDays,
                null, // Don't include full definition in list
                f.CreatedAt,
                f.UpdatedAt
            ))
            .ToListAsync();

        return Ok(new { Flows = flows });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFlow(Guid id)
    {
        var flow = await _db.Flows.FindAsync(id);
        if (flow == null)
        {
            return NotFound(new { error = "Flow not found" });
        }

        return Ok(new FlowDto(
            flow.Id,
            flow.Name,
            flow.Description,
            flow.OwnerUserId,
            flow.FolderId,
            flow.Deployed,
            flow.Shared,
            flow.TestMode,
            flow.ExecutionMode,
            flow.ScanRateMs,
            flow.LogsEnabled,
            flow.LogsRetentionDays,
            flow.Definition,
            flow.CreatedAt,
            flow.UpdatedAt
        ));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFlow([FromBody] CreateFlowRequest request)
    {
        var flow = new Flow
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            FolderId = request.FolderId,
            Shared = request.Shared ?? false,
            ExecutionMode = request.ExecutionMode ?? "manual",
            ScanRateMs = request.ScanRateMs ?? 1000,
            LogsEnabled = request.LogsEnabled ?? true,
            LogsRetentionDays = request.LogsRetentionDays ?? 30,
            Definition = request.Definition ?? "{}",
            Deployed = false,
            TestMode = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Flows.Add(flow);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetFlow), new { id = flow.Id }, new FlowDto(
            flow.Id,
            flow.Name,
            flow.Description,
            flow.OwnerUserId,
            flow.FolderId,
            flow.Deployed,
            flow.Shared,
            flow.TestMode,
            flow.ExecutionMode,
            flow.ScanRateMs,
            flow.LogsEnabled,
            flow.LogsRetentionDays,
            flow.Definition,
            flow.CreatedAt,
            flow.UpdatedAt
        ));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateFlow(Guid id, [FromBody] UpdateFlowRequest request)
    {
        var flow = await _db.Flows.FindAsync(id);
        if (flow == null)
        {
            return NotFound(new { error = "Flow not found" });
        }

        if (request.Name != null) flow.Name = request.Name;
        if (request.Description != null) flow.Description = request.Description;
        if (request.FolderId != null) flow.FolderId = request.FolderId;
        if (request.Shared != null) flow.Shared = request.Shared.Value;
        if (request.Deployed != null) flow.Deployed = request.Deployed.Value;
        if (request.TestMode != null) flow.TestMode = request.TestMode.Value;
        if (request.ExecutionMode != null) flow.ExecutionMode = request.ExecutionMode;
        if (request.ScanRateMs != null) flow.ScanRateMs = request.ScanRateMs.Value;
        if (request.LogsEnabled != null) flow.LogsEnabled = request.LogsEnabled.Value;
        if (request.LogsRetentionDays != null) flow.LogsRetentionDays = request.LogsRetentionDays.Value;
        if (request.Definition != null) flow.Definition = request.Definition;

        flow.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new FlowDto(
            flow.Id,
            flow.Name,
            flow.Description,
            flow.OwnerUserId,
            flow.FolderId,
            flow.Deployed,
            flow.Shared,
            flow.TestMode,
            flow.ExecutionMode,
            flow.ScanRateMs,
            flow.LogsEnabled,
            flow.LogsRetentionDays,
            flow.Definition,
            flow.CreatedAt,
            flow.UpdatedAt
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFlow(Guid id)
    {
        var flow = await _db.Flows.FindAsync(id);
        if (flow == null)
        {
            return NotFound(new { error = "Flow not found" });
        }

        _db.Flows.Remove(flow);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/deploy")]
    public async Task<IActionResult> DeployFlow(Guid id, [FromQuery] bool deployed = true)
    {
        var flow = await _db.Flows.FindAsync(id);
        if (flow == null)
        {
            return NotFound(new { error = "Flow not found" });
        }

        flow.Deployed = deployed;
        if (deployed)
        {
            flow.TestMode = false; // Exit test mode when deploying
        }
        flow.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { deployed = flow.Deployed });
    }
}
