using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class UnconfiguredBlockchainTransferGateway : IBlockchainTransferGateway
{
    public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new ExternalDependencyNotConfiguredException(
            "No blockchain transfer gateway is configured. Enable simulation or provide a real gateway implementation.");
    }

    public Task<BlockchainTransferStatus> GetTransferStatusAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();
        throw new ExternalDependencyNotConfiguredException(
            "No blockchain transfer gateway is configured. Enable simulation or provide a real gateway implementation.");
    }
}
