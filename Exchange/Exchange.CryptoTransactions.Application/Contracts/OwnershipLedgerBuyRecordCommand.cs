using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record OwnershipLedgerBuyRecordCommand(
    string ClientOrderId,
    string CustomerAccountId,
    AssetSymbol AssetSymbol,
    QuoteCurrency QuoteCurrency,
    decimal Quantity,
    decimal InternalFillQuantity,
    decimal ExternalHedgeQuantity,
    decimal UnitPrice,
    decimal TotalCost,
    DateTimeOffset ExecutedAtUtc,
    string? ExternalHedgeOrderId);
