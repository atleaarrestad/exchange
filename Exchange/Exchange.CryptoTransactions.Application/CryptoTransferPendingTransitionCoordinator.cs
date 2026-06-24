using Exchange.CryptoTransactions.Application.Contracts;

namespace Exchange.CryptoTransactions.Application;

public sealed class CryptoTransferPendingTransitionCoordinator(
    ICryptoTransferFundsReservationGateway fundsReservationGateway,
    ICryptoTransferIdempotencyStore idempotencyStore) : ICryptoTransferPendingTransitionCoordinator
{
    public async Task CommitAndMarkCompletedAsync(
        PendingCryptoTransferOperation operation,
        CryptoTransferReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(receipt);
        cancellationToken.ThrowIfCancellationRequested();

        await fundsReservationGateway.CommitAsync(
            operation.SourceAccountId,
            operation.AssetSymbol,
            operation.IdempotencyKey,
            cancellationToken);

        var markedCompleted = await idempotencyStore.TryMarkCompletedAsync(operation, receipt, cancellationToken);
        if (!markedCompleted)
        {
            throw new InvalidOperationException(
                $"Unable to transition pending transfer '{operation.SourceAccountId}/{operation.AssetSymbol.Value}/{operation.IdempotencyKey}' to completed state after funds commit.");
        }
    }

    public async Task ReleaseAndDropPendingAsync(
        PendingCryptoTransferOperation operation,
        string failureContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureContext);
        cancellationToken.ThrowIfCancellationRequested();

        await fundsReservationGateway.ReleaseAsync(
            operation.SourceAccountId,
            operation.AssetSymbol,
            operation.IdempotencyKey,
            cancellationToken);

        var released = await idempotencyStore.TryReleasePendingAsync(operation, cancellationToken);
        if (!released)
        {
            throw new InvalidOperationException(
                $"Unable to release pending transfer '{operation.SourceAccountId}/{operation.AssetSymbol.Value}/{operation.IdempotencyKey}' after {failureContext}.");
        }
    }
}
