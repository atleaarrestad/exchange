using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record BuiltTransaction(
    AssetSymbol AssetSymbol,
    string Payload,
    decimal NetworkFee);
