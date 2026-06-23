using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record PendingCryptoTransferOperation(
    string SourceAccountId,
    AssetSymbol AssetSymbol,
    string IdempotencyKey,
    string RequestFingerprint,
    decimal TotalDebit,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc);
