namespace Exchange.CryptoTransactions.Application.Messaging;

public sealed record CryptoTransferSubmissionRequestedIntegrationEvent(
    string SourceAccountId,
    string AssetSymbol,
    string IdempotencyKey);
