namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class BackgroundWorkerHeartbeatEntity
{
    public required string WorkerName { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
}
