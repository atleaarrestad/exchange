using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record SignedTransaction(
    AssetSymbol AssetSymbol,
    string SignedPayload,
    decimal NetworkFee);
