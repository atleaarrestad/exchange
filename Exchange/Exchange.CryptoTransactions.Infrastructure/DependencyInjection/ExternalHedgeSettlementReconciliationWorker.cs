using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class ExternalHedgeSettlementReconciliationWorker(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    IExternalHedgeSettlementService externalHedgeSettlementService,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    IBackgroundWorkerHeartbeatStore heartbeatStore,
    TimeProvider timeProvider,
    ILogger<ExternalHedgeSettlementReconciliationWorker> logger) : BackgroundService
{
    private const int MaxSettlementsPerIteration = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tradingPolicy = tradingPolicyProvider.GetCurrent();
                var intervalSeconds = Math.Max(1, Math.Min(10, tradingPolicy.MaxBufferedHedgeDelaySeconds / 3));
                await heartbeatStore.UpsertHeartbeatAsync(
                    BackgroundWorkerNames.ExternalHedgeSettlementReconciliation,
                    timeProvider.GetUtcNow(),
                    stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);

                var externalOrderIds = await GetUnsettledExternalOrderIdsAsync(stoppingToken);
                foreach (var externalOrderId in externalOrderIds)
                {
                    try
                    {
                        await externalHedgeSettlementService.SettleAsync(externalOrderId, stoppingToken);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(
                            exception,
                            "External hedge settlement reconciliation failed for external order id '{ExternalOrderId}'.",
                            externalOrderId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "External hedge settlement reconciliation iteration failed.");
            }
        }
    }

    private async Task<string[]> GetUnsettledExternalOrderIdsAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExternalHedgeExecutionRecords
            .AsNoTracking()
            .Where(record => record.SettledAtUtc == null)
            .OrderBy(record => record.ExecutedAtUtc)
            .Select(record => record.ExternalOrderId)
            .Take(MaxSettlementsPerIteration)
            .ToArrayAsync(cancellationToken);
    }
}
