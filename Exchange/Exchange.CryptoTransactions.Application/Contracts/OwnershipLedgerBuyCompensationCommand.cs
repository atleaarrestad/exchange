using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record OwnershipLedgerBuyCompensationCommand(
    string ClientOrderId,
    string CustomerAccountId,
    AssetSymbol AssetSymbol,
    string CompensationReason,
    DateTimeOffset CompensatedAtUtc);
