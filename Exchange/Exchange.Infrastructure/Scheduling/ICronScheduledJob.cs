namespace Exchange.Infrastructure.Scheduling;

public interface ICronScheduledJob
{
    string JobName { get; }
    string DisplayName { get; }
    string JobType { get; }
    bool Enabled { get; }
    string CronExpression { get; }
    TimeSpan Timeout { get; }
    Task ExecuteAsync(CancellationToken cancellationToken);
}
