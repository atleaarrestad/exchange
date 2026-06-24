using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class StaticReferencePriceFeed(BrokeredTradingOptions options) : IInternalReferencePriceFeed
{
    public Task<PriceQuote> GetReferencePriceAsync(
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var price = options.GetReferencePrice(assetSymbol, quoteCurrency);
        return Task.FromResult(new PriceQuote(price, DateTimeOffset.UtcNow, "internal-reference"));
    }
}
