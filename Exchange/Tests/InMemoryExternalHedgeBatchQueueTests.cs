using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Gateways;

namespace Tests;

[TestClass]
public sealed class InMemoryExternalHedgeBatchQueueTests
{
    [TestMethod]
    public async Task ExecuteDueAsync_WhenCountThresholdIsReached_ExecutesAggregatedExternalOrder()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(now);
        var gateway = new SpyExternalLiquidityHedgingGateway();
        var policy = new BrokeredTradingPolicy
        {
            QuoteTtlSeconds = 30,
            InternalOnlySpreadBasisPoints = 35m,
            ExternalHedgeSpreadBasisPoints = 90m,
            MaxBufferedHedgeCustomerBuys = 2,
            MaxBufferedHedgeDelaySeconds = 120
        };
        var queue = new InMemoryExternalHedgeBatchQueue(gateway, new StaticBrokeredTradingPolicyProvider(policy), timeProvider);

        await queue.RegisterAsync(new BufferedExternalHedgeRequest("customer-1", "order-1", AssetSymbol.Bitcoin, QuoteCurrency.NorwegianKrone, 0.4m, now));
        await queue.RegisterAsync(new BufferedExternalHedgeRequest("customer-2", "order-2", AssetSymbol.Bitcoin, QuoteCurrency.NorwegianKrone, 0.6m, now));
        await queue.ExecuteDueAsync();

        Assert.AreEqual(1, gateway.Requests.Count);
        Assert.AreEqual(1.0m, gateway.Requests[0].Quantity);
    }

    [TestMethod]
    public async Task ExecuteDueAsync_WhenTimeThresholdIsReached_ExecutesBufferedOrder()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(start);
        var gateway = new SpyExternalLiquidityHedgingGateway();
        var policy = new BrokeredTradingPolicy
        {
            QuoteTtlSeconds = 30,
            InternalOnlySpreadBasisPoints = 35m,
            ExternalHedgeSpreadBasisPoints = 90m,
            MaxBufferedHedgeCustomerBuys = 10,
            MaxBufferedHedgeDelaySeconds = 5
        };
        var queue = new InMemoryExternalHedgeBatchQueue(gateway, new StaticBrokeredTradingPolicyProvider(policy), timeProvider);

        await queue.RegisterAsync(new BufferedExternalHedgeRequest("customer-3", "order-3", AssetSymbol.Ether, QuoteCurrency.NorwegianKrone, 2m, start));
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        await queue.ExecuteDueAsync();

        Assert.AreEqual(1, gateway.Requests.Count);
        Assert.AreEqual(2m, gateway.Requests[0].Quantity);
    }

    [TestMethod]
    public async Task RegisterAsync_WhenClientOrderIdsMatchAcrossCustomers_RegistersBothRequests()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(now);
        var gateway = new SpyExternalLiquidityHedgingGateway();
        var policy = new BrokeredTradingPolicy
        {
            QuoteTtlSeconds = 30,
            InternalOnlySpreadBasisPoints = 35m,
            ExternalHedgeSpreadBasisPoints = 90m,
            MaxBufferedHedgeCustomerBuys = 2,
            MaxBufferedHedgeDelaySeconds = 120
        };
        var queue = new InMemoryExternalHedgeBatchQueue(gateway, new StaticBrokeredTradingPolicyProvider(policy), timeProvider);

        await queue.RegisterAsync(new BufferedExternalHedgeRequest("customer-1", "same-order", AssetSymbol.Bitcoin, QuoteCurrency.NorwegianKrone, 0.4m, now));
        await queue.RegisterAsync(new BufferedExternalHedgeRequest("customer-2", "same-order", AssetSymbol.Bitcoin, QuoteCurrency.NorwegianKrone, 0.6m, now));
        await queue.ExecuteDueAsync();

        Assert.AreEqual(1, gateway.Requests.Count);
        Assert.AreEqual(1.0m, gateway.Requests[0].Quantity);
    }

    private sealed class SpyExternalLiquidityHedgingGateway : IExternalLiquidityHedgingGateway
    {
        public List<HedgePurchaseRequest> Requests { get; } = new();

        public Task<HedgePurchaseResult> BuyAsync(HedgePurchaseRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new HedgePurchaseResult($"hedge-{Guid.CreateVersion7()}", request.Quantity, 1_000_000m, DateTimeOffset.UtcNow));
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset currentUtc) : TimeProvider
    {
        private DateTimeOffset current = currentUtc;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration)
        {
            current = current.Add(duration);
        }
    }
}
