using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class ExternalHedgeBatchExecutionWorker(
    IExternalHedgeBatchQueue externalHedgeBatchQueue,
    BrokeredTradingPolicy tradingPolicy,
    ILogger<ExternalHedgeBatchExecutionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(1, Math.Min(10, tradingPolicy.MaxBufferedHedgeDelaySeconds / 3));
        var interval = TimeSpan.FromSeconds(intervalSeconds);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await externalHedgeBatchQueue.ExecuteDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "External hedge batch execution iteration failed.");
            }
        }
    }
}
