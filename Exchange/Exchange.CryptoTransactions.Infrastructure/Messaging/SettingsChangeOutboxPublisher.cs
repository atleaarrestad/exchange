using Exchange.CryptoTransactions.Infrastructure.Persistence;
using MassTransit;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed class SettingsChangeOutboxPublisher(IBus bus) : ISettingsChangeOutboxPublisher
{
    public async Task PublishAsync(SettingsChangeOutboxEntryEntity entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        object message = SettingsChangeOutboxMessageRegistry.DeserializeMessage(entry.MessageType, entry.PayloadJson);

        await bus.Publish(message, cancellationToken);
    }
}
