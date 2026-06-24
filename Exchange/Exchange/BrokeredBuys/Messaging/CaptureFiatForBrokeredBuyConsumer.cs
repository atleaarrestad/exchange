using Exchange.FiatTransactions.Application.Contracts;
using Exchange.FiatTransactions.Domain.ValueObjects;
using MassTransit;

namespace Exchange.BrokeredBuys.Messaging;

public sealed class CaptureFiatForBrokeredBuyConsumer(
    IFiatLedger fiatLedger) : IConsumer<CaptureFiatForBrokeredBuy>
{
    public async Task Consume(ConsumeContext<CaptureFiatForBrokeredBuy> context)
    {
        try
        {
            await fiatLedger.CaptureReservedBrokeredBuySettlementAsync(
                new FiatLedgerBrokeredBuyReservationCaptureCommand(
                    context.Message.ClientOrderId,
                    context.Message.CustomerAccountId,
                    FiatCurrency.Parse(context.Message.FiatCurrency, nameof(context.Message.FiatCurrency)),
                    context.Message.CapturedAmount,
                    context.Message.CapturedAtUtc),
                context.CancellationToken);

            await context.Publish(new FiatCapturedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                context.Message.FiatCurrency,
                context.Message.CapturedAmount,
                context.Message.CapturedAtUtc), context.CancellationToken);
        }
        catch (Exception exception)
        {
            await context.Publish(new FiatCaptureFailedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                exception.Message,
                DateTimeOffset.UtcNow), context.CancellationToken);
        }
    }
}
