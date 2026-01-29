using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataForeman.Core.Entities;
using DataForeman.Infrastructure.Data;

namespace DataForeman.API.Controllers;

/// <summary>
/// Controller for background job management.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly DataForemanDbContext _context;
    private readonly ILogger<JobsController> _logger;

    public JobsController(DataForemanDbContext context, ILogger<JobsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all jobs with optional filtering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetJobs(
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.Jobs.AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(j => j.Status == status);
        }

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(j => j.Type == type);
        }

        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(offset)
            .Take(Math.Min(limit, 100))
            .Select(j => new
            {
                j.Id,
                j.Type,
                j.Status,
                j.Progress,
                j.CreatedAt,
                j.StartedAt,
                j.FinishedAt,
                j.Attempt,
                j.MaxAttempts
            })
            .ToListAsync();

        return Ok(new { items = jobs, limit, offset, count = jobs.Count });
    }

    /// <summary>
    /// Get a specific job by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await _context.Jobs.FindAsync(id);

        if (job == null)
        {
            return NotFound(new { error = "job_not_found" });
        }

        return Ok(job);
    }

    /// <summary>
    /// Create a new job.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] CreateJobRequest request)
    {
        var job = new Job
        {
            Type = request.Type,
            Params = request.Params,
            MaxAttempts = request.MaxAttempts ?? 1,
            RunAt = request.RunAt
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Job {Id} ({Type}) created", job.Id, job.Type);

        return Ok(new { id = job.Id });
    }

    /// <summary>
    /// Cancel a job.
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelJob(Guid id)
    {
        var job = await _context.Jobs.FindAsync(id);

        if (job == null)
        {
            return NotFound(new { error = "job_not_found" });
        }

        if (job.Status == "completed" || job.Status == "failed" || job.Status == "cancelled")
        {
            return BadRequest(new { error = "Job has already finished" });
        }

        job.CancellationRequested = true;
        job.Status = job.Status == "running" ? "cancelling" : "cancelled";
        job.UpdatedAt = DateTime.UtcNow;

        if (job.Status == "cancelled")
        {
            job.FinishedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Job {Id} cancellation requested", id);

        return Ok(new { ok = true, status = job.Status });
    }

    /// <summary>
    /// Delete a job (only completed/failed/cancelled jobs).
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        var job = await _context.Jobs.FindAsync(id);

        if (job == null)
        {
            return NotFound(new { error = "job_not_found" });
        }

        if (job.Status == "running" || job.Status == "queued")
        {
            return BadRequest(new { error = "Cannot delete active job" });
        }

        _context.Jobs.Remove(job);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Job {Id} deleted", id);

        return Ok(new { ok = true });
    }

    /// <summary>
    /// Get job statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetJobStats()
    {
        var stats = await _context.Jobs
            .GroupBy(j => j.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        var recentErrors = await _context.Jobs
            .Where(j => j.Status == "failed")
            .OrderByDescending(j => j.FinishedAt)
            .Take(5)
            .Select(j => new { j.Id, j.Type, j.Error, j.FinishedAt })
            .ToListAsync();

        return Ok(new { statusCounts = stats, recentErrors });
    }
}

public record CreateJobRequest(
    string Type,
    string? Params = null,
    int? MaxAttempts = null,
    DateTime? RunAt = null
);
