using DataForeman.Core.Entities;

namespace DataForeman.Core.Interfaces;

public interface IConnectionRepository
{
    Task<Connection?> GetByIdAsync(Guid id);
    Task<Connection?> GetByNameAsync(string name);
    Task<IEnumerable<Connection>> GetAllAsync(bool includeDeleted = false);
    Task<IEnumerable<Connection>> GetEnabledAsync();
    Task<Connection> CreateAsync(Connection connection);
    Task UpdateAsync(Connection connection);
    Task DeleteAsync(Guid id);
    Task SoftDeleteAsync(Guid id);
}

public interface ITagMetadataRepository
{
    Task<TagMetadata?> GetByIdAsync(int tagId);
    Task<TagMetadata?> GetByPathAsync(Guid connectionId, string tagPath, string driverType);
    Task<IEnumerable<TagMetadata>> GetByConnectionIdAsync(Guid connectionId, bool includeDeleted = false);
    Task<IEnumerable<TagMetadata>> GetSubscribedTagsAsync(Guid connectionId);
    Task<TagMetadata> CreateAsync(TagMetadata tag);
    Task UpdateAsync(TagMetadata tag);
    Task DeleteAsync(int tagId);
    Task SetSubscribedAsync(int tagId, bool subscribed);
    Task<IEnumerable<TagMetadata>> SearchAsync(string query, int limit = 100);
}

public interface IPollGroupRepository
{
    Task<PollGroup?> GetByIdAsync(int groupId);
    Task<IEnumerable<PollGroup>> GetAllAsync();
    Task<IEnumerable<PollGroup>> GetActiveAsync();
}

public interface IUnitOfMeasureRepository
{
    Task<UnitOfMeasure?> GetByIdAsync(int id);
    Task<IEnumerable<UnitOfMeasure>> GetAllAsync();
    Task<IEnumerable<UnitOfMeasure>> GetByCategoryAsync(string category);
    Task<UnitOfMeasure> CreateAsync(UnitOfMeasure unit);
    Task UpdateAsync(UnitOfMeasure unit);
    Task DeleteAsync(int id);
}

public interface ITagValueRepository
{
    Task<IEnumerable<TagValue>> GetByTagIdAsync(int tagId, DateTime from, DateTime to, int limit = 10000);
    Task<IEnumerable<TagValue>> GetByTagIdsAsync(IEnumerable<int> tagIds, DateTime from, DateTime to, int limit = 10000);
    Task<TagValue?> GetLatestAsync(int tagId);
    Task<IDictionary<int, TagValue>> GetLatestForTagsAsync(IEnumerable<int> tagIds);
    Task CreateAsync(TagValue value);
    Task CreateManyAsync(IEnumerable<TagValue> values);
    Task CleanupOldDataAsync(int tagId, DateTime before);
}
