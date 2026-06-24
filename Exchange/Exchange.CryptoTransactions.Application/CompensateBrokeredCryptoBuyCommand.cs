namespace Exchange.CryptoTransactions.Application;

public sealed record CompensateBrokeredCryptoBuyCommand(
    string ClientOrderId,
    string CustomerAccountId,
    string AssetSymbol,
    string CompensationReason,
    DateTimeOffset CompensatedAtUtc);
