using Exchange.CryptoTransactions.Infrastructure.Persistence;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public interface ISettingsChangeOutboxPublisher
{
    Task PublishAsync(SettingsChangeOutboxEntryEntity entry, CancellationToken cancellationToken = default);
}
