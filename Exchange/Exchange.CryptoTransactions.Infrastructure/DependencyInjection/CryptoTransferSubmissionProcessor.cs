using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Microsoft.Extensions.Logging;
using Polly.Bulkhead;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class CryptoTransferSubmissionProcessor(
    ICryptoTransferIdempotencyStore idempotencyStore,
    IBlockchainTransferGateway blockchainTransferGateway,
    ICryptoTransferPendingTransitionCoordinator pendingTransitionCoordinator,
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

            await pendingTransitionCoordinator.CommitAndMarkCompletedAsync(
                operation,
                new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    gatewayResult.GatewayTransactionId,
                    gatewayResult.SubmittedAtUtc,
                    operation.TotalDebit,
                    gatewayResult.RequiredConfirmations,
                    CryptoTransferReceiptStatus.Submitted),
                cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Crypto transfer submission timed out for {SourceAccountId}/{AssetSymbol}/{IdempotencyKey}. Pending reconciliation will continue.",
                operation.SourceAccountId,
                operation.AssetSymbol.Value,
                operation.IdempotencyKey);
        }
        catch (BrokenCircuitException exception)
        {
            logger.LogWarning(
                exception,
                "Crypto transfer submission deferred because blockchain gateway circuit is open for {SourceAccountId}/{AssetSymbol}/{IdempotencyKey}.",
                operation.SourceAccountId,
                operation.AssetSymbol.Value,
                operation.IdempotencyKey);
        }
        catch (BulkheadRejectedException exception)
        {
            logger.LogWarning(
                exception,
                "Crypto transfer submission deferred because blockchain gateway bulkhead is saturated for {SourceAccountId}/{AssetSymbol}/{IdempotencyKey}.",
                operation.SourceAccountId,
                operation.AssetSymbol.Value,
                operation.IdempotencyKey);
        }
        catch (TimeoutRejectedException exception)
        {
            logger.LogWarning(
                exception,
                "Crypto transfer submission deferred after resilience timeout for {SourceAccountId}/{AssetSymbol}/{IdempotencyKey}.",
                operation.SourceAccountId,
                operation.AssetSymbol.Value,
                operation.IdempotencyKey);
        }
        catch (Exception exception) when (
            exception is BlockchainTransferRejectedException
            or ExternalDependencyNotConfiguredException)
        {
            await pendingTransitionCoordinator.ReleaseAndDropPendingAsync(
                operation,
                "deterministic submission failure",
                cancellationToken);

            logger.LogWarning(
                exception,
                "Crypto transfer submission was deterministically rejected for {SourceAccountId}/{AssetSymbol}/{IdempotencyKey}.",
                operation.SourceAccountId,
                operation.AssetSymbol.Value,
                operation.IdempotencyKey);
        }
    }
}
