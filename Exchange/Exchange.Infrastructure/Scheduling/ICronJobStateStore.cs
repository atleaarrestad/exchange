namespace Exchange.Infrastructure.Scheduling;

public interface ICronJobStateStore
{
    Task EnsureStateAsync(string jobName, string cronExpression, bool isEnabled, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<CronJobLeaseClaim?> TryClaimAsync(string jobName, string workerId, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken);
    Task<Guid> StartExecutionAsync(string jobName, string workerId, DateTimeOffset scheduledAtUtc, DateTimeOffset startedAtUtc, CancellationToken cancellationToken);
    Task CompleteExecutionAsync(
        Guid runRecordId,
        string jobName,
        Guid leaseToken,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc,
        DateTimeOffset nextRunAtUtc,
        CronJobRunStatus status,
        string resultMessage,
        string? error,
        CancellationToken cancellationToken);
}
