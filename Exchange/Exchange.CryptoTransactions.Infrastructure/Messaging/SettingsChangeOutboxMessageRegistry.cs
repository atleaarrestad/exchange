using System.Text.Json;
using Exchange.CryptoTransactions.Application.Messaging;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public static class SettingsChangeOutboxMessageRegistry
{
    private static readonly IReadOnlyDictionary<string, Func<string, object>> Deserializers =
        new Dictionary<string, Func<string, object>>(StringComparer.Ordinal)
        {
            [SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged] = payloadJson => Deserialize<CryptoSettingsProfileChangedIntegrationEvent>(payloadJson),
            [SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged] = payloadJson => Deserialize<CryptoGatewaySettingsProfileChangedIntegrationEvent>(payloadJson),
            [SettingsChangeOutboxMessageTypes.CryptoGatewayResilienceSettingsProfileChanged] = payloadJson => Deserialize<CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent>(payloadJson)
        };

    private static readonly IReadOnlyDictionary<Type, string> MessageTypesByPayloadType =
        new Dictionary<Type, string>
        {
            [typeof(CryptoSettingsProfileChangedIntegrationEvent)] = SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged,
            [typeof(CryptoGatewaySettingsProfileChangedIntegrationEvent)] = SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged,
            [typeof(CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent)] = SettingsChangeOutboxMessageTypes.CryptoGatewayResilienceSettingsProfileChanged
        };

    public static string ResolveMessageType<TPayload>()
    {
        if (MessageTypesByPayloadType.TryGetValue(typeof(TPayload), out var messageType))
        {
            return messageType;
        }

        throw new InvalidOperationException($"No settings outbox message type is registered for payload '{typeof(TPayload).Name}'.");
    }

    public static object DeserializeMessage(string messageType, string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(payloadJson);

        if (Deserializers.TryGetValue(messageType, out var deserialize))
        {
            return deserialize(payloadJson);
        }

        throw new InvalidOperationException($"Unknown outbox message type '{messageType}'.");
    }

    private static T Deserialize<T>(string payloadJson)
    {
        var value = JsonSerializer.Deserialize<T>(payloadJson);
        return value ?? throw new InvalidOperationException($"Unable to deserialize payload to {typeof(T).Name}.");
    }
}
