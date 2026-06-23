namespace Exchange.Infrastructure.Caching;

public interface IScopedAppCache
{
    Task SetEntityEntryAsync<T>(
        string scope,
        string entityId,
        string entryName,
        T value,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    Task<T?> GetEntityEntryAsync<T>(
        string scope,
        string entityId,
        string entryName,
        CancellationToken cancellationToken = default);

    Task RemoveEntityEntryAsync(
        string scope,
        string entityId,
        string entryName,
        CancellationToken cancellationToken = default);
}
