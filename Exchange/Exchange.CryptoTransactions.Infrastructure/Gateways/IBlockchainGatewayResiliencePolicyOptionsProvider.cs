using Exchange.CryptoTransactions.Resilience.Gateways;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public interface IBlockchainGatewayResiliencePolicyOptionsProvider
{
    BlockchainGatewayResiliencePolicyOptions GetCurrent();

    Task RefreshAsync(Guid? profileId, CancellationToken cancellationToken = default);
}
