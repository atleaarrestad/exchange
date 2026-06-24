namespace Exchange.Infrastructure.Messaging;

public static class SettingsFanoutEndpointNameFactory
{
    private const string DefaultInstanceId = "instance";
    private const string SubscriberSuffix = "subscriber";

    public static string BuildSubscriberEndpointName(string messageTopic, string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageTopic);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return $"{messageTopic}-{SubscriberSuffix}-{instanceId}";
    }

    public static string ResolveInstanceId(string? configuredInstanceId)
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
