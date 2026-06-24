namespace Exchange.Infrastructure.Scheduling.Persistence;

public sealed class CronJobStateEntity
{
    public string JobName { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTimeOffset NextRunAtUtc { get; set; }
    public DateTimeOffset? LastStartedAtUtc { get; set; }
    public DateTimeOffset? LastCompletedAtUtc { get; set; }
    public CronJobRunStatus? LastRunStatus { get; set; }
    public string? LastError { get; set; }
    public string? LeaseOwnerId { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
}
