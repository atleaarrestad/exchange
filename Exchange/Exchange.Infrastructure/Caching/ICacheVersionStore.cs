namespace Exchange.Infrastructure.Caching;

public interface ICacheVersionStore
{
    ValueTask<long> GetVersionAsync(string scope, string entityId, CancellationToken cancellationToken = default);
    ValueTask<long> IncrementVersionAsync(string scope, string entityId, CancellationToken cancellationToken = default);
}
