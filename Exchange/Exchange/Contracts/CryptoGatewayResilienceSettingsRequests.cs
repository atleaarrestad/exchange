namespace Exchange.Contracts;

public sealed record UpsertCryptoGatewayResilienceSettingsRequest(
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
    int MaxQueueingActions);
