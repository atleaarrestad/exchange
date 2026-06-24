using Exchange.CryptoTransactions.Application.Contracts;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class UnconfiguredExternalLiquidityHedgingGateway : IExternalLiquidityHedgingGateway
{
    public Task<HedgePurchaseResult> BuyAsync(HedgePurchaseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new ExternalDependencyNotConfiguredException(
            "No external liquidity hedging gateway is configured. Enable simulation or provide a real external execution implementation.");
    }
}
