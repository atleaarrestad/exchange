using Microsoft.Extensions.Configuration;

namespace Exchange.Infrastructure.Messaging;

public sealed record MessagingTransportOptions
{
    public const string InMemoryTransport = "in-memory";
    public const string RabbitMqTransport = "rabbitmq";

    public string Transport { get; init; } = RabbitMqTransport;
    public string InstanceId { get; init; } = SettingsFanoutEndpointNameFactory.ResolveInstanceId(null);
    public RabbitMqOptions RabbitMq { get; init; } = new();

    public bool UseRabbitMq => string.Equals(Transport, RabbitMqTransport, StringComparison.OrdinalIgnoreCase);

    public static MessagingTransportOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Messaging");
        return new MessagingTransportOptions
        {
            Transport = section.GetValue<string>(nameof(Transport)) ?? RabbitMqTransport,
            InstanceId = SettingsFanoutEndpointNameFactory.ResolveInstanceId(section.GetValue<string>(nameof(InstanceId))),
            RabbitMq = new RabbitMqOptions
            {
                Host = section.GetValue<string>($"{nameof(RabbitMq)}:{nameof(RabbitMqOptions.Host)}") ?? RabbitMqOptions.DefaultHost,
                VirtualHost = section.GetValue<string>($"{nameof(RabbitMq)}:{nameof(RabbitMqOptions.VirtualHost)}") ?? RabbitMqOptions.DefaultVirtualHost,
                Username = section.GetValue<string>($"{nameof(RabbitMq)}:{nameof(RabbitMqOptions.Username)}") ?? RabbitMqOptions.DefaultUsername,
                Password = section.GetValue<string>($"{nameof(RabbitMq)}:{nameof(RabbitMqOptions.Password)}") ?? RabbitMqOptions.DefaultPassword
            }
        };
    }
}

public sealed record RabbitMqOptions
{
    public const string DefaultHost = "localhost";
    public const string DefaultVirtualHost = "/";
    public const string DefaultUsername = "guest";
    public const string DefaultPassword = "guest";

    public string Host { get; init; } = DefaultHost;
    public string VirtualHost { get; init; } = DefaultVirtualHost;
    public string Username { get; init; } = DefaultUsername;
    public string Password { get; init; } = DefaultPassword;
}
