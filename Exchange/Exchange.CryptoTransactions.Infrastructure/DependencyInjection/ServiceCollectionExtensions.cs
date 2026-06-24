using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCryptoTransactionsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetValue<string>(InfrastructureConfigurationKeys.IdempotencySqliteConnectionString)
            ?? InfrastructureConfigurationKeys.DefaultIdempotencySqliteConnectionString;
        var timeoutReconciliationOptions = TimeoutReconciliationOptions.FromConfiguration(configuration);
        var krakenGatewayOptions = KrakenBlockchainTransferGatewayOptions.FromConfiguration(configuration);
        var brokeredTradingOptions = BrokeredTradingOptions.FromConfiguration(configuration);
        var brokeredTradingPolicy = new BrokeredTradingPolicy
        {
            QuoteTtlSeconds = brokeredTradingOptions.QuoteTtlSeconds,
            InternalOnlySpreadBasisPoints = brokeredTradingOptions.InternalOnlySpreadBasisPoints,
            ExternalHedgeSpreadBasisPoints = brokeredTradingOptions.ExternalHedgeSpreadBasisPoints,
            MaxAllowedSlippageBasisPoints = brokeredTradingOptions.MaxAllowedSlippageBasisPoints,
            MaxBufferedHedgeCustomerBuys = brokeredTradingOptions.MaxBufferedHedgeCustomerBuys,
            MaxBufferedHedgeDelaySeconds = brokeredTradingOptions.MaxBufferedHedgeDelaySeconds
        };
        brokeredTradingPolicy.Validate();

        services.AddDbContextFactory<CryptoTransactionsDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton(timeoutReconciliationOptions);
        services.AddSingleton(krakenGatewayOptions);
        services.AddSingleton(brokeredTradingOptions);
        services.AddSingleton(brokeredTradingPolicy);
        services.AddSingleton<ISubmitCryptoTransferCommandValidator, SubmitCryptoTransferCommandValidator>();
        services.AddSingleton<ICryptoTransferIdempotencyStore>(serviceProvider =>
            new SqliteCryptoTransferIdempotencyStore(serviceProvider.GetRequiredService<IDbContextFactory<CryptoTransactionsDbContext>>()));
        services.AddSingleton<ICryptoTransferService, CryptoTransferService>();
        services.AddSingleton<ICryptoTransferTimeoutReconciler, CryptoTransferTimeoutReconciler>();
        services.AddSingleton<IBrokeredCryptoBuyService, BrokeredCryptoBuyService>();
        services.AddSingleton<IBrokeredCryptoBuyQuoteStore, InMemoryBrokeredCryptoBuyQuoteStore>();
        services.AddSingleton<ICryptoOwnershipLedger, InMemoryCryptoOwnershipLedger>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IExternalHedgeBatchQueue, InMemoryExternalHedgeBatchQueue>();
        services.AddSingleton<IInternalReferencePriceFeed, StaticReferencePriceFeed>();
        services.AddSingleton<ILiveMarketPriceFeed, UnconfiguredLiveMarketPriceFeed>();
        services.AddSingleton<IExternalLiquidityHedgingGateway, UnconfiguredExternalLiquidityHedgingGateway>();
        services.AddSingleton<ICryptoTransferFundsReservationGateway, UnconfiguredCryptoTransferFundsReservationGateway>();
        if (krakenGatewayOptions.Enabled)
        {
            services.AddSingleton<IBlockchainTransferGateway>(_ =>
            {
                var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(krakenGatewayOptions.BaseUrl),
                    Timeout = TimeSpan.FromSeconds(krakenGatewayOptions.HttpTimeoutSeconds)
                };
                return new KrakenBlockchainTransferGateway(krakenGatewayOptions, httpClient, TimeProvider.System);
            });
        }
        else
        {
            services.AddSingleton<IBlockchainTransferGateway, UnconfiguredBlockchainTransferGateway>();
        }
        services.AddHostedService<CryptoTransferTimeoutReconciliationWorker>();
        services.AddHostedService<ExternalHedgeBatchExecutionWorker>();
        return services;
    }
}
