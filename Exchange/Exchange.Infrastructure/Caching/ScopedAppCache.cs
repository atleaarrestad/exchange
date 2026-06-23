namespace Exchange.Infrastructure.Caching;

public sealed class ScopedAppCache(
    IAppCache appCache,
    ICacheKeyFactory cacheKeyFactory) : IScopedAppCache
{
    public async Task SetEntityEntryAsync<T>(
        string scope,
        string entityId,
        string entryName,
        T value,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var key = await cacheKeyFactory.BuildVersionedKeyAsync(scope, entityId, entryName, cancellationToken);
        await appCache.SetAsync(key, value, ttl, cancellationToken);
    }

    public async Task<T?> GetEntityEntryAsync<T>(
        string scope,
        string entityId,
        string entryName,
        CancellationToken cancellationToken = default)
    {
        var key = await cacheKeyFactory.BuildVersionedKeyAsync(scope, entityId, entryName, cancellationToken);
        return await appCache.GetAsync<T>(key, cancellationToken);
    }

    public async Task RemoveEntityEntryAsync(
        string scope,
        string entityId,
        string entryName,
        CancellationToken cancellationToken = default)
    {
        var key = await cacheKeyFactory.BuildVersionedKeyAsync(scope, entityId, entryName, cancellationToken);
        await appCache.RemoveAsync(key, cancellationToken);
    }
}
