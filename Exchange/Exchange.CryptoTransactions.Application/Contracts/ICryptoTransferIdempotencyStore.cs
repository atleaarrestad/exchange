using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ICryptoTransferIdempotencyStore
{
    Task<CryptoTransferReceipt> ExecuteOnceAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        string requestFingerprint,
        decimal totalDebit,
        Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingCryptoTransferOperation>> GetPendingOlderThanAsync(
        DateTimeOffset olderThanUtc,
        CancellationToken cancellationToken = default);

    Task<bool> TryAcquirePendingAsync(
        PendingCryptoTransferOperation operation,
        CancellationToken cancellationToken = default);

    Task<bool> TryMarkCompletedAsync(
        PendingCryptoTransferOperation operation,
        CryptoTransferReceipt receipt,
        CancellationToken cancellationToken = default);

    Task<bool> TryReleasePendingAsync(
        PendingCryptoTransferOperation operation,
        CancellationToken cancellationToken = default);
}
