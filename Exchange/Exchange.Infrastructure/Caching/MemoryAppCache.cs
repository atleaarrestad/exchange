using Microsoft.Extensions.Caching.Memory;

namespace Exchange.Infrastructure.Caching;

public sealed class MemoryAppCache(IMemoryCache cache) : IAppCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(key);

        var cacheEntryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? DefaultTtl
        };

        cache.Set(key, value, cacheEntryOptions);
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(key);

        return Task.FromResult(cache.TryGetValue(key, out T? value) ? value : default);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateKey(key);

        cache.Remove(key);
        return Task.CompletedTask;
    }

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
    }
}
