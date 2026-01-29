using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DataForeman.Core.Entities;
using DataForeman.Infrastructure.Data;

namespace DataForeman.API.Controllers;

/// <summary>
/// Controller for user management operations.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly DataForemanDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(DataForemanDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all users (admin only).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] bool includeInactive = false)
    {
        var query = _context.Users.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(u => u.IsActive);
        }

        var users = await query
            .OrderBy(u => u.Email)
            .Skip(offset)
            .Take(Math.Min(limit, 100))
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.IsActive,
                u.CreatedAt,
                u.UpdatedAt,
                PermissionCount = u.Permissions.Count
            })
            .ToListAsync();

        var totalCount = await query.CountAsync();

        return Ok(new { items = users, limit, offset, count = users.Count, total = totalCount });
    }

    /// <summary>
    /// Get a specific user by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound(new { error = "user_not_found" });
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            Permissions = user.Permissions.Select(p => new
            {
                p.Feature,
                p.CanCreate,
                p.CanRead,
                p.CanUpdate,
                p.CanDelete
            })
        });
    }

    /// <summary>
    /// Update a user.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { error = "user_not_found" });
        }

        if (request.DisplayName != null) user.DisplayName = request.DisplayName;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Id} updated", id);

        return Ok(new { ok = true });
    }

    /// <summary>
    /// Delete a user (soft delete by deactivating).
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { error = "user_not_found" });
        }

        // Don't allow deleting the default user
        if (id == Guid.Parse("00000000-0000-0000-0000-000000000001"))
        {
            return BadRequest(new { error = "Cannot delete default user" });
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Id} deactivated", id);

        return Ok(new { ok = true });
    }

    /// <summary>
    /// Get user permissions.
    /// </summary>
    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetUserPermissions(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound(new { error = "user_not_found" });
        }

        return Ok(new
        {
            userId = id,
            permissions = user.Permissions.Select(p => new
            {
                p.Feature,
                p.CanCreate,
                p.CanRead,
                p.CanUpdate,
                p.CanDelete
            })
        });
    }

    /// <summary>
    /// Update user permissions.
    /// </summary>
    [HttpPut("{id}/permissions")]
    public async Task<IActionResult> UpdateUserPermissions(Guid id, [FromBody] UpdatePermissionsRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound(new { error = "user_not_found" });
        }

        // Remove existing permissions for features being updated
        var featuresToUpdate = request.Permissions.Select(p => p.Feature).ToHashSet();
        var permissionsToRemove = user.Permissions.Where(p => featuresToUpdate.Contains(p.Feature)).ToList();
        _context.UserPermissions.RemoveRange(permissionsToRemove);

        // Add new permissions
        foreach (var perm in request.Permissions)
        {
            _context.UserPermissions.Add(new UserPermission
            {
                UserId = id,
                Feature = perm.Feature,
                CanCreate = perm.CanCreate,
                CanRead = perm.CanRead,
                CanUpdate = perm.CanUpdate,
                CanDelete = perm.CanDelete
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Permissions updated for user {Id}", id);

        return Ok(new { ok = true });
    }
}

public record UpdateUserRequest(
    string? DisplayName,
    bool? IsActive
);

public record UpdatePermissionsRequest(
    List<PermissionDto> Permissions
);

public record PermissionDto(
    string Feature,
    bool CanCreate,
    bool CanRead,
    bool CanUpdate,
    bool CanDelete
);
