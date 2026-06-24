using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class InMemoryBrokeredCryptoBuyQuoteStore : IBrokeredCryptoBuyQuoteStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, BrokeredCryptoBuyQuote> quotes = new();

    public Task StoreAsync(BrokeredCryptoBuyQuote quote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quote);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            quotes[quote.QuoteId] = quote;
            return Task.CompletedTask;
        }
    }

    public Task<BrokeredCryptoBuyQuote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            quotes.TryGetValue(quoteId, out var quote);
            return Task.FromResult(quote);
        }
    }
}
