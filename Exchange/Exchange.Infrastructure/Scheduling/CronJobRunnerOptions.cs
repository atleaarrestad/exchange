using Cronos;
using Microsoft.Extensions.Configuration;

namespace Exchange.Infrastructure.Scheduling;

public sealed record CronJobRunnerOptions
{
    public const int DefaultPollIntervalSeconds = 30;
    public const int DefaultLeaseDurationSeconds = 300;

    public int PollIntervalSeconds { get; init; } = DefaultPollIntervalSeconds;
    public int LeaseDurationSeconds { get; init; } = DefaultLeaseDurationSeconds;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseDurationSeconds);

    public static CronJobRunnerOptions FromConfiguration(IConfiguration configuration, string sectionPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);
        return FromConfiguration(configuration.GetSection(sectionPath));
    }

    public static CronJobRunnerOptions FromConfiguration(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var options = new CronJobRunnerOptions
        {
            PollIntervalSeconds = section.GetValue<int?>(nameof(PollIntervalSeconds)) ?? DefaultPollIntervalSeconds,
            LeaseDurationSeconds = section.GetValue<int?>(nameof(LeaseDurationSeconds)) ?? DefaultLeaseDurationSeconds
        };

        if (options.PollIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.PollIntervalSeconds), options.PollIntervalSeconds, "PollIntervalSeconds must be greater than zero.");
        }

        if (options.LeaseDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.LeaseDurationSeconds), options.LeaseDurationSeconds, "LeaseDurationSeconds must be greater than zero.");
        }

        return options;
    }

    public static DateTimeOffset GetNextOccurrenceUtc(string cronExpression, DateTimeOffset fromUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);
        var parsed = CronExpression.Parse(cronExpression, CronFormat.Standard);
        var next = parsed.GetNextOccurrence(fromUtc.UtcDateTime, TimeZoneInfo.Utc);
        if (!next.HasValue)
        {
            throw new InvalidOperationException($"Cron expression '{cronExpression}' does not produce future occurrences.");
        }

        return new DateTimeOffset(next.Value, TimeSpan.Zero);
    }
}
