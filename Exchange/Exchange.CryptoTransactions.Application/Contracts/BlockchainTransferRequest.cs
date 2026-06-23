using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record BlockchainTransferRequest(
    string IdempotencyKey,
    string SourceAccountId,
    string DestinationAddress,
    AssetSymbol AssetSymbol,
    decimal Amount,
    decimal NetworkFee,
    decimal TotalDebit);
