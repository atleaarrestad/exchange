using Exchange.CryptoTransactions.Application;
using MassTransit;

namespace Exchange.BrokeredBuys.Messaging;

public sealed class BookCryptoForBrokeredBuyConsumer(
    IBrokeredCryptoBuyService brokeredCryptoBuyService) : IConsumer<BookCryptoForBrokeredBuy>
{
    public async Task Consume(ConsumeContext<BookCryptoForBrokeredBuy> context)
    {
        try
        {
            var receipt = await brokeredCryptoBuyService.ExecuteAsync(
                new ExecuteBrokeredCryptoBuyCommand(
                    context.Message.QuoteId,
                    context.Message.ClientOrderId,
                    context.Message.CustomerAccountId,
                    context.Message.AssetSymbol,
                    context.Message.Quantity,
                    context.Message.QuoteCurrency,
                    context.Message.MaxUnitPrice,
                    context.Message.MaxTotalCost),
                context.CancellationToken);

            await context.Publish(new CryptoBookedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                receipt.QuoteCurrency,
                receipt.TotalCost,
                receipt.ExecutedAtUtc), context.CancellationToken);
        }
        catch (Exception exception)
        {
            await context.Publish(new CryptoBookingFailedForBrokeredBuy(
                context.Message.CorrelationId,
                context.Message.ClientOrderId,
                context.Message.CustomerAccountId,
                exception.Message,
                DateTimeOffset.UtcNow), context.CancellationToken);
        }
    }
}
