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
public class ChartsController : ControllerBase
{
    private readonly DataForemanDbContext _context;
    private readonly ILogger<ChartsController> _logger;

    public ChartsController(DataForemanDbContext context, ILogger<ChartsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCharts(
        [FromQuery] string scope = "all",
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var userId = GetUserIdFromClaims();
        

        IQueryable<ChartConfig> query = _context.ChartConfigs
            .Where(c => !c.IsDeleted);

        query = scope switch
        {
            "mine" => query.Where(c => c.UserId == userId),
            "shared" => query.Where(c => c.IsShared),
            "system" => query.Where(c => c.IsSystemChart),
            _ => query.Where(c => c.UserId == userId || c.IsShared)
        };

        var charts = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip(offset)
            .Take(Math.Min(limit, 200))
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.ChartType,
                c.TimeMode,
                c.CreatedAt,
                c.UpdatedAt,
                c.IsShared,
                c.IsSystemChart,
                IsOwner = c.UserId == userId,
                c.FolderId
            })
            .ToListAsync();

        return Ok(new { items = charts, limit, offset, count = charts.Count });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetChart(Guid id)
    {
        var userId = GetUserIdFromClaims();
        

        var chart = await _context.ChartConfigs
            .Where(c => c.Id == id && !c.IsDeleted && (c.UserId == userId || c.IsShared || c.IsSystemChart))
            .FirstOrDefaultAsync();

        if (chart == null)
        {
            return NotFound(new { error = "chart_not_found" });
        }

        return Ok(chart);
    }

    [HttpPost]
    public async Task<IActionResult> CreateChart([FromBody] CreateChartRequest request)
    {
        var userId = GetUserIdFromClaims();
        

        var chart = new ChartConfig
        {
            UserId = userId,
            Name = request.Name ?? "New Chart",
            Description = request.Description,
            ChartType = request.ChartType ?? "line",
            TimeMode = request.TimeMode ?? "fixed",
            TimeDuration = request.TimeDuration,
            TimeOffset = request.TimeOffset ?? 0,
            LiveEnabled = request.LiveEnabled,
            ShowTimeBadge = request.ShowTimeBadge ?? true,
            TimeFrom = request.TimeFrom,
            TimeTo = request.TimeTo,
            Options = request.Options ?? "{}",
            IsShared = request.IsShared,
            FolderId = request.FolderId
        };

        _context.ChartConfigs.Add(chart);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Chart {Id} created by user {UserId}", chart.Id, userId);

        return Ok(new { id = chart.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateChart(Guid id, [FromBody] UpdateChartRequest request)
    {
        var userId = GetUserIdFromClaims();
        

        var chart = await _context.ChartConfigs
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);

        if (chart == null)
        {
            return NotFound(new { error = "chart_not_found" });
        }

        if (request.Name != null) chart.Name = request.Name;
        if (request.Description != null) chart.Description = request.Description;
        if (request.ChartType != null) chart.ChartType = request.ChartType;
        if (request.TimeMode != null) chart.TimeMode = request.TimeMode;
        if (request.TimeDuration.HasValue) chart.TimeDuration = request.TimeDuration;
        if (request.TimeOffset.HasValue) chart.TimeOffset = request.TimeOffset.Value;
        if (request.LiveEnabled.HasValue) chart.LiveEnabled = request.LiveEnabled.Value;
        if (request.ShowTimeBadge.HasValue) chart.ShowTimeBadge = request.ShowTimeBadge.Value;
        if (request.TimeFrom.HasValue) chart.TimeFrom = request.TimeFrom;
        if (request.TimeTo.HasValue) chart.TimeTo = request.TimeTo;
        if (request.Options != null) chart.Options = request.Options;
        if (request.IsShared.HasValue) chart.IsShared = request.IsShared.Value;
        if (request.FolderId.HasValue) chart.FolderId = request.FolderId.Value;

        chart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteChart(Guid id)
    {
        var userId = GetUserIdFromClaims();
        

        var chart = await _context.ChartConfigs
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);

        if (chart == null)
        {
            return NotFound(new { error = "chart_not_found" });
        }

        chart.IsDeleted = true;
        chart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Chart {Id} deleted by user {UserId}", id, userId);

        return Ok(new { ok = true });
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
        return DefaultUserId;
    }
}

public record CreateChartRequest(
    string? Name,
    string? Description,
    string? ChartType,
    string? TimeMode,
    long? TimeDuration,
    long? TimeOffset,
    bool LiveEnabled = false,
    bool? ShowTimeBadge = true,
    DateTime? TimeFrom = null,
    DateTime? TimeTo = null,
    string? Options = null,
    bool IsShared = false,
    Guid? FolderId = null
);

public record UpdateChartRequest(
    string? Name,
    string? Description,
    string? ChartType,
    string? TimeMode,
    long? TimeDuration,
    long? TimeOffset,
    bool? LiveEnabled,
    bool? ShowTimeBadge,
    DateTime? TimeFrom,
    DateTime? TimeTo,
    string? Options,
    bool? IsShared,
    Guid? FolderId
);
