using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.FiatTransactions.Application.Contracts;
using Exchange.FiatTransactions.Domain.ValueObjects;
using MassTransit;

namespace Exchange.BrokeredBuys.Messaging;

public sealed class ReserveFiatForBrokeredBuyConsumer(
    IBrokeredCryptoBuyQuoteStore quoteStore,
    IFiatLedger fiatLedger) : IConsumer<ReserveFiatForBrokeredBuy>
{
    public async Task Consume(ConsumeContext<ReserveFiatForBrokeredBuy> context)
    {
        try
        {
            var quote = await quoteStore.GetByIdAsync(context.Message.QuoteId, context.CancellationToken);
            if (quote is null)
            {
                throw new InvalidOperationException($"Quote '{context.Message.QuoteId}' was not found.");
            }

            if (quote.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException($"Quote '{quote.QuoteId}' expired at {quote.ExpiresAtUtc:O}.");
            }

            if (!string.Equals(quote.CustomerAccountId, context.Message.CustomerAccountId.Trim(), StringComparison.Ordinal) ||
                !string.Equals(quote.QuoteCurrency, context.Message.FiatCurrency.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Quote does not match reservation request.");
            }

            await fiatLedger.ReserveBrokeredBuyFundsAsync(
                new FiatLedgerBrokeredBuyReservationCommand(
                    context.Message.ClientOrderId,
                    context.Message.CustomerAccountId,
                    FiatCurrency.Parse(context.Message.FiatCurrency, nameof(context.Message.FiatCurrency)),
                    quote.TotalCost,
                    context.Message.ReservedAtUtc),
                context.CancellationToken);

            await context.Publish(new FiatReservedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                context.Message.FiatCurrency,
                quote.TotalCost,
                context.Message.ReservedAtUtc), context.CancellationToken);
        }
        catch (Exception exception)
        {
            await context.Publish(new FiatReservationFailedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                exception.Message,
                DateTimeOffset.UtcNow), context.CancellationToken);
        }
    }
}
