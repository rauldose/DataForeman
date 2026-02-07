using Microsoft.Extensions.Caching.Memory;
using DataForeman.Shared.Models;

namespace DataForeman.Api.Services;

/// <summary>
/// Caching service for frequently accessed data
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
    Task RemoveAsync(string key);
    Task<IReadOnlyList<PollGroup>> GetPollGroupsAsync(Func<Task<IReadOnlyList<PollGroup>>> factory);
    Task<IReadOnlyList<UnitOfMeasure>> GetUnitsOfMeasureAsync(Func<Task<IReadOnlyList<UnitOfMeasure>>> factory);
    Task<IReadOnlyList<TagMetadata>> GetSubscribedTagsAsync(Func<Task<IReadOnlyList<TagMetadata>>> factory);
    void InvalidatePollGroups();
    void InvalidateUnitsOfMeasure();
    void InvalidateSubscribedTags();
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheService> _logger;
    
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongExpiration = TimeSpan.FromMinutes(30);
    
    private const string PollGroupsKey = "cache:pollgroups";
    private const string UnitsOfMeasureKey = "cache:units";
    private const string SubscribedTagsKey = "cache:subscribedtags";

    public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key) where T : class
    {
        var value = _cache.Get<T>(key);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
        };
        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PollGroup>> GetPollGroupsAsync(Func<Task<IReadOnlyList<PollGroup>>> factory)
    {
        return await GetOrCreateAsync(PollGroupsKey, factory, LongExpiration);
    }

    public async Task<IReadOnlyList<UnitOfMeasure>> GetUnitsOfMeasureAsync(Func<Task<IReadOnlyList<UnitOfMeasure>>> factory)
    {
        return await GetOrCreateAsync(UnitsOfMeasureKey, factory, LongExpiration);
    }

    public async Task<IReadOnlyList<TagMetadata>> GetSubscribedTagsAsync(Func<Task<IReadOnlyList<TagMetadata>>> factory)
    {
        return await GetOrCreateAsync(SubscribedTagsKey, factory, DefaultExpiration);
    }

    public void InvalidatePollGroups()
    {
        _cache.Remove(PollGroupsKey);
        _logger.LogDebug("Cache invalidated: {Key}", PollGroupsKey);
    }

    public void InvalidateUnitsOfMeasure()
    {
        _cache.Remove(UnitsOfMeasureKey);
        _logger.LogDebug("Cache invalidated: {Key}", UnitsOfMeasureKey);
    }

    public void InvalidateSubscribedTags()
    {
        _cache.Remove(SubscribedTagsKey);
        _logger.LogDebug("Cache invalidated: {Key}", SubscribedTagsKey);
    }

    private async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration) where T : class
    {
        if (_cache.TryGetValue(key, out T? cached) && cached != null)
        {
            _logger.LogDebug("Cache hit: {Key}", key);
            return cached;
        }

        _logger.LogDebug("Cache miss: {Key}, loading from source", key);
        var value = await factory();
        
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };
        _cache.Set(key, value, options);
        
        return value;
    }
}
