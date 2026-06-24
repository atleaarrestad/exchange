using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using MassTransit;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class CryptoTransferSubmissionRequestedConsumer(
    CryptoTransferSubmissionProcessor processor,
    ICryptoTransferIdempotencyStore idempotencyStore) : IConsumer<CryptoTransferSubmissionRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CryptoTransferSubmissionRequestedIntegrationEvent> context)
    {
        var assetSymbol = AssetSymbol.Parse(context.Message.AssetSymbol, nameof(context.Message.AssetSymbol));
        var pending = await idempotencyStore.GetPendingAsync(
            context.Message.SourceAccountId,
            assetSymbol,
            context.Message.IdempotencyKey,
            context.CancellationToken);
        if (pending is null)
        {
            return;
        }

        await processor.ProcessOperationAsync(pending, context.CancellationToken);
    }
}
