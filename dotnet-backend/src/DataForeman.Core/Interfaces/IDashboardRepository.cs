using DataForeman.Core.Entities;

namespace DataForeman.Core.Interfaces;

public interface IDashboardRepository
{
    Task<Dashboard?> GetByIdAsync(Guid id);
    Task<Dashboard?> GetVisibleAsync(Guid id, Guid userId);
    Task<IEnumerable<Dashboard>> GetByUserIdAsync(Guid userId, string scope = "all", int limit = 50, int offset = 0);
    Task<Dashboard> CreateAsync(Dashboard dashboard);
    Task UpdateAsync(Dashboard dashboard);
    Task DeleteAsync(Guid id);
    Task<bool> IsOwnerAsync(Guid dashboardId, Guid userId);
}

public interface IDashboardFolderRepository
{
    Task<DashboardFolder?> GetByIdAsync(Guid id);
    Task<IEnumerable<DashboardFolder>> GetByUserIdAsync(Guid userId);
    Task<DashboardFolder> CreateAsync(DashboardFolder folder);
    Task UpdateAsync(DashboardFolder folder);
    Task DeleteAsync(Guid id);
}
