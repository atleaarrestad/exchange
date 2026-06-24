using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Simulation.DependencyInjection;
using MassTransit;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddCryptoTransactionsInfrastructure(builder.Configuration);

var isSimulationEnabled = builder.Configuration
    .GetSection("Simulation")
    .GetValue<bool>("Enabled");
if (isSimulationEnabled)
{
    builder.Services.AddCryptoTransactionsSimulation(builder.Configuration);
}

var useRabbitMq = string.Equals(
    builder.Configuration.GetValue<string>("Messaging:Transport"),
    "rabbitmq",
    StringComparison.OrdinalIgnoreCase);
var instanceId = ResolveInstanceId(builder.Configuration.GetValue<string>("Messaging:InstanceId"));

builder.Services.AddMassTransit(configurator =>
{
    configurator.SetKebabCaseEndpointNameFormatter();
    configurator.AddConsumer<CryptoSettingsProfileChangedConsumer>();
    configurator.AddConsumer<CryptoGatewaySettingsProfileChangedConsumer>();
    configurator.AddConsumer<CryptoTransferSubmissionRequestedConsumer>();

    if (useRabbitMq)
    {
        configurator.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(
                builder.Configuration.GetValue<string>("Messaging:RabbitMq:Host") ?? "localhost",
                builder.Configuration.GetValue<string>("Messaging:RabbitMq:VirtualHost") ?? "/",
                host =>
                {
                    host.Username(builder.Configuration.GetValue<string>("Messaging:RabbitMq:Username") ?? "guest");
                    host.Password(builder.Configuration.GetValue<string>("Messaging:RabbitMq:Password") ?? "guest");
                });
            cfg.ReceiveEndpoint(
                MessagingEndpointNames.CryptoTransferSubmission,
                endpoint =>
                {
                    endpoint.PrefetchCount = 32;
                    endpoint.ConcurrentMessageLimit = 16;
                    endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(2)));
                    endpoint.ConfigureConsumer<CryptoTransferSubmissionRequestedConsumer>(context);
                });
            cfg.ReceiveEndpoint(
                BuildFanoutEndpointName(SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged, instanceId),
                endpoint => endpoint.ConfigureConsumer<CryptoSettingsProfileChangedConsumer>(context));
            cfg.ReceiveEndpoint(
                BuildFanoutEndpointName(SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged, instanceId),
                endpoint => endpoint.ConfigureConsumer<CryptoGatewaySettingsProfileChangedConsumer>(context));
        });
        return;
    }

    configurator.UsingInMemory((context, cfg) =>
    {
        cfg.ReceiveEndpoint(
            MessagingEndpointNames.CryptoTransferSubmission,
            endpoint =>
            {
                endpoint.ConcurrentMessageLimit = 16;
                endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(2)));
                endpoint.ConfigureConsumer<CryptoTransferSubmissionRequestedConsumer>(context);
            });
        cfg.ReceiveEndpoint(
            BuildFanoutEndpointName(SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged, instanceId),
            endpoint => endpoint.ConfigureConsumer<CryptoSettingsProfileChangedConsumer>(context));
        cfg.ReceiveEndpoint(
            BuildFanoutEndpointName(SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged, instanceId),
            endpoint => endpoint.ConfigureConsumer<CryptoGatewaySettingsProfileChangedConsumer>(context));
    });
});

var appHost = builder.Build();
appHost.Run();

static string BuildFanoutEndpointName(string messageTopic, string instanceId)
{
    return $"{messageTopic}-subscriber-{instanceId}";
}

static string ResolveInstanceId(string? configuredInstanceId)
{
    var normalizedConfigured = NormalizeInstanceId(configuredInstanceId);
    if (normalizedConfigured is not null)
    {
        return normalizedConfigured;
    }

    var normalizedHost = NormalizeInstanceId(Environment.GetEnvironmentVariable("HOSTNAME"));
    if (normalizedHost is not null)
    {
        return normalizedHost;
    }

    var normalizedMachine = NormalizeInstanceId(Environment.MachineName);
    if (normalizedMachine is not null)
    {
        return $"{normalizedMachine}-{Environment.ProcessId}";
    }

    return $"instance-{Environment.ProcessId}";
}

static string? NormalizeInstanceId(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    Span<char> buffer = stackalloc char[value.Length];
    var index = 0;

    foreach (var character in value)
    {
        if (char.IsLetterOrDigit(character))
        {
            buffer[index++] = char.ToLowerInvariant(character);
            continue;
        }

        if (character is '-' or '_' or '.')
        {
            buffer[index++] = '-';
        }
    }

    if (index == 0)
    {
        return null;
    }

    var normalized = new string(buffer[..index]).Trim('-');
    return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
}
