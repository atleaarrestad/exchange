namespace Exchange.Infrastructure.Caching;

public interface ICacheInvalidationService
{
    ValueTask<long> InvalidateEntityAsync(string scope, string entityId, CancellationToken cancellationToken = default);
    Task InvalidateKeyAsync(string key, CancellationToken cancellationToken = default);
    Task InvalidateKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}
