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
public class ChartsController : ControllerBase
{
    private readonly DataForemanDbContext _db;

    public ChartsController(DataForemanDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCharts()
    {
        var charts = await _db.ChartConfigs
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ChartConfigDto(
                c.Id,
                c.UserId,
                c.FolderId,
                c.Name,
                c.Description,
                c.ChartType,
                c.IsSystemChart,
                c.IsShared,
                c.TimeMode,
                c.TimeDuration,
                c.TimeOffset,
                c.LiveEnabled,
                c.ShowTimeBadge,
                c.TimeFrom,
                c.TimeTo,
                null, // Don't include options in list
                c.CreatedAt,
                c.UpdatedAt
            ))
            .ToListAsync();

        return Ok(new { Charts = charts });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetChart(Guid id)
    {
        var chart = await _db.ChartConfigs.FindAsync(id);
        if (chart == null)
        {
            return NotFound(new { error = "Chart not found" });
        }

        return Ok(new ChartConfigDto(
            chart.Id,
            chart.UserId,
            chart.FolderId,
            chart.Name,
            chart.Description,
            chart.ChartType,
            chart.IsSystemChart,
            chart.IsShared,
            chart.TimeMode,
            chart.TimeDuration,
            chart.TimeOffset,
            chart.LiveEnabled,
            chart.ShowTimeBadge,
            chart.TimeFrom,
            chart.TimeTo,
            chart.Options,
            chart.CreatedAt,
            chart.UpdatedAt
        ));
    }

    [HttpPost]
    public async Task<IActionResult> CreateChart([FromBody] CreateChartRequest request)
    {
        var chart = new ChartConfig
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            FolderId = request.FolderId,
            ChartType = request.ChartType ?? "line",
            IsShared = request.IsShared ?? false,
            TimeMode = request.TimeMode ?? "rolling",
            TimeDuration = request.TimeDuration ?? 3600000,
            TimeOffset = request.TimeOffset ?? 0,
            LiveEnabled = request.LiveEnabled ?? false,
            ShowTimeBadge = request.ShowTimeBadge ?? true,
            TimeFrom = request.TimeFrom,
            TimeTo = request.TimeTo,
            Options = request.Options ?? "{}",
            IsSystemChart = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.ChartConfigs.Add(chart);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetChart), new { id = chart.Id }, new ChartConfigDto(
            chart.Id,
            chart.UserId,
            chart.FolderId,
            chart.Name,
            chart.Description,
            chart.ChartType,
            chart.IsSystemChart,
            chart.IsShared,
            chart.TimeMode,
            chart.TimeDuration,
            chart.TimeOffset,
            chart.LiveEnabled,
            chart.ShowTimeBadge,
            chart.TimeFrom,
            chart.TimeTo,
            chart.Options,
            chart.CreatedAt,
            chart.UpdatedAt
        ));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateChart(Guid id, [FromBody] UpdateChartRequest request)
    {
        var chart = await _db.ChartConfigs.FindAsync(id);
        if (chart == null)
        {
            return NotFound(new { error = "Chart not found" });
        }

        if (request.Name != null) chart.Name = request.Name;
        if (request.Description != null) chart.Description = request.Description;
        if (request.FolderId != null) chart.FolderId = request.FolderId;
        if (request.ChartType != null) chart.ChartType = request.ChartType;
        if (request.IsShared != null) chart.IsShared = request.IsShared.Value;
        if (request.TimeMode != null) chart.TimeMode = request.TimeMode;
        if (request.TimeDuration != null) chart.TimeDuration = request.TimeDuration;
        if (request.TimeOffset != null) chart.TimeOffset = request.TimeOffset.Value;
        if (request.LiveEnabled != null) chart.LiveEnabled = request.LiveEnabled.Value;
        if (request.ShowTimeBadge != null) chart.ShowTimeBadge = request.ShowTimeBadge.Value;
        if (request.TimeFrom != null) chart.TimeFrom = request.TimeFrom;
        if (request.TimeTo != null) chart.TimeTo = request.TimeTo;
        if (request.Options != null) chart.Options = request.Options;

        chart.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ChartConfigDto(
            chart.Id,
            chart.UserId,
            chart.FolderId,
            chart.Name,
            chart.Description,
            chart.ChartType,
            chart.IsSystemChart,
            chart.IsShared,
            chart.TimeMode,
            chart.TimeDuration,
            chart.TimeOffset,
            chart.LiveEnabled,
            chart.ShowTimeBadge,
            chart.TimeFrom,
            chart.TimeTo,
            chart.Options,
            chart.CreatedAt,
            chart.UpdatedAt
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteChart(Guid id)
    {
        var chart = await _db.ChartConfigs.FindAsync(id);
        if (chart == null)
        {
            return NotFound(new { error = "Chart not found" });
        }

        _db.ChartConfigs.Remove(chart);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("query")]
    public async Task<IActionResult> QueryData([FromBody] ChartQueryRequest request)
    {
        // TODO: Implement actual time-series data query from tag history
        // For now, return empty results as the time-series storage is not yet implemented
        return Ok(new { Items = new List<object>() });
    }
}

public record ChartQueryRequest(
    List<Guid> TagIds,
    DateTime From,
    DateTime To,
    int? Limit
);
