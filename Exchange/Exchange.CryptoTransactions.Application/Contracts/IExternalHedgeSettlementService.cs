using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IExternalHedgeSettlementService
{
    Task RegisterExecutionAsync(ExternalHedgeExecutionObservation observation, CancellationToken cancellationToken = default);
    Task SettleAsync(string externalOrderId, CancellationToken cancellationToken = default);
}

public sealed record ExternalHedgeExecutionObservation(
    string ExternalOrderId,
    AssetSymbol AssetSymbol,
    QuoteCurrency QuoteCurrency,
    decimal ExecutedQuantity,
    decimal ExecutedUnitPrice,
    DateTimeOffset ExecutedAtUtc);
