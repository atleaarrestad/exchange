namespace Exchange.CryptoTransactions.Application;

public sealed record BrokeredCryptoBuyReceipt(
    string ClientOrderId,
    string CustomerAccountId,
    string AssetSymbol,
    string QuoteCurrency,
    decimal Quantity,
    decimal InternalFillQuantity,
    decimal ExternalHedgeQuantity,
    decimal UnitPrice,
    decimal TotalCost,
    DateTimeOffset ExecutedAtUtc,
    string? ExternalHedgeOrderId);
