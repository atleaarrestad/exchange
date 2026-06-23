namespace Exchange.Infrastructure.Caching;

public sealed class CacheInvalidationService(
    IAppCache appCache,
    ICacheVersionStore versionStore) : ICacheInvalidationService
{
    public ValueTask<long> InvalidateEntityAsync(
        string scope,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        return versionStore.IncrementVersionAsync(scope, entityId, cancellationToken);
    }

    public Task InvalidateKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return appCache.RemoveAsync(key, cancellationToken);
    }

    public async Task InvalidateKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await appCache.RemoveAsync(key, cancellationToken);
        }
    }
}
