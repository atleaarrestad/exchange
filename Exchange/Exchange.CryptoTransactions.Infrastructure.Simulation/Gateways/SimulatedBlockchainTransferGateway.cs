using Exchange.CryptoTransactions.Application.Contracts;
using System.Collections.Frozen;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedBlockchainTransferGateway(
    IEnumerable<IBlockchainTransferStrategy> strategies) : IBlockchainTransferGateway
{
    private readonly FrozenDictionary<string, IBlockchainTransferStrategy> strategiesByAsset =
        strategies.ToFrozenDictionary(
            static strategy => strategy.AssetSymbol.Trim().ToUpperInvariant(),
            static strategy => strategy,
            StringComparer.Ordinal);

    public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var assetSymbol = request.AssetSymbol.Trim().ToUpperInvariant();

        if (!strategiesByAsset.TryGetValue(assetSymbol, out var strategy))
        {
            throw new BlockchainTransferRejectedException($"No simulation transfer strategy is registered for asset '{assetSymbol}'.");
        }

        return strategy.SubmitAsync(request, cancellationToken);
    }
}
