using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Microsoft.Extensions.Hosting;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class RuntimeSettingsBootstrapWorker(
    ICryptoSettingsService cryptoSettingsService,
    ICryptoGatewaySettingsService cryptoGatewaySettingsService,
    ICryptoGatewayResilienceSettingsService cryptoGatewayResilienceSettingsService,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    IKrakenGatewayOptionsProvider krakenGatewayOptionsProvider,
    IBlockchainGatewayResiliencePolicyOptionsProvider blockchainGatewayResiliencePolicyOptionsProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await cryptoSettingsService.GetAllAsync(cancellationToken);
        await cryptoGatewaySettingsService.GetAllAsync(cancellationToken);
        await cryptoGatewayResilienceSettingsService.GetAllAsync(cancellationToken);
        await tradingPolicyProvider.RefreshAsync(null, cancellationToken);
        await krakenGatewayOptionsProvider.RefreshAsync(null, cancellationToken);
        await blockchainGatewayResiliencePolicyOptionsProvider.RefreshAsync(null, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
