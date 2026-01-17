using DataForeman.Core.Entities;

namespace DataForeman.Core.Interfaces;

public interface IChartRepository
{
    Task<ChartConfig?> GetByIdAsync(Guid id);
    Task<ChartConfig?> GetVisibleAsync(Guid id, Guid userId);
    Task<IEnumerable<ChartConfig>> GetByUserIdAsync(Guid userId, string scope = "all", int limit = 50, int offset = 0);
    Task<ChartConfig> CreateAsync(ChartConfig chart);
    Task UpdateAsync(ChartConfig chart);
    Task DeleteAsync(Guid id);
    Task<bool> IsOwnerAsync(Guid chartId, Guid userId);
}

public interface IChartFolderRepository
{
    Task<ChartFolder?> GetByIdAsync(Guid id);
    Task<IEnumerable<ChartFolder>> GetByUserIdAsync(Guid userId);
    Task<ChartFolder> CreateAsync(ChartFolder folder);
    Task UpdateAsync(ChartFolder folder);
    Task DeleteAsync(Guid id);
}
