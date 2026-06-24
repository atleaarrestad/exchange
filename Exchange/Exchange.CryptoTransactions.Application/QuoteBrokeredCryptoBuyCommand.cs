namespace Exchange.CryptoTransactions.Application;

public sealed record QuoteBrokeredCryptoBuyCommand(
    string CustomerAccountId,
    string AssetSymbol,
    decimal Quantity,
    string QuoteCurrency);
