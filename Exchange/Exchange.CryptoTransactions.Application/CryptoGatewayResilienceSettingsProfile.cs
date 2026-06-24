namespace Exchange.CryptoTransactions.Application;

public sealed record CryptoGatewayResilienceSettingsProfile(
    Guid Id,
    string Name,
    bool Enabled,
    int OperationTimeoutSeconds,
    int RetryCount,
    int RetryDelayMilliseconds,
    double FailureRatio,
    int MinimumThroughput,
    int SamplingDurationSeconds,
    int BreakDurationSeconds,
    int MaxParallelization,
    int MaxQueueingActions,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
