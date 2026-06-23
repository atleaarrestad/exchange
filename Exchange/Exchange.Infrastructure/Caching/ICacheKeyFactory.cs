namespace Exchange.Infrastructure.Caching;

public interface ICacheKeyFactory
{
    ValueTask<string> BuildVersionedKeyAsync(
        string scope,
        string entityId,
        string entryName,
        CancellationToken cancellationToken = default);

    string BuildVersionPointerKey(string scope, string entityId);
}
