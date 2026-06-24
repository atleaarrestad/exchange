namespace Exchange.Contracts;

public sealed record QuoteBrokeredCryptoBuyRequest(
    string CustomerAccountId,
    string AssetSymbol,
    decimal Quantity,
    string QuoteCurrency);

public sealed record ExecuteBrokeredCryptoBuyRequest(
    Guid QuoteId,
    string ClientOrderId,
    string CustomerAccountId,
    string AssetSymbol,
    decimal Quantity,
    string QuoteCurrency,
    decimal? MaxUnitPrice = null,
    decimal? MaxTotalCost = null);
