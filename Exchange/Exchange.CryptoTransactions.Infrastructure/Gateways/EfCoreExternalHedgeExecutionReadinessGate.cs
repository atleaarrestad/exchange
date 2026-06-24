using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreExternalHedgeExecutionReadinessGate(
    IBackgroundWorkerHeartbeatStore heartbeatStore,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    TimeProvider timeProvider) : IExternalHedgeExecutionReadinessGate
{
    private static readonly TimeSpan MinimumFreshness = TimeSpan.FromSeconds(10);

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        var lastSeen = await heartbeatStore.GetLastSeenAtUtcAsync(
            BackgroundWorkerNames.ExternalHedgeBatchExecution,
            cancellationToken);
        if (lastSeen is null)
        {
            throw new ExternalHedgeExecutionUnavailableException(
                "External hedge execution worker heartbeat is missing. External hedge-backed buys are temporarily unavailable.");
        }

        var tradingPolicy = tradingPolicyProvider.GetCurrent();
        var freshnessWindow = TimeSpan.FromSeconds(Math.Max(
            MinimumFreshness.TotalSeconds,
            tradingPolicy.MaxBufferedHedgeDelaySeconds * 2));
        var now = timeProvider.GetUtcNow();
        if (now - lastSeen.Value.ToUniversalTime() > freshnessWindow)
        {
            throw new ExternalHedgeExecutionUnavailableException(
                "External hedge execution worker heartbeat is stale. External hedge-backed buys are temporarily unavailable.");
        }
    }
}
