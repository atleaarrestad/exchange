using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IExternalHedgeBatchQueue
{
    Task RegisterAsync(BufferedExternalHedgeRequest request, CancellationToken cancellationToken = default);
    Task ExecuteDueAsync(CancellationToken cancellationToken = default);
}

public sealed record BufferedExternalHedgeRequest(
    string CustomerAccountId,
    string ClientOrderId,
    AssetSymbol AssetSymbol,
    QuoteCurrency QuoteCurrency,
    decimal Quantity,
    DateTimeOffset RequestedAtUtc);
