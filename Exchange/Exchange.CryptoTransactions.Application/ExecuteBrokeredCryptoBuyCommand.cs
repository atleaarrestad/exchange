namespace Exchange.CryptoTransactions.Application;

public sealed record ExecuteBrokeredCryptoBuyCommand(
    Guid QuoteId,
    string ClientOrderId,
    string CustomerAccountId,
    string AssetSymbol,
    decimal Quantity,
    string QuoteCurrency,
    decimal? MaxUnitPrice = null,
    decimal? MaxTotalCost = null);
