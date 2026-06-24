namespace Exchange.Configuration;

public sealed record MessagingTransportOptions
{
    public const string InMemoryTransport = "in-memory";
    public const string RabbitMqTransport = "rabbitmq";
    private const string DefaultInstanceId = "instance";

    public string Transport { get; init; } = RabbitMqTransport;
    public string InstanceId { get; init; } = DefaultInstanceId;
    public RabbitMqOptions RabbitMq { get; init; } = new();

    public bool UseRabbitMq => string.Equals(Transport, RabbitMqTransport, StringComparison.OrdinalIgnoreCase);

    public static MessagingTransportOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Messaging");
        return new MessagingTransportOptions
        {
            Transport = section.GetValue<string>(nameof(Transport)) ?? RabbitMqTransport,
            InstanceId = ResolveInstanceId(section.GetValue<string>(nameof(InstanceId))),
            RabbitMq = new RabbitMqOptions
            {
                Host = section.GetValue<string>($"{nameof(RabbitMq)}:{nameof(RabbitMqOptions.Host)}") ?? RabbitMqOptions.DefaultHost,
                VirtualHost = section.GetValue<string>($"{nameof(RabbitMq)}:{nameof(RabbitMqOptions.VirtualHost)}") ?? RabbitMqOptions.DefaultVirtualHost,
                Username = section.GetValue<string>($"{nameof(RabbitMq)}:{nameof(RabbitMqOptions.Username)}") ?? RabbitMqOptions.DefaultUsername,
                Password = section.GetValue<string>($"{nameof(RabbitMq)}:{nameof(RabbitMqOptions.Password)}") ?? RabbitMqOptions.DefaultPassword
            }
        };
    }

    private static string ResolveInstanceId(string? configuredInstanceId)
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

        return $"{DefaultInstanceId}-{Environment.ProcessId}";
    }

    private static string? NormalizeInstanceId(string? value)
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
