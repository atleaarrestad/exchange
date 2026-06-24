namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IBrokeredCryptoBuyQuoteStore
{
    Task StoreAsync(BrokeredCryptoBuyQuote quote, CancellationToken cancellationToken = default);
    Task<BrokeredCryptoBuyQuote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default);
}
