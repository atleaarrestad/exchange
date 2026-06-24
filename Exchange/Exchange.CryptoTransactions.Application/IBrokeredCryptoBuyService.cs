namespace Exchange.CryptoTransactions.Application;

public interface IBrokeredCryptoBuyService
{
    Task<BrokeredCryptoBuyQuote> QuoteAsync(QuoteBrokeredCryptoBuyCommand command, CancellationToken cancellationToken = default);
    Task<BrokeredCryptoBuyReceipt> ExecuteAsync(ExecuteBrokeredCryptoBuyCommand command, CancellationToken cancellationToken = default);
    Task CompensateAsync(CompensateBrokeredCryptoBuyCommand command, CancellationToken cancellationToken = default);
}
