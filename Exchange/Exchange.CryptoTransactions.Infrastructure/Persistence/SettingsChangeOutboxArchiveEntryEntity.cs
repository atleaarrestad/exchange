namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class SettingsChangeOutboxArchiveEntryEntity
{
    public Guid Id { get; set; }
    public string MessageType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public int PublishAttemptCount { get; set; }
    public DateTimeOffset ArchivedAtUtc { get; set; }
}
