using Exchange.CryptoTransactions.Application.Contracts;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedExternalLiquidityHedgingGateway(ILiveMarketPriceFeed liveMarketPriceFeed) : IExternalLiquidityHedgingGateway
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, HedgePurchaseResult> executedOrders = new(StringComparer.Ordinal);

    public async Task<HedgePurchaseResult> BuyAsync(HedgePurchaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedOrderId = request.ClientOrderId.Trim();
        lock (gate)
        {
            if (executedOrders.TryGetValue(normalizedOrderId, out var existing))
            {
                return existing;
            }
        }

        var livePrice = await liveMarketPriceFeed.GetLivePriceAsync(request.AssetSymbol, request.QuoteCurrency, cancellationToken);
        var result = new HedgePurchaseResult(
            $"sim-hedge-{Guid.CreateVersion7()}",
            request.Quantity,
            livePrice.UnitPrice,
            DateTimeOffset.UtcNow);

        lock (gate)
        {
            if (executedOrders.TryGetValue(normalizedOrderId, out var existing))
            {
                return existing;
            }

            executedOrders[normalizedOrderId] = result;
            return result;
        }
    }
}
