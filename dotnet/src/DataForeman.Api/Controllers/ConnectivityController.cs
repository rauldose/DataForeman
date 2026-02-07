using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DataForeman.Api.Data;
using DataForeman.Api.Services;
using DataForeman.Shared.DTOs;
using DataForeman.Shared.Models;

namespace DataForeman.Api.Controllers;

[ApiController]
[Route("api/connectivity")]
[Authorize]
public class ConnectivityController : ControllerBase
{
    private readonly DataForemanDbContext _db;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ConnectivityController> _logger;

    public ConnectivityController(DataForemanDbContext db, IPermissionService permissionService, ILogger<ConnectivityController> logger)
    {
        _db = db;
        _permissionService = permissionService;
        _logger = logger;
    }

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections()
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "connectivity.devices", "read"))
        {
            return Forbid();
        }

        var connections = await _db.Connections
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .Select(c => new ConnectionDto(
                c.Id,
                c.Name,
                c.Type,
                c.Enabled,
                c.ConfigData,
                c.IsSystemConnection,
                c.MaxTagsPerGroup,
                c.MaxConcurrentConnections,
                c.CreatedAt,
                c.UpdatedAt
            ))
            .ToListAsync();

        return Ok(new { connections });
    }

    [HttpGet("connections/{id:guid}")]
    public async Task<IActionResult> GetConnection(Guid id)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "connectivity.devices", "read"))
        {
            return Forbid();
        }

        var connection = await _db.Connections
            .Where(c => c.Id == id && c.DeletedAt == null)
            .Select(c => new ConnectionDto(
                c.Id,
                c.Name,
                c.Type,
                c.Enabled,
                c.ConfigData,
                c.IsSystemConnection,
                c.MaxTagsPerGroup,
                c.MaxConcurrentConnections,
                c.CreatedAt,
                c.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        if (connection == null)
        {
            return NotFound(new { error = "not_found" });
        }

        return Ok(connection);
    }

    [HttpPost("connections")]
    public async Task<IActionResult> CreateConnection([FromBody] CreateConnectionRequest request)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "connectivity.devices", "create"))
        {
            return Forbid();
        }

        var connection = new Connection
        {
            Name = request.Name,
            Type = request.Type,
            Enabled = request.Enabled,
            ConfigData = request.ConfigData ?? "{}",
            MaxTagsPerGroup = request.MaxTagsPerGroup ?? 500,
            MaxConcurrentConnections = request.MaxConcurrentConnections ?? 8
        };

        _db.Connections.Add(connection);
        await _db.SaveChangesAsync();

        return Created($"/api/connectivity/connections/{connection.Id}", new { id = connection.Id });
    }

    [HttpPut("connections/{id:guid}")]
    public async Task<IActionResult> UpdateConnection(Guid id, [FromBody] UpdateConnectionRequest request)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "connectivity.devices", "update"))
        {
            return Forbid();
        }

        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);
        if (connection == null)
        {
            return NotFound(new { error = "not_found" });
        }

        if (request.Name != null) connection.Name = request.Name;
        if (request.Type != null) connection.Type = request.Type;
        if (request.Enabled != null) connection.Enabled = request.Enabled.Value;
        if (request.ConfigData != null) connection.ConfigData = request.ConfigData;
        if (request.MaxTagsPerGroup != null) connection.MaxTagsPerGroup = request.MaxTagsPerGroup;
        if (request.MaxConcurrentConnections != null) connection.MaxConcurrentConnections = request.MaxConcurrentConnections;
        connection.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpDelete("connections/{id:guid}")]
    public async Task<IActionResult> DeleteConnection(Guid id)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "connectivity.devices", "delete"))
        {
            return Forbid();
        }

        var connection = await _db.Connections.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);
        if (connection == null)
        {
            return NotFound(new { error = "not_found" });
        }

        connection.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    // Tags endpoints
    [HttpGet("tags")]
    public async Task<IActionResult> GetTags([FromQuery] Guid? connectionId)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "connectivity.tags", "read"))
        {
            return Forbid();
        }

        var query = _db.TagMetadata.Where(t => !t.IsDeleted);
        if (connectionId != null)
        {
            query = query.Where(t => t.ConnectionId == connectionId);
        }

        var tags = await query
            .OrderBy(t => t.TagPath)
            .Select(t => new TagMetadataDto(
                t.TagId,
                t.ConnectionId,
                t.DriverType,
                t.TagPath,
                t.TagName,
                t.IsSubscribed,
                t.PollGroupId,
                t.DataType,
                t.UnitId,
                t.Description,
                t.Metadata,
                t.OnChangeEnabled,
                t.OnChangeDeadband,
                t.OnChangeDeadbandType,
                t.OnChangeHeartbeatMs,
                t.CreatedAt,
                t.UpdatedAt
            ))
            .ToListAsync();

        return Ok(new { tags });
    }

    [HttpPost("tags")]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "connectivity.tags", "create"))
        {
            return Forbid();
        }

        var tag = new TagMetadata
        {
            ConnectionId = request.ConnectionId,
            DriverType = request.DriverType,
            TagPath = request.TagPath,
            TagName = request.TagName,
            IsSubscribed = request.IsSubscribed,
            PollGroupId = request.PollGroupId ?? 5,
            DataType = request.DataType,
            UnitId = request.UnitId,
            Description = request.Description,
            OnChangeEnabled = request.OnChangeEnabled ?? false,
            OnChangeDeadband = request.OnChangeDeadband ?? 0,
            OnChangeDeadbandType = request.OnChangeDeadbandType ?? "absolute",
            OnChangeHeartbeatMs = request.OnChangeHeartbeatMs ?? 60000
        };

        _db.TagMetadata.Add(tag);
        await _db.SaveChangesAsync();

        return Created($"/api/connectivity/tags/{tag.TagId}", new { tag_id = tag.TagId });
    }

    [HttpPut("tags/{id:int}")]
    public async Task<IActionResult> UpdateTag(int id, [FromBody] UpdateTagRequest request)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "connectivity.tags", "update"))
        {
            return Forbid();
        }

        var tag = await _db.TagMetadata.FirstOrDefaultAsync(t => t.TagId == id && !t.IsDeleted);
        if (tag == null)
        {
            return NotFound(new { error = "not_found" });
        }

        if (request.TagName != null) tag.TagName = request.TagName;
        if (request.IsSubscribed != null) tag.IsSubscribed = request.IsSubscribed.Value;
        if (request.PollGroupId != null) tag.PollGroupId = request.PollGroupId.Value;
        if (request.UnitId != null) tag.UnitId = request.UnitId;
        if (request.Description != null) tag.Description = request.Description;
        if (request.OnChangeEnabled != null) tag.OnChangeEnabled = request.OnChangeEnabled.Value;
        if (request.OnChangeDeadband != null) tag.OnChangeDeadband = request.OnChangeDeadband.Value;
        if (request.OnChangeDeadbandType != null) tag.OnChangeDeadbandType = request.OnChangeDeadbandType;
        if (request.OnChangeHeartbeatMs != null) tag.OnChangeHeartbeatMs = request.OnChangeHeartbeatMs.Value;
        tag.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdStr!);
    }
}
