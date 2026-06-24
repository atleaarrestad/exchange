using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class CryptoTransferSubmissionProcessor(
    ICryptoTransferIdempotencyStore idempotencyStore,
    IBlockchainTransferGateway blockchainTransferGateway,
    ICryptoTransferFundsReservationGateway fundsReservationGateway,
    ILogger<CryptoTransferSubmissionProcessor> logger)
{
    private static readonly TimeSpan GatewaySubmitTimeout = TimeSpan.FromSeconds(30);

    public async Task ProcessOperationAsync(PendingCryptoTransferOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        var acquired = await idempotencyStore.TryAcquirePendingAsync(operation, cancellationToken);
        if (!acquired)
        {
            return;
        }

        using var timeoutCts = new CancellationTokenSource(GatewaySubmitTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var gatewayResult = await blockchainTransferGateway.SubmitAsync(
                new BlockchainTransferRequest(
                    operation.IdempotencyKey,
                    operation.SourceAccountId,
                    operation.DestinationAddress,
                    operation.AssetSymbol,
                    operation.Amount,
                    operation.NetworkFee,
                    operation.TotalDebit),
                linkedCts.Token);

            await fundsReservationGateway.CommitAsync(
                operation.SourceAccountId,
                operation.AssetSymbol,
                operation.IdempotencyKey,
                cancellationToken);

            var completed = await idempotencyStore.TryMarkCompletedAsync(
                operation,
                new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    gatewayResult.GatewayTransactionId,
                    gatewayResult.SubmittedAtUtc,
                    operation.TotalDebit,
                    gatewayResult.RequiredConfirmations,
                    CryptoTransferReceiptStatus.Submitted),
                cancellationToken);
            if (!completed)
            {
                throw new InvalidOperationException(
                    $"Unable to mark crypto transfer '{operation.SourceAccountId}/{operation.AssetSymbol.Value}/{operation.IdempotencyKey}' as completed after successful gateway submission.");
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Crypto transfer submission timed out for {SourceAccountId}/{AssetSymbol}/{IdempotencyKey}. Pending reconciliation will continue.",
                operation.SourceAccountId,
                operation.AssetSymbol.Value,
                operation.IdempotencyKey);
        }
        catch (Exception exception) when (
            exception is BlockchainTransferRejectedException
            or ExternalDependencyNotConfiguredException)
        {
            await fundsReservationGateway.ReleaseAsync(
                operation.SourceAccountId,
                operation.AssetSymbol,
                operation.IdempotencyKey,
                cancellationToken);

            var released = await idempotencyStore.TryReleasePendingAsync(operation, cancellationToken);
            if (!released)
            {
                throw new InvalidOperationException(
                    $"Unable to release pending crypto transfer '{operation.SourceAccountId}/{operation.AssetSymbol.Value}/{operation.IdempotencyKey}' after deterministic submission failure.",
                    exception);
            }

            logger.LogWarning(
                exception,
                "Crypto transfer submission was deterministically rejected for {SourceAccountId}/{AssetSymbol}/{IdempotencyKey}.",
                operation.SourceAccountId,
                operation.AssetSymbol.Value,
                operation.IdempotencyKey);
        }
    }
}
