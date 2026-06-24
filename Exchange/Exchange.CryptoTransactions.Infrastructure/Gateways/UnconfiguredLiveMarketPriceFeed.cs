using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class UnconfiguredLiveMarketPriceFeed : ILiveMarketPriceFeed
{
    public Task<PriceQuote> GetLivePriceAsync(
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ExternalDependencyNotConfiguredException(
            "No live market price feed is configured. Enable simulation or provide a real market data implementation.");
    }
}
