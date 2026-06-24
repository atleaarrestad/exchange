using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Exchange.Infrastructure.Scheduling;
using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed record SettingsChangeOutboxArchivalOptions
{
    public const bool DefaultEnabled = true;
    public const string DefaultCronExpression = "0 2 * * *";
    public const int DefaultTimeoutSeconds = 300;
    public const int DefaultMatureAfterSeconds = 86400;
    public const int DefaultBatchSize = 500;

    public bool Enabled { get; init; } = DefaultEnabled;
    public string CronExpression { get; init; } = DefaultCronExpression;
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;
    public int MatureAfterSeconds { get; init; } = DefaultMatureAfterSeconds;
    public int BatchSize { get; init; } = DefaultBatchSize;

    public TimeSpan Timeout => TimeSpan.FromSeconds(TimeoutSeconds);
    public TimeSpan MatureAfter => TimeSpan.FromSeconds(MatureAfterSeconds);

    public static SettingsChangeOutboxArchivalOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(InfrastructureConfigurationKeys.SettingsChangeOutboxArchivalJobSection);
        var options = new SettingsChangeOutboxArchivalOptions
        {
            Enabled = section.GetValue<bool?>(nameof(Enabled)) ?? DefaultEnabled,
            CronExpression = section.GetValue<string>(nameof(CronExpression))?.Trim() ?? DefaultCronExpression,
            TimeoutSeconds = section.GetValue<int?>(nameof(TimeoutSeconds)) ?? DefaultTimeoutSeconds,
            MatureAfterSeconds = section.GetValue<int?>(nameof(MatureAfterSeconds)) ?? DefaultMatureAfterSeconds,
            BatchSize = section.GetValue<int?>(nameof(BatchSize)) ?? DefaultBatchSize
        };

        if (string.IsNullOrWhiteSpace(options.CronExpression))
        {
            throw new ArgumentOutOfRangeException(nameof(options.CronExpression), options.CronExpression, "CronExpression must be provided.");
        }

        _ = CronJobRunnerOptions.GetNextOccurrenceUtc(options.CronExpression, DateTimeOffset.UtcNow);

        if (options.TimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TimeoutSeconds), options.TimeoutSeconds, "TimeoutSeconds must be greater than zero.");
        }

        if (options.MatureAfterSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MatureAfterSeconds), options.MatureAfterSeconds, "MatureAfterSeconds must be greater than zero.");
        }

        if (options.BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.BatchSize), options.BatchSize, "BatchSize must be greater than zero.");
        }

        return options;
    }
}
