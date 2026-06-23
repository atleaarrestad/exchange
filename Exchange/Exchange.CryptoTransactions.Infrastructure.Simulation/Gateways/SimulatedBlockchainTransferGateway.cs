using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using System.Collections.Frozen;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedBlockchainTransferGateway(
    IEnumerable<IBlockchainTransferStrategy> strategies) : IBlockchainTransferGateway
{
    private readonly Dictionary<(string SourceAccountId, AssetSymbol AssetSymbol, string IdempotencyKey), BlockchainTransferResult> submittedTransfers =
        new();
    private readonly Lock submittedTransfersGate = new();

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

        return SubmitAndTrackAsync(strategy, request, cancellationToken);
    }

    public Task<BlockchainTransferStatus> GetTransferStatusAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = (sourceAccountId.Trim(), assetSymbol, idempotencyKey.Trim());

        lock (submittedTransfersGate)
        {
            if (submittedTransfers.TryGetValue(key, out var existingTransfer))
            {
                return Task.FromResult(new BlockchainTransferStatus(
                    BlockchainTransferStatusKind.Submitted,
                    existingTransfer.GatewayTransactionId,
                    existingTransfer.SubmittedAtUtc,
                    existingTransfer.RequiredConfirmations));
            }
        }

        return Task.FromResult(new BlockchainTransferStatus(BlockchainTransferStatusKind.NotSubmitted));
    }

    private async Task<BlockchainTransferResult> SubmitAndTrackAsync(
        IBlockchainTransferStrategy strategy,
        BlockchainTransferRequest request,
        CancellationToken cancellationToken)
    {
        var transferResult = await strategy.SubmitAsync(request, cancellationToken);
        var key = (request.SourceAccountId.Trim(), request.AssetSymbol, request.IdempotencyKey.Trim());

        lock (submittedTransfersGate)
        {
            submittedTransfers[key] = transferResult;
        }

        return transferResult;
    }
}
