namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record HedgePurchaseResult(
    string ExternalOrderId,
    decimal ExecutedQuantity,
    decimal ExecutedUnitPrice,
    DateTimeOffset ExecutedAtUtc);
