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
            .Include(c => c.Series)
                .ThenInclude(s => s.Tag)
            .Include(c => c.Axes)
            .Where(c => c.Id == id && !c.IsDeleted && (c.UserId == userId || c.IsShared || c.IsSystemChart))
            .FirstOrDefaultAsync();

        if (chart == null)
        {
            return NotFound(new { error = "chart_not_found" });
        }

        return Ok(new
        {
            chart.Id,
            chart.Name,
            chart.Description,
            chart.ChartType,
            chart.TimeMode,
            chart.TimeDuration,
            chart.TimeOffset,
            chart.LiveEnabled,
            chart.RefreshInterval,
            chart.EnableLegend,
            chart.LegendPosition,
            chart.EnableTooltip,
            chart.EnableZoom,
            chart.EnablePan,
            chart.TimeFrom,
            chart.TimeTo,
            chart.Options,
            chart.CreatedAt,
            chart.UpdatedAt,
            Series = chart.Series.Select(s => new
            {
                s.Id,
                s.TagId,
                TagPath = s.Tag?.TagPath,
                TagName = s.Tag?.TagName,
                s.Label,
                s.Color,
                s.SeriesType,
                s.AxisIndex,
                s.DisplayOrder,
                s.Visible,
                s.LineWidth,
                s.ShowMarkers,
                s.MarkerSize,
                s.Opacity
            }),
            Axes = chart.Axes.Select(a => new
            {
                a.Id,
                a.AxisIndex,
                a.AxisType,
                a.Position,
                a.Label,
                a.Min,
                a.Max,
                a.AutoScale,
                a.ShowGridLines,
                a.GridLineStyle,
                a.LabelFormat,
                a.Logarithmic
            })
        });
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

    [HttpGet("data")]
    public async Task<IActionResult> GetChartData(
        [FromQuery] int[] tagIds,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 10000)
    {
        if (tagIds == null || tagIds.Length == 0)
        {
            return BadRequest(new { error = "tagIds_required" });
        }

        var fromTime = from ?? DateTime.UtcNow.AddHours(-24);
        var toTime = to ?? DateTime.UtcNow;

        var data = await _context.TagValues
            .Where(tv => tagIds.Contains(tv.TagId) && tv.Timestamp >= fromTime && tv.Timestamp <= toTime)
            .OrderBy(tv => tv.Timestamp)
            .Take(Math.Min(limit, 50000))
            .Select(tv => new
            {
                tv.TagId,
                tv.Timestamp,
                tv.NumericValue,
                tv.StringValue,
                tv.BooleanValue,
                tv.Quality
            })
            .ToListAsync();

        // Group by tag ID for easier consumption
        var groupedData = data.GroupBy(d => d.TagId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(v => new
                {
                    timestamp = v.Timestamp,
                    value = v.NumericValue ?? (v.BooleanValue.HasValue ? (v.BooleanValue.Value ? 1.0 : 0.0) : 0.0),
                    quality = v.Quality
                }).ToList()
            );

        return Ok(new { data = groupedData, from = fromTime, to = toTime, count = data.Count });
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetAvailableTags()
    {
        var tags = await _context.TagMetadata
            .Where(t => t.IsSubscribed && !t.IsDeleted)
            .Select(t => new
            {
                t.TagId,
                t.TagPath,
                t.TagName,
                t.Description,
                t.DataType,
                Unit = t.Unit != null ? new { t.Unit.Symbol, t.Unit.Name } : null
            })
            .ToListAsync();

        return Ok(new { tags, count = tags.Count });
    }
    
    [HttpPost("{chartId}/series")]
    public async Task<IActionResult> AddSeriesToChart(Guid chartId, [FromBody] AddSeriesRequest request)
    {
        var userId = GetUserIdFromClaims();
        var chart = await _context.ChartConfigs
            .FirstOrDefaultAsync(c => c.Id == chartId && c.UserId == userId && !c.IsDeleted);

        if (chart == null)
        {
            return NotFound(new { error = "chart_not_found" });
        }

        var series = new ChartSeries
        {
            ChartId = chartId,
            TagId = request.TagId,
            Label = request.Label ?? $"Series {request.TagId}",
            Color = request.Color ?? "#4caf50",
            SeriesType = request.SeriesType ?? "line",
            AxisIndex = request.AxisIndex ?? 0,
            DisplayOrder = request.DisplayOrder ?? 0,
            Visible = request.Visible ?? true,
            LineWidth = request.LineWidth,
            ShowMarkers = request.ShowMarkers ?? false,
            MarkerSize = request.MarkerSize,
            Opacity = request.Opacity ?? 1.0
        };

        _context.ChartSeries.Add(series);
        chart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { id = series.Id });
    }
    
    [HttpDelete("{chartId}/series/{seriesId}")]
    public async Task<IActionResult> RemoveSeriesFromChart(Guid chartId, Guid seriesId)
    {
        var userId = GetUserIdFromClaims();
        var series = await _context.ChartSeries
            .Include(s => s.Chart)
            .FirstOrDefaultAsync(s => s.Id == seriesId && s.ChartId == chartId && s.Chart!.UserId == userId);

        if (series == null)
        {
            return NotFound(new { error = "series_not_found" });
        }

        _context.ChartSeries.Remove(series);
        series.Chart!.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { ok = true });
    }
    
    [HttpPost("{chartId}/axes")]
    public async Task<IActionResult> AddAxisToChart(Guid chartId, [FromBody] AddAxisRequest request)
    {
        var userId = GetUserIdFromClaims();
        var chart = await _context.ChartConfigs
            .FirstOrDefaultAsync(c => c.Id == chartId && c.UserId == userId && !c.IsDeleted);

        if (chart == null)
        {
            return NotFound(new { error = "chart_not_found" });
        }

        var axis = new ChartAxis
        {
            ChartId = chartId,
            AxisIndex = request.AxisIndex,
            AxisType = request.AxisType ?? "Y",
            Position = request.Position ?? "left",
            Label = request.Label,
            Min = request.Min,
            Max = request.Max,
            AutoScale = request.AutoScale ?? true,
            ShowGridLines = request.ShowGridLines ?? true,
            GridLineStyle = request.GridLineStyle ?? "solid",
            LabelFormat = request.LabelFormat,
            Logarithmic = request.Logarithmic ?? false
        };

        _context.ChartAxes.Add(axis);
        chart.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { id = axis.Id });
    }

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

public record AddSeriesRequest(
    int TagId,
    string? Label,
    string? Color,
    string? SeriesType,
    int? AxisIndex,
    int? DisplayOrder,
    bool? Visible,
    double? LineWidth,
    bool? ShowMarkers,
    double? MarkerSize,
    double? Opacity
);

public record AddAxisRequest(
    int AxisIndex,
    string? AxisType,
    string? Position,
    string? Label,
    double? Min,
    double? Max,
    bool? AutoScale,
    bool? ShowGridLines,
    string? GridLineStyle,
    string? LabelFormat,
    bool? Logarithmic
);
