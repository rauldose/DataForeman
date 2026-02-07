using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DataForeman.Api.Data;
using DataForeman.Api.Services;
using DataForeman.Shared.DTOs;
using DataForeman.Shared.Models;

namespace DataForeman.Api.Controllers;

[ApiController]
[Route("api/dashboards")]
[Authorize]
public class DashboardsController : ControllerBase
{
    private readonly DataForemanDbContext _db;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<DashboardsController> _logger;

    public DashboardsController(DataForemanDbContext db, IPermissionService permissionService, ILogger<DashboardsController> logger)
    {
        _db = db;
        _permissionService = permissionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboards()
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "dashboards", "read"))
        {
            return Forbid();
        }

        var dashboards = await _db.Dashboards
            .Where(d => !d.IsDeleted && (d.UserId == userId || d.IsShared))
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new DashboardDto(
                d.Id,
                d.UserId,
                d.FolderId,
                d.Name,
                d.Description,
                d.IsShared,
                d.Layout,
                d.Options,
                d.CreatedAt,
                d.UpdatedAt
            ))
            .ToListAsync();

        return Ok(new { dashboards });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDashboard(Guid id)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "dashboards", "read"))
        {
            return Forbid();
        }

        var dashboard = await _db.Dashboards
            .Where(d => d.Id == id && !d.IsDeleted && (d.UserId == userId || d.IsShared))
            .Select(d => new DashboardDto(
                d.Id,
                d.UserId,
                d.FolderId,
                d.Name,
                d.Description,
                d.IsShared,
                d.Layout,
                d.Options,
                d.CreatedAt,
                d.UpdatedAt
            ))
            .FirstOrDefaultAsync();

        if (dashboard == null)
        {
            return NotFound(new { error = "not_found" });
        }

        return Ok(dashboard);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDashboard([FromBody] CreateDashboardRequest request)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "dashboards", "create"))
        {
            return Forbid();
        }

        var dashboard = new Dashboard
        {
            UserId = userId,
            FolderId = request.FolderId,
            Name = request.Name,
            Description = request.Description,
            IsShared = request.IsShared ?? false,
            Layout = request.Layout ?? "{}",
            Options = request.Options ?? "{}"
        };

        _db.Dashboards.Add(dashboard);
        await _db.SaveChangesAsync();

        return Created($"/api/dashboards/{dashboard.Id}", new { id = dashboard.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateDashboard(Guid id, [FromBody] UpdateDashboardRequest request)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "dashboards", "update"))
        {
            return Forbid();
        }

        var dashboard = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && !d.IsDeleted);
        if (dashboard == null)
        {
            return NotFound(new { error = "not_found" });
        }

        if (request.Name != null) dashboard.Name = request.Name;
        if (request.Description != null) dashboard.Description = request.Description;
        if (request.FolderId != null) dashboard.FolderId = request.FolderId;
        if (request.IsShared != null) dashboard.IsShared = request.IsShared.Value;
        if (request.Layout != null) dashboard.Layout = request.Layout;
        if (request.Options != null) dashboard.Options = request.Options;
        dashboard.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDashboard(Guid id)
    {
        var userId = GetUserId();
        if (!await _permissionService.CanAsync(userId, "dashboards", "delete"))
        {
            return Forbid();
        }

        var dashboard = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && !d.IsDeleted);
        if (dashboard == null)
        {
            return NotFound(new { error = "not_found" });
        }

        dashboard.IsDeleted = true;
        dashboard.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdStr!);
    }
}
