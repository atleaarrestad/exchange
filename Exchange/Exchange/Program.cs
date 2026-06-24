using MassTransit;
using Exchange.Configuration;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Simulation.DependencyInjection;
using Exchange.Infrastructure.Caching;
using Exchange.Infrastructure.Messaging;
using Exchange.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructureCaching();
builder.Services.AddCryptoTransactionsInfrastructure(
    builder.Configuration,
    includeBackgroundWorkers: false,
    includeBootstrapWorker: false);
var messagingOptions = MessagingTransportOptions.FromConfiguration(builder.Configuration);

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
            SettingsFanoutEndpointRegistration.ConfigureFanoutEndpoints(
                (endpointName, configureEndpoint) => cfg.ReceiveEndpoint(endpointName, configureEndpoint),
                CryptoSettingsChangeMassTransitRegistration.BuildFanoutSubscriptions(context),
                messagingOptions.InstanceId);
        });
        return;
    }

    configurator.UsingInMemory((context, cfg) =>
    {
        SettingsFanoutEndpointRegistration.ConfigureFanoutEndpoints(
            (endpointName, configureEndpoint) => cfg.ReceiveEndpoint(endpointName, configureEndpoint),
            CryptoSettingsChangeMassTransitRegistration.BuildFanoutSubscriptions(context),
            messagingOptions.InstanceId);
    });
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

var runMigrationsOnStartup = builder.Configuration.GetValue<bool>(InfrastructureConfigurationKeys.RunMigrationsOnStartup);
if (runMigrationsOnStartup)
{
    await app.Services.MigrateCryptoTransactionsDatabaseAsync();
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
