namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record SignedTransaction(
    string AssetSymbol,
    string SignedPayload,
    decimal NetworkFee);
