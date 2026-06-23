namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record BlockchainTransferStatus(
    BlockchainTransferStatusKind StatusKind,
    string? GatewayTransactionId = null,
    DateTimeOffset? SubmittedAtUtc = null,
    int RequiredConfirmations = 0);
