using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public sealed record SettingsChangeOutboxPublisherOptions
{
    public const int DefaultPollIntervalSeconds = 2;
    public const int DefaultLeaseDurationSeconds = 30;
    public const int DefaultClaimBatchSize = 100;
    public const int DefaultMaxPublishAttempts = 10;

    public int PollIntervalSeconds { get; init; } = DefaultPollIntervalSeconds;
    public int LeaseDurationSeconds { get; init; } = DefaultLeaseDurationSeconds;
    public int ClaimBatchSize { get; init; } = DefaultClaimBatchSize;
    public int MaxPublishAttempts { get; init; } = DefaultMaxPublishAttempts;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);
    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseDurationSeconds);

    public static SettingsChangeOutboxPublisherOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(InfrastructureConfigurationKeys.SettingsChangeOutboxSection);
        var options = new SettingsChangeOutboxPublisherOptions
        {
            PollIntervalSeconds = section.GetValue<int?>(nameof(PollIntervalSeconds)) ?? DefaultPollIntervalSeconds,
            LeaseDurationSeconds = section.GetValue<int?>(nameof(LeaseDurationSeconds)) ?? DefaultLeaseDurationSeconds,
            ClaimBatchSize = section.GetValue<int?>(nameof(ClaimBatchSize)) ?? DefaultClaimBatchSize,
            MaxPublishAttempts = section.GetValue<int?>(nameof(MaxPublishAttempts)) ?? DefaultMaxPublishAttempts
        };

        if (options.PollIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.PollIntervalSeconds), options.PollIntervalSeconds, "PollIntervalSeconds must be greater than zero.");
        }

        if (options.LeaseDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.LeaseDurationSeconds), options.LeaseDurationSeconds, "LeaseDurationSeconds must be greater than zero.");
        }

        if (options.ClaimBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ClaimBatchSize), options.ClaimBatchSize, "ClaimBatchSize must be greater than zero.");
        }

        if (options.MaxPublishAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxPublishAttempts), options.MaxPublishAttempts, "MaxPublishAttempts must be greater than zero.");
        }

        return options;
    }
}
