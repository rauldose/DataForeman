using Microsoft.EntityFrameworkCore;
using DataForeman.Api.Data;
using DataForeman.Shared.Models;

namespace DataForeman.Api.Repositories;

public class FlowRepository : Repository<Flow>, IFlowRepository
{
    public FlowRepository(DataForemanDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Flow>> GetDeployedFlowsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.Deployed)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Flow>> GetFlowsByOwnerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.OwnerUserId == userId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Flow>> GetFlowsByFolderAsync(Guid? folderId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(f => f.FolderId == folderId)
            .OrderByDescending(f => f.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}

public class ChartRepository : Repository<ChartConfig>, IChartRepository
{
    public ChartRepository(DataForemanDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ChartConfig>> GetChartsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChartConfig>> GetSharedChartsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.IsShared && !c.IsDeleted)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}

public class DashboardRepository : Repository<Dashboard>, IDashboardRepository
{
    public DashboardRepository(DataForemanDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Dashboard>> GetDashboardsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.UserId == userId && !d.IsDeleted)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Dashboard>> GetSharedDashboardsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.IsShared && !d.IsDeleted)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}

public class ConnectionRepository : Repository<Connection>, IConnectionRepository
{
    public ConnectionRepository(DataForemanDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Connection>> GetEnabledConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.Enabled && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Connection?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.Name == name && c.DeletedAt == null, cancellationToken);
    }
}

public class TagRepository : Repository<TagMetadata>, ITagRepository
{
    public TagRepository(DataForemanDbContext context) : base(context) { }

    public async Task<TagMetadata?> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(t => t.TagId == tagId, cancellationToken);
    }

    public async Task<IReadOnlyList<TagMetadata>> GetTagsByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.ConnectionId == connectionId && !t.IsDeleted)
            .OrderBy(t => t.TagName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TagMetadata>> GetSubscribedTagsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.IsSubscribed && !t.IsDeleted)
            .Include(t => t.Connection)
            .ToListAsync(cancellationToken);
    }

    public async Task<TagMetadata?> GetByPathAsync(Guid connectionId, string tagPath, string driverType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(t => t.ConnectionId == connectionId && t.TagPath == tagPath && t.DriverType == driverType, cancellationToken);
    }
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(DataForemanDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetWithAuthIdentitiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.AuthIdentities)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetWithPermissionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.UserPermissions)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}

public class SessionRepository : Repository<Session>, ISessionRepository
{
    public SessionRepository(DataForemanDbContext context) : base(context) { }

    public async Task<Session?> GetByJtiAsync(Guid jti, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.Jti == jti, cancellationToken);
    }

    public async Task<Session?> GetByRefreshHashAsync(string refreshHash, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.RefreshHash == refreshHash && s.RevokedAt == null, cancellationToken);
    }

    public async Task<IReadOnlyList<Session>> GetActiveSessionsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> RevokeExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var expiredSessions = await _dbSet
            .Where(s => s.RevokedAt == null && s.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var session in expiredSessions)
        {
            session.RevokedAt = DateTime.UtcNow;
        }

        return expiredSessions.Count;
    }

    public async Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbSet
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.RevokedAt = DateTime.UtcNow;
        }
    }
}
