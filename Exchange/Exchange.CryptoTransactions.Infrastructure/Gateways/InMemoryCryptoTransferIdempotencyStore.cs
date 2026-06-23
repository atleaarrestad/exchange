using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using System.Collections.Concurrent;
using System.Threading;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class InMemoryCryptoTransferIdempotencyStore : ICryptoTransferIdempotencyStore
{
    private readonly ConcurrentDictionary<CryptoTransferIdempotencyKey, Lazy<Task<CryptoTransferReceipt>>> operations = new();

    public async Task<CryptoTransferReceipt> ExecuteOnceAsync(
        string sourceAccountId,
        string idempotencyKey,
        Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentNullException.ThrowIfNull(transferFactory);

        var key = new CryptoTransferIdempotencyKey(sourceAccountId.Trim(), idempotencyKey.Trim());
        var lazyOperation = operations.GetOrAdd(
            key,
            static (_, callback) => new Lazy<Task<CryptoTransferReceipt>>(
                () => callback(CancellationToken.None),
                LazyThreadSafetyMode.ExecutionAndPublication),
            transferFactory);

        try
        {
            return await lazyOperation.Value.WaitAsync(cancellationToken);
        }
        catch
        {
            operations.TryRemove(key, out _);
            throw;
        }
    }

    private readonly record struct CryptoTransferIdempotencyKey(string SourceAccountId, string IdempotencyKey);
}
