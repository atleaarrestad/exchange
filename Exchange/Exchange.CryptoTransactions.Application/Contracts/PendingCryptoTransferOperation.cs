using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record PendingCryptoTransferOperation(
    string SourceAccountId,
    AssetSymbol AssetSymbol,
    string IdempotencyKey,
    string RequestFingerprint,
    decimal TotalDebit,
    string DestinationAddress,
    decimal Amount,
    decimal NetworkFee,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastUpdatedAtUtc);
