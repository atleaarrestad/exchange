using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using System.Collections.Frozen;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedBlockchainTransferGateway(
    IEnumerable<IBlockchainTransferStrategy> strategies) : IBlockchainTransferGateway
{
    private readonly FrozenDictionary<AssetSymbol, IBlockchainTransferStrategy> strategiesByAsset =
        strategies.ToFrozenDictionary(
            static strategy => strategy.AssetSymbol,
            static strategy => strategy,
            EqualityComparer<AssetSymbol>.Default);

    public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var assetSymbol = request.AssetSymbol;

        if (!strategiesByAsset.TryGetValue(assetSymbol, out var strategy))
        {
            throw new BlockchainTransferRejectedException($"No simulation transfer strategy is registered for asset '{assetSymbol.Value}'.");
        }

        return strategy.SubmitAsync(request, cancellationToken);
    }
}
