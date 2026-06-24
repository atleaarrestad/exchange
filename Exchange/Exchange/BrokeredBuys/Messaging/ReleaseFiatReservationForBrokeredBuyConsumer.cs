using Exchange.FiatTransactions.Application.Contracts;
using Exchange.FiatTransactions.Domain.ValueObjects;
using MassTransit;

namespace Exchange.BrokeredBuys.Messaging;

public sealed class ReleaseFiatReservationForBrokeredBuyConsumer(
    IFiatLedger fiatLedger) : IConsumer<ReleaseFiatReservationForBrokeredBuy>
{
    public async Task Consume(ConsumeContext<ReleaseFiatReservationForBrokeredBuy> context)
    {
        try
        {
            await fiatLedger.ReleaseReservedBrokeredBuyFundsAsync(
                new FiatLedgerBrokeredBuyReservationReleaseCommand(
                    context.Message.ClientOrderId,
                    context.Message.CustomerAccountId,
                    FiatCurrency.Parse(context.Message.FiatCurrency, nameof(context.Message.FiatCurrency)),
                    context.Message.ReleasedAmount,
                    context.Message.ReleasedAtUtc),
                context.CancellationToken);

            await context.Publish(new FiatReservationReleasedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                context.Message.FiatCurrency,
                context.Message.ReleasedAmount,
                context.Message.ReleasedAtUtc), context.CancellationToken);
        }
        catch (Exception exception)
        {
            await context.Publish(new FiatReservationReleaseFailedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                exception.Message,
                DateTimeOffset.UtcNow), context.CancellationToken);
        }
    }
}
