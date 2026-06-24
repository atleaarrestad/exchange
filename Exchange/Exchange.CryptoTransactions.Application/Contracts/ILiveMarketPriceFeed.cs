using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ILiveMarketPriceFeed
{
    Task<PriceQuote> GetLivePriceAsync(
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        CancellationToken cancellationToken = default);
}
