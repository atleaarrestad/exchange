namespace Exchange.Infrastructure.Scheduling;

public sealed record CronJobLeaseClaim(Guid LeaseToken, DateTimeOffset ScheduledAtUtc);
