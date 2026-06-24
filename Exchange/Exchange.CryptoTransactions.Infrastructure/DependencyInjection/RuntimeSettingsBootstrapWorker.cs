using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class RuntimeSettingsBootstrapWorker(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    ICryptoSettingsService cryptoSettingsService,
    ICryptoGatewaySettingsService cryptoGatewaySettingsService,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    IKrakenGatewayOptionsProvider krakenGatewayOptionsProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        await cryptoSettingsService.GetAllAsync(cancellationToken);
        await cryptoGatewaySettingsService.GetAllAsync(cancellationToken);
        await tradingPolicyProvider.RefreshAsync(null, cancellationToken);
        await krakenGatewayOptionsProvider.RefreshAsync(null, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
