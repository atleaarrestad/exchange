using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class RuntimeSettingsBootstrapWorker(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    ICryptoSettingsService cryptoSettingsService,
    ICryptoGatewaySettingsService cryptoGatewaySettingsService,
    ICryptoGatewayResilienceSettingsService cryptoGatewayResilienceSettingsService,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    IKrakenGatewayOptionsProvider krakenGatewayOptionsProvider,
    IBlockchainGatewayResiliencePolicyOptionsProvider blockchainGatewayResiliencePolicyOptionsProvider,
    IHostEnvironment hostEnvironment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await ValidateSchemaAsync(context, hostEnvironment, cancellationToken);
        await cryptoSettingsService.GetAllAsync(cancellationToken);
        await cryptoGatewaySettingsService.GetAllAsync(cancellationToken);
        await cryptoGatewayResilienceSettingsService.GetAllAsync(cancellationToken);
        await tradingPolicyProvider.RefreshAsync(null, cancellationToken);
        await krakenGatewayOptionsProvider.RefreshAsync(null, cancellationToken);
        await blockchainGatewayResiliencePolicyOptionsProvider.RefreshAsync(null, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task ValidateSchemaAsync(
        CryptoTransactionsDbContext context,
        IHostEnvironment hostEnvironment,
        CancellationToken cancellationToken)
    {
        try
        {
            await VerifySchemaShapeAsync(context, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedColumn or PostgresErrorCodes.UndefinedTable)
        {
            if (hostEnvironment.IsDevelopment())
            {
                await context.Database.EnsureDeletedAsync(cancellationToken);
                await context.Database.EnsureCreatedAsync(cancellationToken);
                await VerifySchemaShapeAsync(context, cancellationToken);
                return;
            }

            throw new InvalidOperationException(
                "Detected a stale local database schema while migrations are disabled. Drop and recreate the development database, then start the worker again.",
                ex);
        }
    }

    private static async Task VerifySchemaShapeAsync(CryptoTransactionsDbContext context, CancellationToken cancellationToken)
    {
        await context.BackgroundWorkerHeartbeats
            .AsNoTracking()
            .Select(entity => entity.WorkerName)
            .Take(1)
            .ToListAsync(cancellationToken);
        await context.CryptoTransferIdempotencyReceipts
            .AsNoTracking()
            .Select(entity => new
            {
                entity.Amount,
                entity.NetworkFee,
                entity.DestinationAddress
            })
            .Take(1)
            .ToListAsync(cancellationToken);
        await context.SettingsChangeOutboxEntries
            .AsNoTracking()
            .Select(entity => new
            {
                entity.LeaseExpiresAtUtc,
                entity.LeaseOwnerId,
                entity.LeaseToken
            })
            .Take(1)
            .ToListAsync(cancellationToken);
        await context.ExternalHedgeBatchEntries
            .AsNoTracking()
            .Select(entity => new
            {
                entity.LeaseExpiresAtUtc,
                entity.LeaseOwnerId,
                entity.LeaseToken
            })
            .Take(1)
            .ToListAsync(cancellationToken);
    }
}
