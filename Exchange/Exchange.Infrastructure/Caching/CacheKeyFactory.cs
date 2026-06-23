namespace Exchange.Infrastructure.Caching;

public sealed class CacheKeyFactory(ICacheVersionStore versionStore) : ICacheKeyFactory
{
    private const string VersionPointerPrefix = "cache-version";

    public async ValueTask<string> BuildVersionedKeyAsync(
        string scope,
        string entityId,
        string entryName,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(scope, nameof(scope));
        ValidateSegment(entityId, nameof(entityId));
        ValidateSegment(entryName, nameof(entryName));

        var version = await versionStore.GetVersionAsync(scope, entityId, cancellationToken);
        return $"{scope}:{entityId}:{entryName}:v{version}";
    }

    public string BuildVersionPointerKey(string scope, string entityId)
    {
        ValidateSegment(scope, nameof(scope));
        ValidateSegment(entityId, nameof(entityId));
        return $"{VersionPointerPrefix}:{scope}:{entityId}";
    }

    private static void ValidateSegment(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
    }
}
