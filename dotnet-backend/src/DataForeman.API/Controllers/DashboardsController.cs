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
public class DashboardsController : ControllerBase
{
    private readonly DataForemanDbContext _context;
    private readonly ILogger<DashboardsController> _logger;

    public DashboardsController(DataForemanDbContext context, ILogger<DashboardsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboards(
        [FromQuery] string scope = "all",
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        IQueryable<Dashboard> query = _context.Dashboards
            .Where(d => !d.IsDeleted);

        query = scope switch
        {
            "mine" => query.Where(d => d.UserId == userId),
            "shared" => query.Where(d => d.IsShared),
            _ => query.Where(d => d.UserId == userId || d.IsShared)
        };

        var dashboards = await query
            .OrderByDescending(d => d.UpdatedAt)
            .Skip(offset)
            .Take(Math.Min(limit, 200))
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Description,
                d.CreatedAt,
                d.UpdatedAt,
                d.IsShared,
                IsOwner = d.UserId == userId,
                FolderId = d.FolderId
            })
            .ToListAsync();

        return Ok(new { items = dashboards, limit, offset, count = dashboards.Count });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDashboard(Guid id)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        var dashboard = await _context.Dashboards
            .Where(d => d.Id == id && !d.IsDeleted && (d.UserId == userId || d.IsShared))
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Description,
                d.Layout,
                d.Options,
                d.CreatedAt,
                d.UpdatedAt,
                d.IsShared,
                IsOwner = d.UserId == userId,
                d.FolderId
            })
            .FirstOrDefaultAsync();

        if (dashboard == null)
        {
            return NotFound(new { error = "dashboard_not_found" });
        }

        return Ok(dashboard);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDashboard([FromBody] CreateDashboardRequest request)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        var dashboard = new Dashboard
        {
            UserId = userId.Value,
            Name = request.Name ?? "New Dashboard",
            Description = request.Description,
            Layout = request.Layout ?? "{}",
            Options = request.Options ?? "{}",
            IsShared = request.IsShared,
            FolderId = request.FolderId
        };

        _context.Dashboards.Add(dashboard);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Dashboard {Id} created by user {UserId}", dashboard.Id, userId);

        return Ok(new { id = dashboard.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDashboard(Guid id, [FromBody] UpdateDashboardRequest request)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        var dashboard = await _context.Dashboards
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && !d.IsDeleted);

        if (dashboard == null)
        {
            return NotFound(new { error = "dashboard_not_found" });
        }

        if (request.Name != null) dashboard.Name = request.Name;
        if (request.Description != null) dashboard.Description = request.Description;
        if (request.Layout != null) dashboard.Layout = request.Layout;
        if (request.Options != null) dashboard.Options = request.Options;
        if (request.IsShared.HasValue) dashboard.IsShared = request.IsShared.Value;
        if (request.FolderId.HasValue) dashboard.FolderId = request.FolderId.Value;

        dashboard.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDashboard(Guid id)
    {
        var userId = GetUserIdFromClaims();
        if (userId == null) return Unauthorized();

        var dashboard = await _context.Dashboards
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && !d.IsDeleted);

        if (dashboard == null)
        {
            return NotFound(new { error = "dashboard_not_found" });
        }

        dashboard.IsDeleted = true;
        dashboard.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Dashboard {Id} deleted by user {UserId}", id, userId);

        return Ok(new { ok = true });
    }

    private Guid? GetUserIdFromClaims()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }
        return null;
    }
}

public record CreateDashboardRequest(
    string? Name,
    string? Description,
    string? Layout,
    string? Options,
    bool IsShared = false,
    Guid? FolderId = null
);

public record UpdateDashboardRequest(
    string? Name,
    string? Description,
    string? Layout,
    string? Options,
    bool? IsShared,
    Guid? FolderId
);
