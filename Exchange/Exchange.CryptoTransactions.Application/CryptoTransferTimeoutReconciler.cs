using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application;

public sealed class CryptoTransferTimeoutReconciler(
    IBlockchainTransferGateway blockchainTransferGateway,
    ICryptoTransferIdempotencyStore idempotencyStore,
    ICryptoTransferPendingTransitionCoordinator pendingTransitionCoordinator) : ICryptoTransferTimeoutReconciler
{
    private static readonly TimeSpan NotSubmittedReleaseSafetyWindow = TimeSpan.FromMinutes(2);

    public async Task ReconcileAsync(DateTimeOffset staleBeforeUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var staleOperations = await idempotencyStore.GetPendingOlderThanAsync(staleBeforeUtc, cancellationToken);
        var unknownOperations = new List<(string SourceAccountId, AssetSymbol AssetSymbol, string IdempotencyKey)>();

        foreach (var operation in staleOperations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var acquired = await idempotencyStore.TryAcquirePendingAsync(operation, cancellationToken);
            if (!acquired)
            {
                continue;
            }

            var transferStatus = await blockchainTransferGateway.GetTransferStatusAsync(
                operation.SourceAccountId,
                operation.AssetSymbol,
                operation.IdempotencyKey,
                cancellationToken);

            if (transferStatus.StatusKind == BlockchainTransferStatusKind.Unknown)
            {
                unknownOperations.Add((operation.SourceAccountId, operation.AssetSymbol, operation.IdempotencyKey));
                continue;
            }

            if (transferStatus.StatusKind == BlockchainTransferStatusKind.NotSubmitted)
            {
                var nowUtc = DateTimeOffset.UtcNow;
                if (nowUtc - operation.CreatedAtUtc.ToUniversalTime() < NotSubmittedReleaseSafetyWindow)
                {
                    continue;
                }

                await pendingTransitionCoordinator.ReleaseAndDropPendingAsync(
                    operation,
                    "reconciliation release",
                    cancellationToken);
                continue;
            }

            if (string.IsNullOrWhiteSpace(transferStatus.GatewayTransactionId))
            {
                throw new InvalidOperationException("Gateway returned submitted status without a transaction id.");
            }

            var receipt = new CryptoTransferReceipt(
                Guid.CreateVersion7(),
                transferStatus.GatewayTransactionId,
                transferStatus.SubmittedAtUtc ?? DateTimeOffset.UtcNow,
                operation.TotalDebit,
                transferStatus.RequiredConfirmations);

            await pendingTransitionCoordinator.CommitAndMarkCompletedAsync(operation, receipt, cancellationToken);
        }

        if (unknownOperations.Count > 0)
        {
            var unknownSummary = string.Join(
                ", ",
                unknownOperations.Select(static operation =>
                    $"{operation.SourceAccountId}/{operation.AssetSymbol.Value}/{operation.IdempotencyKey}"));

            throw new UnknownBlockchainTransferStatusException(
                $"Unable to reconcile {unknownOperations.Count} pending crypto transfer operation(s) because gateway status is unknown: {unknownSummary}. Manual investigation is required.");
        }
    }
}
