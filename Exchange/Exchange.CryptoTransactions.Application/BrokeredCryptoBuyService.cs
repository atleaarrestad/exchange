using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.Aggregates;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application;

public sealed class BrokeredCryptoBuyService(
    IInternalReferencePriceFeed internalReferencePriceFeed,
    ILiveMarketPriceFeed liveMarketPriceFeed,
    IBrokeredCryptoBuyQuoteStore quoteStore,
    ICryptoOwnershipLedger cryptoOwnershipLedger,
    IExternalHedgeBatchQueue externalHedgeBatchQueue,
    IExternalHedgeExecutionReadinessGate externalHedgeExecutionReadinessGate,
    IBrokeredTradingPolicyProvider tradingPolicyProvider) : IBrokeredCryptoBuyService
{
    public async Task<BrokeredCryptoBuyQuote> QuoteAsync(
        QuoteBrokeredCryptoBuyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command.CustomerAccountId, command.Quantity, command.QuoteCurrency);

        var assetSymbol = AssetSymbol.Parse(command.AssetSymbol, nameof(command.AssetSymbol));
        var quoteCurrency = QuoteCurrency.Parse(command.QuoteCurrency, nameof(command.QuoteCurrency));
        var tradingPolicy = tradingPolicyProvider.GetCurrent();
        var pricedBuy = await BuildPricedBuyAsync(
            command.CustomerAccountId,
            assetSymbol,
            quoteCurrency,
            command.Quantity,
            tradingPolicy,
            cancellationToken);
        await quoteStore.StoreAsync(pricedBuy.Quote, cancellationToken);

        return pricedBuy.Quote;
    }

    public async Task<BrokeredCryptoBuyReceipt> ExecuteAsync(
        ExecuteBrokeredCryptoBuyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ClientOrderId);
        Validate(command.CustomerAccountId, command.Quantity, command.QuoteCurrency);

        var assetSymbol = AssetSymbol.Parse(command.AssetSymbol, nameof(command.AssetSymbol));
        var quoteCurrency = QuoteCurrency.Parse(command.QuoteCurrency, nameof(command.QuoteCurrency));
        var normalizedClientOrderId = command.ClientOrderId.Trim();
        var now = DateTimeOffset.UtcNow;
        var existing = await cryptoOwnershipLedger.GetRecordedCustomerBuyAsync(
            command.CustomerAccountId,
            assetSymbol,
            normalizedClientOrderId,
            cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingDuplicate(command, existing);
            return existing;
        }

        var quote = await quoteStore.GetByIdAsync(command.QuoteId, cancellationToken)
            ?? throw new QuoteExecutionRejectedException($"Quote '{command.QuoteId}' was not found.");
        EnsureMatchingQuote(command, quote, now);

        EnforcePriceProtection(command, quote);
        var tradingPolicy = tradingPolicyProvider.GetCurrent();
        await EnforceConfiguredSlippageAsync(quote, assetSymbol, quoteCurrency, tradingPolicy, cancellationToken);

        var executedAtUtc = now;
        var ledgerRecord = new OwnershipLedgerBuyRecordCommand(
            normalizedClientOrderId,
            command.CustomerAccountId.Trim(),
            assetSymbol,
            quoteCurrency,
            quote.Quantity,
            quote.InternalFillQuantity,
            quote.ExternalHedgeQuantity,
            quote.UnitPrice,
            quote.TotalCost,
            executedAtUtc,
            null);

        if (quote.ExternalHedgeQuantity > 0m)
        {
            await externalHedgeExecutionReadinessGate.EnsureReadyAsync(cancellationToken);
        }

        var receipt = await cryptoOwnershipLedger.RecordCustomerBuyAsync(ledgerRecord, cancellationToken);
        if (quote.ExternalHedgeQuantity > 0m)
        {
            await externalHedgeBatchQueue.RegisterAsync(
                new BufferedExternalHedgeRequest(
                    command.CustomerAccountId.Trim(),
                    normalizedClientOrderId,
                    assetSymbol,
                    quoteCurrency,
                    quote.ExternalHedgeQuantity,
                    executedAtUtc),
                cancellationToken);
        }

        return receipt;
    }

    private async Task<PricedBuy> BuildPricedBuyAsync(
        string customerAccountId,
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        decimal quantity,
        BrokeredTradingPolicy tradingPolicy,
        CancellationToken cancellationToken)
    {
        tradingPolicy.Validate();

        var availableInventory = await cryptoOwnershipLedger.GetAvailablePlatformInventoryAsync(assetSymbol, cancellationToken);
        var requiresExternalHedge = quantity > availableInventory;
        var marketPrice = requiresExternalHedge
            ? await liveMarketPriceFeed.GetLivePriceAsync(assetSymbol, quoteCurrency, cancellationToken)
            : await internalReferencePriceFeed.GetReferencePriceAsync(assetSymbol, quoteCurrency, cancellationToken);

        var spreadBps = requiresExternalHedge
            ? tradingPolicy.ExternalHedgeSpreadBasisPoints
            : tradingPolicy.InternalOnlySpreadBasisPoints;

        var buy = BrokeredCryptoBuy.Create(
            customerAccountId,
            assetSymbol,
            quoteCurrency,
            quantity,
            availableInventory,
            marketPrice.UnitPrice,
            spreadBps,
            DateTimeOffset.UtcNow,
            tradingPolicy.QuoteTtl);

        var quote = new BrokeredCryptoBuyQuote(
            buy.Id,
            buy.CustomerAccountId,
            buy.AssetSymbol.Value,
            buy.QuoteCurrency.Value,
            buy.RequestedQuantity,
            buy.InternalFillQuantity,
            buy.ExternalHedgeQuantity,
            buy.UnitPrice,
            buy.TotalCost,
            marketPrice.AsOfUtc,
            buy.QuotedAtUtc,
            buy.ExpiresAtUtc,
            buy.RequiresExternalHedge,
            marketPrice.Source);

        return new PricedBuy(buy, quote);
    }

    private static void Validate(string customerAccountId, decimal quantity, string quoteCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteCurrency);
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");
        }
    }

    private static void EnforcePriceProtection(ExecuteBrokeredCryptoBuyCommand command, BrokeredCryptoBuyQuote quote)
    {
        if (command.MaxUnitPrice.HasValue && quote.UnitPrice > command.MaxUnitPrice.Value)
        {
            throw new PriceProtectionExceededException(
                $"Quoted unit price {quote.UnitPrice} exceeded max accepted unit price {command.MaxUnitPrice.Value}.");
        }

        if (command.MaxTotalCost.HasValue && quote.TotalCost > command.MaxTotalCost.Value)
        {
            throw new PriceProtectionExceededException(
                $"Quoted total cost {quote.TotalCost} exceeded max accepted total cost {command.MaxTotalCost.Value}.");
        }
    }

    private async Task EnforceConfiguredSlippageAsync(
        BrokeredCryptoBuyQuote quote,
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        BrokeredTradingPolicy tradingPolicy,
        CancellationToken cancellationToken)
    {
        var marketPrice = quote.RequiresExternalHedge
            ? await liveMarketPriceFeed.GetLivePriceAsync(assetSymbol, quoteCurrency, cancellationToken)
            : await internalReferencePriceFeed.GetReferencePriceAsync(assetSymbol, quoteCurrency, cancellationToken);
        var spreadBasisPoints = quote.RequiresExternalHedge
            ? tradingPolicy.ExternalHedgeSpreadBasisPoints
            : tradingPolicy.InternalOnlySpreadBasisPoints;
        var expectedCurrentUnitPrice = checked(marketPrice.UnitPrice * (1m + (spreadBasisPoints / 10_000m)));
        var slippageBasisPoints = CalculateSlippageBasisPoints(quote.UnitPrice, expectedCurrentUnitPrice);
        if (slippageBasisPoints > tradingPolicy.MaxAllowedSlippageBasisPoints)
        {
            throw new QuoteExecutionRejectedException(
                $"Quote '{quote.QuoteId}' exceeded max configured slippage of {tradingPolicy.MaxAllowedSlippageBasisPoints} bps. Observed slippage: {slippageBasisPoints} bps.");
        }
    }

    private static decimal CalculateSlippageBasisPoints(decimal quotedUnitPrice, decimal currentUnitPrice)
    {
        var priceDelta = Math.Abs(currentUnitPrice - quotedUnitPrice);
        return checked((priceDelta / quotedUnitPrice) * 10_000m);
    }

    private static void EnsureMatchingDuplicate(ExecuteBrokeredCryptoBuyCommand command, BrokeredCryptoBuyReceipt existing)
    {
        if (!string.Equals(existing.CustomerAccountId, command.CustomerAccountId.Trim(), StringComparison.Ordinal) ||
            !string.Equals(existing.AssetSymbol, command.AssetSymbol.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(existing.QuoteCurrency, command.QuoteCurrency.Trim(), StringComparison.OrdinalIgnoreCase) ||
            existing.Quantity != command.Quantity)
        {
            throw new IdempotencyKeyConflictException(
                $"Client order id '{command.ClientOrderId}' was already used with a different brokered buy request.");
        }
    }

    private static void EnsureMatchingQuote(
        ExecuteBrokeredCryptoBuyCommand command,
        BrokeredCryptoBuyQuote quote,
        DateTimeOffset now)
    {
        if (quote.ExpiresAtUtc <= now)
        {
            throw new QuoteExecutionRejectedException(
                $"Quote '{quote.QuoteId}' expired at {quote.ExpiresAtUtc:O}.");
        }

        if (!string.Equals(quote.CustomerAccountId, command.CustomerAccountId.Trim(), StringComparison.Ordinal) ||
            !string.Equals(quote.AssetSymbol, command.AssetSymbol.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(quote.QuoteCurrency, command.QuoteCurrency.Trim(), StringComparison.OrdinalIgnoreCase) ||
            quote.Quantity != command.Quantity)
        {
            throw new QuoteExecutionRejectedException(
                $"Quote '{quote.QuoteId}' does not match the execute request.");
        }
    }

    private sealed record PricedBuy(BrokeredCryptoBuy Buy, BrokeredCryptoBuyQuote Quote);
}
