using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Simulation.DependencyInjection;
using Exchange.FiatTransactions.Infrastructure.DependencyInjection;
using Exchange.Infrastructure.Messaging;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Polly.Bulkhead;
using Polly.CircuitBreaker;
using Polly.Timeout;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCryptoTransactionsInfrastructure(builder.Configuration);
builder.Services.AddFiatTransactionsInfrastructure(builder.Configuration);
var runMigrationsOnStartup = builder.Configuration.GetValue<bool>(Exchange.CryptoTransactions.Infrastructure.DependencyInjection.InfrastructureConfigurationKeys.RunMigrationsOnStartup);
var runFiatMigrationsOnStartup = builder.Configuration.GetValue<bool>(Exchange.FiatTransactions.Infrastructure.DependencyInjection.InfrastructureConfigurationKeys.RunMigrationsOnStartup);

var isSimulationEnabled = builder.Configuration
    .GetSection("Simulation")
    .GetValue<bool>("Enabled");
if (isSimulationEnabled)
{
    builder.Services.AddCryptoTransactionsSimulation(builder.Configuration);
}

var messagingOptions = MessagingTransportOptions.FromConfiguration(builder.Configuration);
const ushort cryptoTransferSubmissionPrefetchCount = 96;
const int cryptoTransferSubmissionConcurrentMessageLimit = 32;

builder.Services.AddMassTransit(configurator =>
{
    configurator.SetKebabCaseEndpointNameFormatter();
    configurator.AddSettingsChangeConsumers();
    configurator.AddConsumer<CryptoTransferSubmissionRequestedConsumer>();

    if (messagingOptions.UseRabbitMq)
    {
        configurator.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(
                messagingOptions.RabbitMq.Host,
                messagingOptions.RabbitMq.VirtualHost,
                host =>
                {
                    host.Username(messagingOptions.RabbitMq.Username);
                    host.Password(messagingOptions.RabbitMq.Password);
                });
            cfg.ReceiveEndpoint(
                MessagingEndpointNames.CryptoTransferSubmission,
                endpoint =>
                {
                    endpoint.PrefetchCount = cryptoTransferSubmissionPrefetchCount;
                    endpoint.ConcurrentMessageLimit = cryptoTransferSubmissionConcurrentMessageLimit;
                    endpoint.UseMessageRetry(retry =>
                    {
                        retry.Ignore<BrokenCircuitException>();
                        retry.Ignore<BulkheadRejectedException>();
                        retry.Ignore<TimeoutRejectedException>();
                        retry.Interval(3, TimeSpan.FromSeconds(2));
                    });
                    endpoint.ConfigureConsumer<CryptoTransferSubmissionRequestedConsumer>(context);
                });
            SettingsFanoutEndpointRegistration.ConfigureFanoutEndpoints(
                (endpointName, configureEndpoint) => cfg.ReceiveEndpoint(endpointName, configureEndpoint),
                CryptoSettingsChangeMassTransitRegistration.BuildFanoutSubscriptions(context),
                messagingOptions.InstanceId);
        });
        return;
    }

    configurator.UsingInMemory((context, cfg) =>
    {
        cfg.ReceiveEndpoint(
            MessagingEndpointNames.CryptoTransferSubmission,
            endpoint =>
            {
                endpoint.ConcurrentMessageLimit = cryptoTransferSubmissionConcurrentMessageLimit;
                endpoint.UseMessageRetry(retry =>
                {
                    retry.Ignore<BrokenCircuitException>();
                    retry.Ignore<BulkheadRejectedException>();
                    retry.Ignore<TimeoutRejectedException>();
                    retry.Interval(3, TimeSpan.FromSeconds(2));
                });
                endpoint.ConfigureConsumer<CryptoTransferSubmissionRequestedConsumer>(context);
            });
        SettingsFanoutEndpointRegistration.ConfigureFanoutEndpoints(
            (endpointName, configureEndpoint) => cfg.ReceiveEndpoint(endpointName, configureEndpoint),
            CryptoSettingsChangeMassTransitRegistration.BuildFanoutSubscriptions(context),
            messagingOptions.InstanceId);
    });
});

var appHost = builder.Build();
if (runMigrationsOnStartup)
{
    await appHost.Services.MigrateCryptoTransactionsDatabaseAsync();
}
if (runFiatMigrationsOnStartup)
{
    await appHost.Services.MigrateFiatTransactionsDatabaseAsync();
}
appHost.Run();
