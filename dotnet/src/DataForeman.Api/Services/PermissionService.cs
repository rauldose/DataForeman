using Microsoft.EntityFrameworkCore;
using DataForeman.Api.Data;

namespace DataForeman.Api.Services;

public interface IPermissionService
{
    Task<bool> CanAsync(Guid userId, string feature, string operation);
    void InvalidateCache(Guid userId);
}

public class PermissionService : IPermissionService
{
    private readonly DataForemanDbContext _db;
    private readonly ILogger<PermissionService> _logger;
    private readonly Dictionary<Guid, Dictionary<string, PermissionCacheEntry>> _cache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public PermissionService(DataForemanDbContext db, ILogger<PermissionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> CanAsync(Guid userId, string feature, string operation)
    {
        // Check cache first
        if (_cache.TryGetValue(userId, out var userCache) && 
            userCache.TryGetValue(feature, out var entry) && 
            entry.Expiry > DateTime.UtcNow)
        {
            return CheckOperation(entry, operation);
        }

        // Load from database
        var permission = await _db.UserPermissions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Feature == feature);

        if (permission == null)
        {
            return false;
        }

        // Cache the result
        var cacheEntry = new PermissionCacheEntry
        {
            CanCreate = permission.CanCreate,
            CanRead = permission.CanRead,
            CanUpdate = permission.CanUpdate,
            CanDelete = permission.CanDelete,
            Expiry = DateTime.UtcNow.Add(_cacheExpiry)
        };

        if (!_cache.ContainsKey(userId))
        {
            _cache[userId] = new Dictionary<string, PermissionCacheEntry>();
        }
        _cache[userId][feature] = cacheEntry;

        return CheckOperation(cacheEntry, operation);
    }

    public void InvalidateCache(Guid userId)
    {
        _cache.Remove(userId);
    }

    private static bool CheckOperation(PermissionCacheEntry entry, string operation)
    {
        return operation.ToLower() switch
        {
            "create" => entry.CanCreate,
            "read" => entry.CanRead,
            "update" => entry.CanUpdate,
            "delete" => entry.CanDelete,
            _ => false
        };
    }

    private class PermissionCacheEntry
    {
        public bool CanCreate { get; set; }
        public bool CanRead { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDelete { get; set; }
        public DateTime Expiry { get; set; }
    }
}
