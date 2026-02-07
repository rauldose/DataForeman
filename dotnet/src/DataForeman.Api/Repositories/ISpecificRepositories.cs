using DataForeman.Shared.Models;

namespace DataForeman.Api.Repositories;

public interface IFlowRepository : IRepository<Flow>
{
    Task<IReadOnlyList<Flow>> GetDeployedFlowsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Flow>> GetFlowsByOwnerAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Flow>> GetFlowsByFolderAsync(Guid? folderId, CancellationToken cancellationToken = default);
}

public interface IChartRepository : IRepository<ChartConfig>
{
    Task<IReadOnlyList<ChartConfig>> GetChartsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChartConfig>> GetSharedChartsAsync(CancellationToken cancellationToken = default);
}

public interface IDashboardRepository : IRepository<Dashboard>
{
    Task<IReadOnlyList<Dashboard>> GetDashboardsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Dashboard>> GetSharedDashboardsAsync(CancellationToken cancellationToken = default);
}

public interface IConnectionRepository : IRepository<Connection>
{
    Task<IReadOnlyList<Connection>> GetEnabledConnectionsAsync(CancellationToken cancellationToken = default);
    Task<Connection?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

public interface ITagRepository : IRepository<TagMetadata>
{
    Task<TagMetadata?> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagMetadata>> GetTagsByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TagMetadata>> GetSubscribedTagsAsync(CancellationToken cancellationToken = default);
    Task<TagMetadata?> GetByPathAsync(Guid connectionId, string tagPath, string driverType, CancellationToken cancellationToken = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetWithAuthIdentitiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetWithPermissionsAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISessionRepository : IRepository<Session>
{
    Task<Session?> GetByJtiAsync(Guid jti, CancellationToken cancellationToken = default);
    Task<Session?> GetByRefreshHashAsync(string refreshHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Session>> GetActiveSessionsByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> RevokeExpiredSessionsAsync(CancellationToken cancellationToken = default);
    Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
