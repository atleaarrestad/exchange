namespace Exchange.CryptoTransactions.Application;

public interface ICryptoGatewayResilienceSettingsService
{
    Task<IReadOnlyList<CryptoGatewayResilienceSettingsProfile>> GetAllAsync(CancellationToken cancellationToken);

    Task<CryptoGatewayResilienceSettingsProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CryptoGatewayResilienceSettingsProfile> CreateAsync(
        CreateCryptoGatewayResilienceSettingsProfileCommand command,
        CancellationToken cancellationToken);

    Task<CryptoGatewayResilienceSettingsProfile?> UpdateAsync(
        Guid id,
        UpdateCryptoGatewayResilienceSettingsProfileCommand command,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record CreateCryptoGatewayResilienceSettingsProfileCommand(
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

public sealed record UpdateCryptoGatewayResilienceSettingsProfileCommand(
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
