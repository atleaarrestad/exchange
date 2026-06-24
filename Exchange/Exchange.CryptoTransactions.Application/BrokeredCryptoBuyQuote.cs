namespace Exchange.CryptoTransactions.Application;

public sealed record BrokeredCryptoBuyQuote(
    Guid QuoteId,
    string CustomerAccountId,
    string AssetSymbol,
    string QuoteCurrency,
    decimal Quantity,
    decimal InternalFillQuantity,
    decimal ExternalHedgeQuantity,
    decimal UnitPrice,
    decimal TotalCost,
    DateTimeOffset MarketPriceObservedAtUtc,
    DateTimeOffset QuotedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool RequiresExternalHedge,
    string PriceSource);
