namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record BuiltTransaction(
    string AssetSymbol,
    string Payload,
    decimal NetworkFee);
