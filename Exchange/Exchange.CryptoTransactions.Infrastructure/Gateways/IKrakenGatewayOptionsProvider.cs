namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public interface IKrakenGatewayOptionsProvider
{
    KrakenBlockchainTransferGatewayOptions GetCurrent();

    Task RefreshAsync(Guid? profileId, CancellationToken cancellationToken = default);
}
