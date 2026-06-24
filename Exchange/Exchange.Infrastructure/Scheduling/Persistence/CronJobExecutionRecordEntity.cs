namespace Exchange.Infrastructure.Scheduling.Persistence;

public sealed class CronJobExecutionRecordEntity
{
    public Guid Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string RunnerId { get; set; } = string.Empty;
    public DateTimeOffset ScheduledAtUtc { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public CronJobRunStatus Status { get; set; }
    public string? ResultMessage { get; set; }
    public string? Error { get; set; }
}
