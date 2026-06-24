using System.Text.Json;
using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using MassTransit;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class SettingsChangeOutboxPublisher(IBus bus) : ISettingsChangeOutboxPublisher
{
    public async Task PublishAsync(SettingsChangeOutboxEntryEntity entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        object message = entry.MessageType switch
        {
            SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged =>
                Deserialize<CryptoSettingsProfileChangedIntegrationEvent>(entry.PayloadJson),
            SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged =>
                Deserialize<CryptoGatewaySettingsProfileChangedIntegrationEvent>(entry.PayloadJson),
            SettingsChangeOutboxMessageTypes.CryptoGatewayResilienceSettingsProfileChanged =>
                Deserialize<CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent>(entry.PayloadJson),
            _ => throw new InvalidOperationException($"Unknown outbox message type '{entry.MessageType}'.")
        };

        await bus.Publish(message, cancellationToken);
    }

    private static T Deserialize<T>(string payloadJson)
    {
        var value = JsonSerializer.Deserialize<T>(payloadJson);
        return value ?? throw new InvalidOperationException($"Unable to deserialize payload to {typeof(T).Name}.");
    }
}
