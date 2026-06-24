using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed record HedgePurchaseRequest(
    string ClientOrderId,
    AssetSymbol AssetSymbol,
    QuoteCurrency QuoteCurrency,
    decimal Quantity);
