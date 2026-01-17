using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DataForeman.Core.Entities;
using DataForeman.Infrastructure.Data;

namespace DataForeman.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConnectivityController : ControllerBase
{
    private readonly DataForemanDbContext _context;
    private readonly ILogger<ConnectivityController> _logger;

    public ConnectivityController(DataForemanDbContext context, ILogger<ConnectivityController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Connections
    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections([FromQuery] bool includeDeleted = false)
    {
        var query = _context.Connections.AsQueryable();
        
        if (!includeDeleted)
        {
            query = query.Where(c => c.DeletedAt == null);
        }

        var connections = await query
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Type,
                c.Enabled,
                c.IsSystemConnection,
                c.CreatedAt,
                c.UpdatedAt,
                TagCount = c.Tags.Count(t => !t.IsDeleted)
            })
            .ToListAsync();

        return Ok(new { connections });
    }

    [HttpGet("connections/{id}")]
    public async Task<IActionResult> GetConnection(Guid id)
    {
        var connection = await _context.Connections
            .Where(c => c.Id == id && c.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (connection == null)
        {
            return NotFound(new { error = "connection_not_found" });
        }

        return Ok(connection);
    }

    [HttpPost("connections")]
    public async Task<IActionResult> CreateConnection([FromBody] CreateConnectionRequest request)
    {
        if (string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.Type))
        {
            return BadRequest(new { error = "Name and type are required" });
        }

        var existingConnection = await _context.Connections
            .FirstOrDefaultAsync(c => c.Name == request.Name && c.DeletedAt == null);

        if (existingConnection != null)
        {
            return Conflict(new { error = "Connection with this name already exists" });
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

        _context.Connections.Add(connection);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Connection {Id} ({Name}) created", connection.Id, connection.Name);

        return Ok(new { id = connection.Id });
    }

    [HttpPut("connections/{id}")]
    public async Task<IActionResult> UpdateConnection(Guid id, [FromBody] UpdateConnectionRequest request)
    {
        var connection = await _context.Connections
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);

        if (connection == null)
        {
            return NotFound(new { error = "connection_not_found" });
        }

        if (request.Name != null) connection.Name = request.Name;
        if (request.Enabled.HasValue) connection.Enabled = request.Enabled.Value;
        if (request.ConfigData != null) connection.ConfigData = request.ConfigData;
        if (request.MaxTagsPerGroup.HasValue) connection.MaxTagsPerGroup = request.MaxTagsPerGroup.Value;
        if (request.MaxConcurrentConnections.HasValue) connection.MaxConcurrentConnections = request.MaxConcurrentConnections.Value;

        connection.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpDelete("connections/{id}")]
    public async Task<IActionResult> DeleteConnection(Guid id)
    {
        var connection = await _context.Connections
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null);

        if (connection == null)
        {
            return NotFound(new { error = "connection_not_found" });
        }

        // Soft delete
        connection.DeletedAt = DateTime.UtcNow;
        connection.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Connection {Id} ({Name}) deleted", id, connection.Name);

        return Ok(new { ok = true });
    }

    // Tags
    [HttpGet("tags")]
    public async Task<IActionResult> GetTags(
        [FromQuery] Guid? connectionId = null,
        [FromQuery] bool subscribedOnly = false,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.TagMetadata
            .Include(t => t.PollGroup)
            .Include(t => t.Unit)
            .Where(t => !t.IsDeleted);

        if (connectionId.HasValue)
        {
            query = query.Where(t => t.ConnectionId == connectionId.Value);
        }

        if (subscribedOnly)
        {
            query = query.Where(t => t.IsSubscribed);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => 
                t.TagPath.Contains(search) || 
                (t.TagName != null && t.TagName.Contains(search)) ||
                (t.Description != null && t.Description.Contains(search)));
        }

        var tags = await query
            .OrderBy(t => t.TagPath)
            .Take(Math.Min(limit, 1000))
            .Select(t => new
            {
                t.TagId,
                t.ConnectionId,
                t.TagPath,
                t.TagName,
                t.DriverType,
                t.DataType,
                t.IsSubscribed,
                t.PollGroupId,
                PollGroupName = t.PollGroup != null ? t.PollGroup.Name : null,
                t.UnitId,
                UnitSymbol = t.Unit != null ? t.Unit.Symbol : null,
                t.Description,
                t.OnChangeEnabled,
                t.CreatedAt,
                t.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { tags, count = tags.Count });
    }

    [HttpGet("tags/{tagId}")]
    public async Task<IActionResult> GetTag(int tagId)
    {
        var tag = await _context.TagMetadata
            .Include(t => t.Connection)
            .Include(t => t.PollGroup)
            .Include(t => t.Unit)
            .FirstOrDefaultAsync(t => t.TagId == tagId && !t.IsDeleted);

        if (tag == null)
        {
            return NotFound(new { error = "tag_not_found" });
        }

        return Ok(tag);
    }

    [HttpPost("tags")]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request)
    {
        var connection = await _context.Connections
            .FirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.DeletedAt == null);

        if (connection == null)
        {
            return BadRequest(new { error = "Connection not found" });
        }

        var existingTag = await _context.TagMetadata
            .FirstOrDefaultAsync(t => 
                t.ConnectionId == request.ConnectionId && 
                t.TagPath == request.TagPath && 
                t.DriverType == request.DriverType &&
                !t.IsDeleted);

        if (existingTag != null)
        {
            return Conflict(new { error = "Tag already exists" });
        }

        var tag = new TagMetadata
        {
            ConnectionId = request.ConnectionId,
            TagPath = request.TagPath,
            TagName = request.TagName,
            DriverType = request.DriverType,
            DataType = request.DataType,
            IsSubscribed = request.IsSubscribed,
            PollGroupId = request.PollGroupId ?? 5,
            UnitId = request.UnitId,
            Description = request.Description,
            OnChangeEnabled = request.OnChangeEnabled,
            OnChangeDeadband = request.OnChangeDeadband ?? 0,
            OnChangeDeadbandType = request.OnChangeDeadbandType ?? "absolute",
            OnChangeHeartbeatMs = request.OnChangeHeartbeatMs ?? 60000
        };

        _context.TagMetadata.Add(tag);
        await _context.SaveChangesAsync();

        return Ok(new { tagId = tag.TagId });
    }

    [HttpPut("tags/{tagId}")]
    public async Task<IActionResult> UpdateTag(int tagId, [FromBody] UpdateTagRequest request)
    {
        var tag = await _context.TagMetadata
            .FirstOrDefaultAsync(t => t.TagId == tagId && !t.IsDeleted);

        if (tag == null)
        {
            return NotFound(new { error = "tag_not_found" });
        }

        if (request.TagName != null) tag.TagName = request.TagName;
        if (request.Description != null) tag.Description = request.Description;
        if (request.IsSubscribed.HasValue) tag.IsSubscribed = request.IsSubscribed.Value;
        if (request.PollGroupId.HasValue) tag.PollGroupId = request.PollGroupId.Value;
        if (request.UnitId.HasValue) tag.UnitId = request.UnitId;
        if (request.OnChangeEnabled.HasValue) tag.OnChangeEnabled = request.OnChangeEnabled.Value;
        if (request.OnChangeDeadband.HasValue) tag.OnChangeDeadband = request.OnChangeDeadband.Value;
        if (request.OnChangeDeadbandType != null) tag.OnChangeDeadbandType = request.OnChangeDeadbandType;
        if (request.OnChangeHeartbeatMs.HasValue) tag.OnChangeHeartbeatMs = request.OnChangeHeartbeatMs.Value;

        tag.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpDelete("tags/{tagId}")]
    public async Task<IActionResult> DeleteTag(int tagId)
    {
        var tag = await _context.TagMetadata
            .FirstOrDefaultAsync(t => t.TagId == tagId && !t.IsDeleted);

        if (tag == null)
        {
            return NotFound(new { error = "tag_not_found" });
        }

        tag.IsDeleted = true;
        tag.DeletedAt = DateTime.UtcNow;
        tag.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    // Poll Groups
    [HttpGet("poll-groups")]
    public async Task<IActionResult> GetPollGroups()
    {
        var pollGroups = await _context.PollGroups
            .OrderBy(pg => pg.PollRateMs)
            .ToListAsync();

        return Ok(new { pollGroups });
    }

    // Units of Measure
    [HttpGet("units")]
    public async Task<IActionResult> GetUnits([FromQuery] string? category = null)
    {
        var query = _context.UnitsOfMeasure.AsQueryable();

        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(u => u.Category == category);
        }

        var units = await query
            .OrderBy(u => u.Category)
            .ThenBy(u => u.Name)
            .ToListAsync();

        return Ok(new { units });
    }

    [HttpGet("units/categories")]
    public async Task<IActionResult> GetUnitCategories()
    {
        var categories = await _context.UnitsOfMeasure
            .Select(u => u.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return Ok(new { categories });
    }
}

public record CreateConnectionRequest(
    string Name,
    string Type,
    bool Enabled = true,
    string? ConfigData = null,
    int? MaxTagsPerGroup = null,
    int? MaxConcurrentConnections = null
);

public record UpdateConnectionRequest(
    string? Name,
    bool? Enabled,
    string? ConfigData,
    int? MaxTagsPerGroup,
    int? MaxConcurrentConnections
);

public record CreateTagRequest(
    Guid ConnectionId,
    string TagPath,
    string DriverType,
    string? TagName = null,
    string? DataType = null,
    bool IsSubscribed = false,
    int? PollGroupId = null,
    int? UnitId = null,
    string? Description = null,
    bool OnChangeEnabled = false,
    float? OnChangeDeadband = null,
    string? OnChangeDeadbandType = null,
    int? OnChangeHeartbeatMs = null
);

public record UpdateTagRequest(
    string? TagName,
    string? Description,
    bool? IsSubscribed,
    int? PollGroupId,
    int? UnitId,
    bool? OnChangeEnabled,
    float? OnChangeDeadband,
    string? OnChangeDeadbandType,
    int? OnChangeHeartbeatMs
);
