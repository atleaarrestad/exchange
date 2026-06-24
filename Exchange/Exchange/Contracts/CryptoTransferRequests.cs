namespace Exchange.Contracts;

public sealed record SubmitCryptoTransferRequest(
    string IdempotencyKey,
    string SourceAccountId,
    string DestinationAddress,
    string AssetSymbol,
    decimal Amount,
    decimal NetworkFee);
