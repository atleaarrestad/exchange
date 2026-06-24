using MassTransit;
using Exchange.Configuration;
using Exchange.BrokeredBuys.Messaging;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Simulation.DependencyInjection;
using Exchange.FiatTransactions.Infrastructure.DependencyInjection;
using Exchange.Infrastructure.Caching;
using Exchange.Infrastructure.Messaging;
using Exchange.Middleware;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructureCaching();
builder.Services.AddCryptoTransactionsInfrastructure(
    builder.Configuration,
    includeBackgroundWorkers: false,
    includeBootstrapWorker: false);
builder.Services.AddFiatTransactionsInfrastructure(builder.Configuration);
var messagingOptions = MessagingTransportOptions.FromConfiguration(builder.Configuration);
var cryptoTransactionsConnectionString = builder.Configuration.GetValue<string>(Exchange.CryptoTransactions.Infrastructure.DependencyInjection.InfrastructureConfigurationKeys.IdempotencyConnectionString)
    ?? Exchange.CryptoTransactions.Infrastructure.DependencyInjection.InfrastructureConfigurationKeys.DefaultIdempotencyConnectionString;
builder.Services.AddDbContext<CryptoTransactionsDbContext>(options =>
{
    options.UseNpgsql(cryptoTransactionsConnectionString);
});

var isSimulationEnabled = builder.Configuration.GetSection(ConfigurationKeys.SimulationSection)
    .GetValue<bool>(ConfigurationKeys.Enabled);

if (isSimulationEnabled)
{
    builder.Services.AddCryptoTransactionsSimulation(builder.Configuration);
}

builder.Services.AddMassTransit(configurator =>
{
    configurator.SetKebabCaseEndpointNameFormatter();
    configurator.AddSettingsChangeConsumers();
    configurator.AddSagaStateMachine<BrokeredFiatCryptoBuyStateMachine, BrokeredFiatCryptoBuySagaState>()
        .EntityFrameworkRepository(repositoryConfigurator =>
        {
            repositoryConfigurator.ConcurrencyMode = ConcurrencyMode.Pessimistic;
            repositoryConfigurator.ExistingDbContext<CryptoTransactionsDbContext>();
            repositoryConfigurator.UsePostgres();
        });
    configurator.AddConsumer<ReserveFiatForBrokeredBuyConsumer>();
    configurator.AddConsumer<BookCryptoForBrokeredBuyConsumer>();
    configurator.AddConsumer<CaptureFiatForBrokeredBuyConsumer>();
    configurator.AddConsumer<ReleaseFiatReservationForBrokeredBuyConsumer>();

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
                "brokered-fiat-crypto-buy-saga",
                endpoint =>
                {
                    endpoint.StateMachineSaga(
                        context.GetRequiredService<BrokeredFiatCryptoBuyStateMachine>(),
                        context.GetRequiredService<ISagaRepository<BrokeredFiatCryptoBuySagaState>>());
                });
            cfg.ReceiveEndpoint("reserve-fiat-for-brokered-buy", endpoint => endpoint.ConfigureConsumer<ReserveFiatForBrokeredBuyConsumer>(context));
            cfg.ReceiveEndpoint("book-crypto-for-brokered-buy", endpoint => endpoint.ConfigureConsumer<BookCryptoForBrokeredBuyConsumer>(context));
            cfg.ReceiveEndpoint("capture-fiat-for-brokered-buy", endpoint => endpoint.ConfigureConsumer<CaptureFiatForBrokeredBuyConsumer>(context));
            cfg.ReceiveEndpoint("release-fiat-reservation-for-brokered-buy", endpoint => endpoint.ConfigureConsumer<ReleaseFiatReservationForBrokeredBuyConsumer>(context));
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
            "brokered-fiat-crypto-buy-saga",
            endpoint =>
            {
                endpoint.StateMachineSaga(
                    context.GetRequiredService<BrokeredFiatCryptoBuyStateMachine>(),
                    context.GetRequiredService<ISagaRepository<BrokeredFiatCryptoBuySagaState>>());
            });
        cfg.ReceiveEndpoint("reserve-fiat-for-brokered-buy", endpoint => endpoint.ConfigureConsumer<ReserveFiatForBrokeredBuyConsumer>(context));
        cfg.ReceiveEndpoint("book-crypto-for-brokered-buy", endpoint => endpoint.ConfigureConsumer<BookCryptoForBrokeredBuyConsumer>(context));
        cfg.ReceiveEndpoint("capture-fiat-for-brokered-buy", endpoint => endpoint.ConfigureConsumer<CaptureFiatForBrokeredBuyConsumer>(context));
        cfg.ReceiveEndpoint("release-fiat-reservation-for-brokered-buy", endpoint => endpoint.ConfigureConsumer<ReleaseFiatReservationForBrokeredBuyConsumer>(context));
        SettingsFanoutEndpointRegistration.ConfigureFanoutEndpoints(
            (endpointName, configureEndpoint) => cfg.ReceiveEndpoint(endpointName, configureEndpoint),
            CryptoSettingsChangeMassTransitRegistration.BuildFanoutSubscriptions(context),
            messagingOptions.InstanceId);
    });
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

var runMigrationsOnStartup = builder.Configuration.GetValue<bool>(Exchange.CryptoTransactions.Infrastructure.DependencyInjection.InfrastructureConfigurationKeys.RunMigrationsOnStartup);
if (runMigrationsOnStartup)
{
    await app.Services.MigrateCryptoTransactionsDatabaseAsync();
}

var runFiatMigrationsOnStartup = builder.Configuration.GetValue<bool>(Exchange.FiatTransactions.Infrastructure.DependencyInjection.InfrastructureConfigurationKeys.RunMigrationsOnStartup);
if (runFiatMigrationsOnStartup)
{
    await app.Services.MigrateFiatTransactionsDatabaseAsync();
}

app.UseMiddleware<ApiExceptionMappingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
