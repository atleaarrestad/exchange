namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record BlockchainTransferRequest(
    string IdempotencyKey,
    string SourceAccountId,
    string DestinationAddress,
    string AssetSymbol,
    decimal Amount,
    decimal NetworkFee,
    decimal TotalDebit);
