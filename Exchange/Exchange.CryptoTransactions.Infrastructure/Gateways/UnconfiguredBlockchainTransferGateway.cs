using Exchange.CryptoTransactions.Application.Contracts;

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
}
