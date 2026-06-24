using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Exchange.CryptoTransactions.Resilience.Gateways;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCryptoTransactionsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool includeBackgroundWorkers = true,
        bool includeBootstrapWorker = true)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetValue<string>(InfrastructureConfigurationKeys.IdempotencyConnectionString)
            ?? InfrastructureConfigurationKeys.DefaultIdempotencyConnectionString;
        var timeoutReconciliationOptions = TimeoutReconciliationOptions.FromConfiguration(configuration);
        var krakenGatewayOptions = KrakenBlockchainTransferGatewayOptions.FromConfiguration(configuration);
        var blockchainGatewayResilienceOptions = BlockchainGatewayResilienceOptions.FromConfiguration(configuration);
        var blockchainGatewayResiliencePolicyOptions = blockchainGatewayResilienceOptions.ToPolicyOptions();
        var brokeredTradingOptions = BrokeredTradingOptions.FromConfiguration(configuration);
        var settingsChangeOutboxPublisherOptions = SettingsChangeOutboxPublisherOptions.FromConfiguration(configuration);
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
        services.AddSingleton(blockchainGatewayResilienceOptions);
        services.AddSingleton(blockchainGatewayResiliencePolicyOptions);
        services.AddSingleton(brokeredTradingOptions);
        services.AddSingleton(settingsChangeOutboxPublisherOptions);
        services.AddSingleton(brokeredTradingPolicy);
        services.AddSingleton<IBrokeredTradingPolicyProvider, RuntimeBrokeredTradingPolicyProvider>();
        services.AddSingleton<IKrakenGatewayOptionsProvider, RuntimeKrakenGatewayOptionsProvider>();
        services.AddSingleton<IBlockchainGatewayResiliencePolicyOptionsProvider, RuntimeBlockchainGatewayResiliencePolicyOptionsProvider>();
        services.AddSingleton<ISubmitCryptoTransferCommandValidator, SubmitCryptoTransferCommandValidator>();
        services.AddSingleton<ICryptoSettingsCommandValidator, CryptoSettingsCommandValidator>();
        services.AddSingleton<ICryptoGatewaySettingsCommandValidator, CryptoGatewaySettingsCommandValidator>();
        services.AddSingleton<ICryptoGatewayResilienceSettingsCommandValidator, CryptoGatewayResilienceSettingsCommandValidator>();
        services.AddSingleton<ICryptoTransferIdempotencyStore>(serviceProvider =>
            new EfCoreCryptoTransferIdempotencyStore(serviceProvider.GetRequiredService<IDbContextFactory<CryptoTransactionsDbContext>>()));
        services.AddSingleton<ICryptoTransferSubmissionSignal, CryptoTransferSubmissionSignal>();
        services.AddSingleton<ICryptoTransferService, CryptoTransferService>();
        services.AddSingleton<ICryptoSettingsService, EfCoreCryptoSettingsService>();
        services.AddSingleton<ICryptoGatewaySettingsService, EfCoreCryptoGatewaySettingsService>();
        services.AddSingleton<ICryptoGatewayResilienceSettingsService, EfCoreCryptoGatewayResilienceSettingsService>();
        services.AddSingleton<ISettingsChangeOutboxPublisher, SettingsChangeOutboxPublisher>();
        services.AddSingleton<ICryptoTransferTimeoutReconciler, CryptoTransferTimeoutReconciler>();
        services.AddSingleton<IBrokeredCryptoBuyService, BrokeredCryptoBuyService>();
        services.AddSingleton<IExternalHedgeExecutionReadinessGate, EfCoreExternalHedgeExecutionReadinessGate>();
        services.AddSingleton<IBrokeredCryptoBuyQuoteStore, InMemoryBrokeredCryptoBuyQuoteStore>();
        services.AddSingleton<ICryptoOwnershipLedger, InMemoryCryptoOwnershipLedger>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IBackgroundWorkerHeartbeatStore, EfCoreBackgroundWorkerHeartbeatStore>();
        services.AddSingleton<IExternalHedgeBatchQueue, EfCoreExternalHedgeBatchQueue>();
        services.AddSingleton<IInternalReferencePriceFeed, StaticReferencePriceFeed>();
        services.AddSingleton<ILiveMarketPriceFeed, UnconfiguredLiveMarketPriceFeed>();
        services.AddSingleton<IExternalLiquidityHedgingGateway, UnconfiguredExternalLiquidityHedgingGateway>();
        services.AddSingleton<ICryptoTransferFundsReservationGateway, UnconfiguredCryptoTransferFundsReservationGateway>();
        services.AddSingleton<RuntimeKrakenBlockchainTransferGateway>();
        services.AddSingleton<RuntimeResilientBlockchainTransferGateway>();
        services.AddSingleton<IBlockchainTransferGateway>(serviceProvider =>
            serviceProvider.GetRequiredService<RuntimeResilientBlockchainTransferGateway>());
        services.AddSingleton<CryptoTransferSubmissionProcessor>();
        if (includeBackgroundWorkers)
        {
            services.AddHostedService<CryptoTransferSubmissionWorker>();
            services.AddHostedService<CryptoTransferTimeoutReconciliationWorker>();
            services.AddHostedService<ExternalHedgeBatchExecutionWorker>();
            services.AddHostedService<SettingsChangeOutboxPublisherWorker>();
        }

        if (includeBootstrapWorker)
        {
            services.AddHostedService<RuntimeSettingsBootstrapWorker>();
        }
        return services;
    }
}
