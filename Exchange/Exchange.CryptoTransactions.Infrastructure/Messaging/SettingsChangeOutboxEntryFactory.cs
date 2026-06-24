using System.Text.Json;
using Exchange.CryptoTransactions.Infrastructure.Persistence;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public static class SettingsChangeOutboxEntryFactory
{
    public static SettingsChangeOutboxEntryEntity Create(string messageType, object payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(payload);

        return new SettingsChangeOutboxEntryEntity
        {
            Id = Guid.CreateVersion7(),
            MessageType = messageType,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PublishedAtUtc = null,
            PublishAttemptCount = 0
        };
    }
}
