namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record BlockchainTransferResult(
    string GatewayTransactionId,
    DateTimeOffset SubmittedAtUtc);
