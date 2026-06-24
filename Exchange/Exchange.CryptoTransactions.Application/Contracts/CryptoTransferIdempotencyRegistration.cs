namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record CryptoTransferIdempotencyRegistration(
    bool CreatedPending,
    CryptoTransferReceipt? CompletedReceipt);
