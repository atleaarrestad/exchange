namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoGatewayResilienceSettingsProfileEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int OperationTimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public int RetryDelayMilliseconds { get; set; }
    public double FailureRatio { get; set; }
    public int MinimumThroughput { get; set; }
    public int SamplingDurationSeconds { get; set; }
    public int BreakDurationSeconds { get; set; }
    public int MaxParallelization { get; set; }
    public int MaxQueueingActions { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
