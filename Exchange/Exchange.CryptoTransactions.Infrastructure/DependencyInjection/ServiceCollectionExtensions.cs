using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
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

        var connectionString = configuration.GetValue<string>(InfrastructureConfigurationKeys.IdempotencyConnectionString)
            ?? InfrastructureConfigurationKeys.DefaultIdempotencyConnectionString;
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

        services.AddDbContextFactory<CryptoTransactionsDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
        services.AddSingleton(timeoutReconciliationOptions);
        services.AddSingleton(krakenGatewayOptions);
        services.AddSingleton(brokeredTradingOptions);
        services.AddSingleton(brokeredTradingPolicy);
        services.AddSingleton<IBrokeredTradingPolicyProvider, RuntimeBrokeredTradingPolicyProvider>();
        services.AddSingleton<IKrakenGatewayOptionsProvider, RuntimeKrakenGatewayOptionsProvider>();
        services.AddSingleton<ISubmitCryptoTransferCommandValidator, SubmitCryptoTransferCommandValidator>();
        services.AddSingleton<ICryptoSettingsCommandValidator, CryptoSettingsCommandValidator>();
        services.AddSingleton<ICryptoGatewaySettingsCommandValidator, CryptoGatewaySettingsCommandValidator>();
        services.AddSingleton<ICryptoTransferIdempotencyStore>(serviceProvider =>
            new EfCoreCryptoTransferIdempotencyStore(serviceProvider.GetRequiredService<IDbContextFactory<CryptoTransactionsDbContext>>()));
        services.AddSingleton<ICryptoTransferService, CryptoTransferService>();
        services.AddSingleton<ICryptoSettingsService, EfCoreCryptoSettingsService>();
        services.AddSingleton<ICryptoGatewaySettingsService, EfCoreCryptoGatewaySettingsService>();
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
        services.AddSingleton<IBlockchainTransferGateway, RuntimeKrakenBlockchainTransferGateway>();
        services.AddHostedService<CryptoTransferTimeoutReconciliationWorker>();
        services.AddHostedService<ExternalHedgeBatchExecutionWorker>();
        services.AddHostedService<SettingsChangeOutboxPublisherWorker>();
        services.AddHostedService<RuntimeSettingsBootstrapWorker>();
        return services;
    }
}
