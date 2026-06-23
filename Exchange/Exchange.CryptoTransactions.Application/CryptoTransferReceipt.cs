namespace Exchange.CryptoTransactions.Application;

public sealed record CryptoTransferReceipt(
    Guid TransferId,
    string GatewayTransactionId,
    DateTimeOffset SubmittedAtUtc,
    decimal TotalDebit);
