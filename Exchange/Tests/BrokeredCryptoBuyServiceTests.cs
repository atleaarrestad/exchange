using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Tests;

[TestClass]
public sealed class BrokeredCryptoBuyServiceTests
{
    [TestMethod]
    public async Task QuoteAsync_WhenInternalInventoryCoversOrder_UsesReferencePriceAndNoHedge()
    {
        var service = CreateService(availableInventory: 5m);

        var quote = await service.QuoteAsync(new QuoteBrokeredCryptoBuyCommand(
            "customer-1",
            "BTC",
            1.25m,
            "NOK"));

        Assert.IsFalse(quote.RequiresExternalHedge);
        Assert.AreEqual(1.25m, quote.InternalFillQuantity);
        Assert.AreEqual(0m, quote.ExternalHedgeQuantity);
        Assert.AreEqual("internal-reference", quote.PriceSource);
        Assert.AreEqual(1_003_500m, quote.UnitPrice);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenInventoryIsInsufficient_BuysExternalAndRecordsExecution()
    {
        var ownershipLedger = new InMemoryOwnershipLedger(availableInventory: 1m);
        var hedgeBatchQueue = new InMemoryExternalHedgeBatchQueue();
        var service = CreateService(ownershipLedger: ownershipLedger, externalHedgeBatchQueue: hedgeBatchQueue);
        var quote = await service.QuoteAsync(new QuoteBrokeredCryptoBuyCommand(
            "customer-2",
            "BTC",
            2m,
            "NOK"));

        var receipt = await service.ExecuteAsync(new ExecuteBrokeredCryptoBuyCommand(
            quote.QuoteId,
            "order-1",
            "customer-2",
            "BTC",
            2m,
            "NOK"));

        Assert.AreEqual(1m, receipt.InternalFillQuantity);
        Assert.AreEqual(1m, receipt.ExternalHedgeQuantity);
        Assert.IsNull(receipt.ExternalHedgeOrderId);
        Assert.AreEqual(0m, await ownershipLedger.GetAvailablePlatformInventoryAsync(AssetSymbol.Bitcoin));
        Assert.AreEqual(1, hedgeBatchQueue.Registered.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenPriceProtectionIsExceeded_ThrowsAndDoesNotHedge()
    {
        var ownershipLedger = new InMemoryOwnershipLedger(availableInventory: 0m);
        var service = CreateService(ownershipLedger: ownershipLedger);
        var quote = await service.QuoteAsync(new QuoteBrokeredCryptoBuyCommand(
            "customer-3",
            "BTC",
            1m,
            "NOK"));

        await Assert.ThrowsExactlyAsync<PriceProtectionExceededException>(() =>
            service.ExecuteAsync(new ExecuteBrokeredCryptoBuyCommand(
                quote.QuoteId,
                "order-2",
                "customer-3",
                "BTC",
                1m,
                "NOK",
                MaxUnitPrice: 900_000m)));
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenQuoteIsExpired_Throws()
    {
        var quoteStore = new InMemoryQuoteStore();
        var service = CreateService(quoteStore: quoteStore);
        var quote = await service.QuoteAsync(new QuoteBrokeredCryptoBuyCommand(
            "customer-4",
            "BTC",
            1m,
            "NOK"));
        quoteStore.Replace(quote with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1) });

        await Assert.ThrowsExactlyAsync<QuoteExecutionRejectedException>(() =>
            service.ExecuteAsync(new ExecuteBrokeredCryptoBuyCommand(
                quote.QuoteId,
                "order-3",
                "customer-4",
                "BTC",
                1m,
                "NOK")));
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenConfiguredSlippageIsExceeded_RejectsExecution()
    {
        var livePriceFeed = new MutableLivePriceFeed(1_010_000m);
        var service = CreateService(
            availableInventory: 0m,
            liveMarketPriceFeed: livePriceFeed,
            maxAllowedSlippageBasisPoints: 100m);
        var quote = await service.QuoteAsync(new QuoteBrokeredCryptoBuyCommand(
            "customer-5",
            "BTC",
            1m,
            "NOK"));

        livePriceFeed.UnitPrice = 1_060_000m;

        await Assert.ThrowsExactlyAsync<QuoteExecutionRejectedException>(() =>
            service.ExecuteAsync(new ExecuteBrokeredCryptoBuyCommand(
                quote.QuoteId,
                "order-5",
                "customer-5",
                "BTC",
                1m,
                "NOK")));
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenHedgeQueueRegistrationFails_CompensatesBookedCrypto()
    {
        var ownershipLedger = new InMemoryOwnershipLedger(availableInventory: 1m);
        var hedgeBatchQueue = new InMemoryExternalHedgeBatchQueue
        {
            ThrowOnRegister = true
        };
        var service = CreateService(ownershipLedger: ownershipLedger, externalHedgeBatchQueue: hedgeBatchQueue);
        var quote = await service.QuoteAsync(new QuoteBrokeredCryptoBuyCommand(
            "customer-6",
            "BTC",
            2m,
            "NOK"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(new ExecuteBrokeredCryptoBuyCommand(
                quote.QuoteId,
                "order-6",
                "customer-6",
                "BTC",
                2m,
                "NOK")));

        Assert.AreEqual(1m, await ownershipLedger.GetAvailablePlatformInventoryAsync(AssetSymbol.Bitcoin));
        Assert.AreEqual(0m, ownershipLedger.GetCustomerHolding("customer-6", AssetSymbol.Bitcoin));
        Assert.AreEqual(0, hedgeBatchQueue.Registered.Count);
    }

    private static BrokeredCryptoBuyService CreateService(
        decimal availableInventory = 5m,
        InMemoryOwnershipLedger? ownershipLedger = null,
        InMemoryQuoteStore? quoteStore = null,
        InMemoryExternalHedgeBatchQueue? externalHedgeBatchQueue = null,
        IInternalReferencePriceFeed? referencePriceFeed = null,
        ILiveMarketPriceFeed? liveMarketPriceFeed = null,
        decimal maxAllowedSlippageBasisPoints = 200m)
    {
        var policy = new BrokeredTradingPolicy
        {
            QuoteTtlSeconds = 30,
            InternalOnlySpreadBasisPoints = 35m,
            ExternalHedgeSpreadBasisPoints = 90m,
            MaxAllowedSlippageBasisPoints = maxAllowedSlippageBasisPoints,
            MaxBufferedHedgeCustomerBuys = 2,
            MaxBufferedHedgeDelaySeconds = 10
        };

        return new BrokeredCryptoBuyService(
            referencePriceFeed ?? new ConstantReferencePriceFeed(),
            liveMarketPriceFeed ?? new ConstantLivePriceFeed(),
            quoteStore ?? new InMemoryQuoteStore(),
            ownershipLedger ?? new InMemoryOwnershipLedger(availableInventory),
            externalHedgeBatchQueue ?? new InMemoryExternalHedgeBatchQueue(),
            new AlwaysReadyExternalHedgeExecutionReadinessGate(),
            new StaticBrokeredTradingPolicyProvider(policy));
    }

    private sealed class AlwaysReadyExternalHedgeExecutionReadinessGate : IExternalHedgeExecutionReadinessGate
    {
        public Task EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryExternalHedgeBatchQueue : IExternalHedgeBatchQueue
    {
        public List<BufferedExternalHedgeRequest> Registered { get; } = new();
        public bool ThrowOnRegister { get; set; }

        public Task RegisterAsync(BufferedExternalHedgeRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnRegister)
            {
                throw new InvalidOperationException("queue unavailable");
            }

            Registered.Add(request);
            return Task.CompletedTask;
        }

        public Task<BufferedExternalHedgeCancellationResult> CancelRegistrationAsync(
            string customerAccountId,
            string clientOrderId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var removed = Registered.RemoveAll(candidate =>
                string.Equals(candidate.CustomerAccountId, customerAccountId.Trim(), StringComparison.Ordinal)
                && string.Equals(candidate.ClientOrderId, clientOrderId.Trim(), StringComparison.Ordinal));

            return Task.FromResult(new BufferedExternalHedgeCancellationResult(
                removed > 0
                    ? BufferedExternalHedgeCancellationStatus.RemovedPending
                    : BufferedExternalHedgeCancellationStatus.NotFound,
                null));
        }

        public Task ExecuteDueAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class ConstantReferencePriceFeed : IInternalReferencePriceFeed
    {
        public Task<PriceQuote> GetReferencePriceAsync(
            AssetSymbol assetSymbol,
            QuoteCurrency quoteCurrency,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PriceQuote(1_000_000m, DateTimeOffset.UtcNow, "internal-reference"));
        }
    }

    private sealed class ConstantLivePriceFeed : ILiveMarketPriceFeed
    {
        public Task<PriceQuote> GetLivePriceAsync(
            AssetSymbol assetSymbol,
            QuoteCurrency quoteCurrency,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PriceQuote(1_010_000m, DateTimeOffset.UtcNow, "live-market"));
        }
    }

    private sealed class MutableLivePriceFeed(decimal unitPrice) : ILiveMarketPriceFeed
    {
        public decimal UnitPrice { get; set; } = unitPrice;

        public Task<PriceQuote> GetLivePriceAsync(
            AssetSymbol assetSymbol,
            QuoteCurrency quoteCurrency,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PriceQuote(UnitPrice, DateTimeOffset.UtcNow, "live-market"));
        }
    }

    private sealed class InMemoryQuoteStore : IBrokeredCryptoBuyQuoteStore
    {
        private readonly Dictionary<Guid, BrokeredCryptoBuyQuote> quotes = new();

        public Task StoreAsync(BrokeredCryptoBuyQuote quote, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            quotes[quote.QuoteId] = quote;
            return Task.CompletedTask;
        }

        public Task<BrokeredCryptoBuyQuote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            quotes.TryGetValue(quoteId, out var quote);
            return Task.FromResult(quote);
        }

        public void Replace(BrokeredCryptoBuyQuote quote)
        {
            quotes[quote.QuoteId] = quote;
        }
    }

    private sealed class InMemoryOwnershipLedger(decimal availableInventory) : ICryptoOwnershipLedger
    {
        private readonly Dictionary<AssetSymbol, decimal> platformInventory = new()
        {
            [AssetSymbol.Bitcoin] = availableInventory,
            [AssetSymbol.Ether] = 0m
        };
        private readonly Dictionary<(string CustomerAccountId, AssetSymbol AssetSymbol), decimal> customerHoldings = new();
        private readonly Dictionary<(string CustomerAccountId, AssetSymbol AssetSymbol, string ClientOrderId), BrokeredCryptoBuyReceipt> executed = new();
        private readonly HashSet<(string CustomerAccountId, AssetSymbol AssetSymbol, string ClientOrderId)> compensated = [];

        public Task<decimal> GetAvailablePlatformInventoryAsync(AssetSymbol assetSymbol, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            platformInventory.TryGetValue(assetSymbol, out var quantity);
            return Task.FromResult(quantity);
        }

        public Task<BrokeredCryptoBuyReceipt?> GetRecordedCustomerBuyAsync(
            string customerAccountId,
            AssetSymbol assetSymbol,
            string clientOrderId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            executed.TryGetValue((customerAccountId.Trim(), assetSymbol, clientOrderId.Trim()), out var existing);
            return Task.FromResult(existing);
        }

        public Task<BrokeredCryptoBuyReceipt> RecordCustomerBuyAsync(
            OwnershipLedgerBuyRecordCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            platformInventory.TryGetValue(command.AssetSymbol, out var currentInventory);
            if (command.InternalFillQuantity > currentInventory)
            {
                throw new InsufficientFundsException("insufficient inventory");
            }

            platformInventory[command.AssetSymbol] = checked(currentInventory - command.InternalFillQuantity);
            var holdingKey = (command.CustomerAccountId, command.AssetSymbol);
            customerHoldings.TryGetValue(holdingKey, out var currentHolding);
            customerHoldings[holdingKey] = checked(currentHolding + command.Quantity);
            var receipt = new BrokeredCryptoBuyReceipt(
                command.ClientOrderId,
                command.CustomerAccountId,
                command.AssetSymbol.Value,
                command.QuoteCurrency.Value,
                command.Quantity,
                command.InternalFillQuantity,
                command.ExternalHedgeQuantity,
                command.UnitPrice,
                command.TotalCost,
                command.ExecutedAtUtc,
                command.ExternalHedgeOrderId);
            executed[(command.CustomerAccountId, command.AssetSymbol, command.ClientOrderId)] = receipt;
            return Task.FromResult(receipt);
        }

        public Task CompensateCustomerBuyAsync(
            OwnershipLedgerBuyCompensationCommand command,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var executionKey = (command.CustomerAccountId.Trim(), command.AssetSymbol, command.ClientOrderId.Trim());
            if (compensated.Contains(executionKey))
            {
                return Task.CompletedTask;
            }

            if (!executed.TryGetValue(executionKey, out var execution))
            {
                throw new InvalidOperationException("buy execution not found");
            }

            var holdingKey = (execution.CustomerAccountId, command.AssetSymbol);
            customerHoldings.TryGetValue(holdingKey, out var currentHolding);
            if (currentHolding < execution.Quantity)
            {
                throw new InvalidOperationException("insufficient holding");
            }

            customerHoldings[holdingKey] = checked(currentHolding - execution.Quantity);
            platformInventory.TryGetValue(command.AssetSymbol, out var currentInventory);
            platformInventory[command.AssetSymbol] = checked(currentInventory + execution.InternalFillQuantity);
            compensated.Add(executionKey);
            return Task.CompletedTask;
        }

        public decimal GetCustomerHolding(string customerAccountId, AssetSymbol assetSymbol)
        {
            customerHoldings.TryGetValue((customerAccountId, assetSymbol), out var quantity);
            return quantity;
        }
    }
}
