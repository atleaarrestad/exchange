using System.Collections.Concurrent;

namespace Exchange.Infrastructure.Caching;

public sealed class MemoryCacheVersionStore : ICacheVersionStore
{
    private readonly ConcurrentDictionary<string, long> versions = new(StringComparer.Ordinal);

    public ValueTask<long> GetVersionAsync(string scope, string entityId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildVersionLookupKey(scope, entityId);
        var version = versions.GetOrAdd(key, 1);
        return ValueTask.FromResult(version);
    }

    public ValueTask<long> IncrementVersionAsync(string scope, string entityId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildVersionLookupKey(scope, entityId);
        var version = versions.AddOrUpdate(key, 2, (_, current) => checked(current + 1));
        return ValueTask.FromResult(version);
    }

    private static string BuildVersionLookupKey(string scope, string entityId)
    {
        ValidateSegment(scope, nameof(scope));
        ValidateSegment(entityId, nameof(entityId));
        return $"{scope}:{entityId}";
    }

    private static void ValidateSegment(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
    }
}
