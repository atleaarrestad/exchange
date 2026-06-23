using Exchange.CryptoTransactions.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class CryptoTransferTimeoutReconciliationWorker(
    ICryptoTransferTimeoutReconciler timeoutReconciler,
    TimeoutReconciliationOptions options,
    ILogger<CryptoTransferTimeoutReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.ScanInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var staleBeforeUtc = DateTimeOffset.UtcNow - options.StaleAfter;
                await timeoutReconciler.ReconcileAsync(staleBeforeUtc, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Crypto transfer timeout reconciliation iteration failed.");
            }
        }
    }
}
