using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedLiveMarketPriceFeed(
    SimulatedMarketPricingOptions simulatedMarketPricingOptions) : ILiveMarketPriceFeed
{
    public async Task<PriceQuote> GetLivePriceAsync(
        AssetSymbol assetSymbol,
        QuoteCurrency quoteCurrency,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var latencyMs = Random.Shared.Next(simulatedMarketPricingOptions.MinLatencyMs, simulatedMarketPricingOptions.MaxLatencyMs + 1);
        if (latencyMs > 0)
        {
            await Task.Delay(latencyMs, cancellationToken);
        }

        var baseline = GetReferencePrice(assetSymbol, quoteCurrency);
        var deviationBps = Random.Shared.NextDouble() * (double)simulatedMarketPricingOptions.MaxDeviationBasisPoints;
        var isNegative = Random.Shared.Next(0, 2) == 0;
        var signedDeviation = isNegative ? -deviationBps : deviationBps;
        var multiplier = 1m + ((decimal)signedDeviation / 10_000m);
        var unitPrice = checked(baseline * multiplier);

        return new PriceQuote(unitPrice, DateTimeOffset.UtcNow, "simulated-live-market");
    }

    private decimal GetReferencePrice(AssetSymbol assetSymbol, QuoteCurrency quoteCurrency)
    {
        if (quoteCurrency != QuoteCurrency.NorwegianKrone)
        {
            throw new ArgumentOutOfRangeException(nameof(quoteCurrency), quoteCurrency.Value, "Only NOK quote currency is supported.");
        }

        return assetSymbol.Value switch
        {
            "BTC" => simulatedMarketPricingOptions.BitcoinReferencePriceNok,
            "ETH" => simulatedMarketPricingOptions.EtherReferencePriceNok,
            _ => throw new ArgumentOutOfRangeException(nameof(assetSymbol), assetSymbol.Value, "Unsupported asset symbol.")
        };
    }
}
