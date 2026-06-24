using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IInternalReferencePriceFeed
{
    Task<PriceQuote> GetReferencePriceAsync(
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        CancellationToken cancellationToken = default);
}
