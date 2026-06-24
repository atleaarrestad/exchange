using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Messaging;
using MassTransit;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class CryptoTransferSubmissionSignal(IBus bus) : ICryptoTransferSubmissionSignal
{
    public Task SignalPendingAsync(PendingCryptoTransferOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return bus.Publish(
            new CryptoTransferSubmissionRequestedIntegrationEvent(
                operation.SourceAccountId,
                operation.AssetSymbol.Value,
                operation.IdempotencyKey),
            cancellationToken);
    }
}
