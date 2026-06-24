using Exchange.CryptoTransactions.Application;
using MassTransit;

namespace Exchange.BrokeredBuys.Messaging;

public sealed class ReverseCryptoBookingForBrokeredBuyConsumer(
    IBrokeredCryptoBuyService brokeredCryptoBuyService) : IConsumer<ReverseCryptoBookingForBrokeredBuy>
{
    public async Task Consume(ConsumeContext<ReverseCryptoBookingForBrokeredBuy> context)
    {
        try
        {
            await brokeredCryptoBuyService.CompensateAsync(
                new CompensateBrokeredCryptoBuyCommand(
                    context.Message.ClientOrderId,
                    context.Message.CustomerAccountId,
                    context.Message.AssetSymbol,
                    context.Message.CompensationReason,
                    context.Message.RequestedAtUtc),
                context.CancellationToken);

            await context.Publish(new CryptoBookingReversedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                DateTimeOffset.UtcNow), context.CancellationToken);
        }
        catch (Exception exception)
        {
            await context.Publish(new CryptoBookingReverseFailedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                exception.Message,
                DateTimeOffset.UtcNow), context.CancellationToken);
        }
    }
}
