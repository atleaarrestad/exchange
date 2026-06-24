namespace Exchange.Infrastructure.Scheduling;

public enum CronJobRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
    TimedOut = 3
}
