using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed record TimeoutReconciliationOptions
{
    public int ScanIntervalSeconds { get; init; } = InfrastructureConfigurationKeys.DefaultTimeoutReconciliationScanIntervalSeconds;
    public int StaleAfterSeconds { get; init; } = InfrastructureConfigurationKeys.DefaultTimeoutReconciliationStaleAfterSeconds;

    public TimeSpan ScanInterval => TimeSpan.FromSeconds(ScanIntervalSeconds);
    public TimeSpan StaleAfter => TimeSpan.FromSeconds(StaleAfterSeconds);

    public static TimeoutReconciliationOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new TimeoutReconciliationOptions
        {
            ScanIntervalSeconds = configuration.GetValue<int?>(InfrastructureConfigurationKeys.TimeoutReconciliationScanIntervalSeconds)
                ?? InfrastructureConfigurationKeys.DefaultTimeoutReconciliationScanIntervalSeconds,
            StaleAfterSeconds = configuration.GetValue<int?>(InfrastructureConfigurationKeys.TimeoutReconciliationStaleAfterSeconds)
                ?? InfrastructureConfigurationKeys.DefaultTimeoutReconciliationStaleAfterSeconds
        };

        if (options.ScanIntervalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ScanIntervalSeconds), options.ScanIntervalSeconds, "ScanIntervalSeconds must be greater than zero.");
        }

        if (options.StaleAfterSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.StaleAfterSeconds), options.StaleAfterSeconds, "StaleAfterSeconds must be greater than zero.");
        }

        return options;
    }
}
