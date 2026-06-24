namespace Exchange.CryptoTransactions.Application;

public interface ICronJobsAdminService
{
    Task<IReadOnlyList<CronJobAdminSummary>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CronJobExecutionRecordSummary>?> GetExecutionsAsync(string jobName, CancellationToken cancellationToken);
    Task<CronJobManualRunResult?> RunNowAsync(string jobName, CancellationToken cancellationToken);
}

public sealed record CronJobAdminSummary(
    string JobName,
    string DisplayName,
    string JobType,
    bool IsEnabled,
    string CronExpression,
    int TimeoutSeconds,
    DateTimeOffset NextRunAtUtc,
    DateTimeOffset? LastStartedAtUtc,
    DateTimeOffset? LastCompletedAtUtc,
    string? LastRunStatus,
    string? LastError);

public sealed record CronJobExecutionRecordSummary(
    Guid Id,
    DateTimeOffset ScheduledAtUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Status,
    string? ResultMessage,
    string? Error,
    string RunnerId);

public sealed record CronJobManualRunResult(
    string JobName,
    DateTimeOffset ScheduledAtUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Status,
    string ResultMessage,
    string? Error);

public sealed class CronJobRunRejectedException(string message) : Exception(message);
