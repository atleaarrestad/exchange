namespace Exchange.CryptoTransactions.Application;

public sealed record SubmitCryptoTransferCommand(
    string IdempotencyKey,
    string SourceAccountId,
    string DestinationAddress,
    string AssetSymbol,
    decimal Amount,
    decimal NetworkFee);
