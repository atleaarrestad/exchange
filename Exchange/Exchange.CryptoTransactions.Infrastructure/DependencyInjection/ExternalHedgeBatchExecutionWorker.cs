using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class ExternalHedgeBatchExecutionWorker(
    IExternalHedgeBatchQueue externalHedgeBatchQueue,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    ILogger<ExternalHedgeBatchExecutionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tradingPolicy = tradingPolicyProvider.GetCurrent();
                var intervalSeconds = Math.Max(1, Math.Min(10, tradingPolicy.MaxBufferedHedgeDelaySeconds / 3));
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
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
