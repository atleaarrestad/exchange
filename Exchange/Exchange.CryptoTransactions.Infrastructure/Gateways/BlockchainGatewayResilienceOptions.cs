using Microsoft.Extensions.Configuration;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Exchange.CryptoTransactions.Resilience.Gateways;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed record BlockchainGatewayResilienceOptions
{
    public const bool DefaultEnabled = true;
    public const int DefaultOperationTimeoutSeconds = 20;
    public const double DefaultFailureRatio = 0.5;
    public const int DefaultMinimumThroughput = 20;
    public const int DefaultSamplingDurationSeconds = 30;
    public const int DefaultBreakDurationSeconds = 30;
    public const int DefaultMaxParallelization = 32;
    public const int DefaultMaxQueueingActions = 64;

    public bool Enabled { get; init; } = DefaultEnabled;
    public int OperationTimeoutSeconds { get; init; } = DefaultOperationTimeoutSeconds;
    public double FailureRatio { get; init; } = DefaultFailureRatio;
    public int MinimumThroughput { get; init; } = DefaultMinimumThroughput;
    public int SamplingDurationSeconds { get; init; } = DefaultSamplingDurationSeconds;
    public int BreakDurationSeconds { get; init; } = DefaultBreakDurationSeconds;
    public int MaxParallelization { get; init; } = DefaultMaxParallelization;
    public int MaxQueueingActions { get; init; } = DefaultMaxQueueingActions;

    public TimeSpan OperationTimeout => TimeSpan.FromSeconds(OperationTimeoutSeconds);
    public TimeSpan SamplingDuration => TimeSpan.FromSeconds(SamplingDurationSeconds);
    public TimeSpan BreakDuration => TimeSpan.FromSeconds(BreakDurationSeconds);

    public BlockchainGatewayResiliencePolicyOptions ToPolicyOptions()
    {
        Validate(this);
        var options = new BlockchainGatewayResiliencePolicyOptions
        {
            Enabled = Enabled,
            OperationTimeout = OperationTimeout,
            FailureRatio = FailureRatio,
            MinimumThroughput = MinimumThroughput,
            SamplingDuration = SamplingDuration,
            BreakDuration = BreakDuration,
            MaxParallelization = MaxParallelization,
            MaxQueueingActions = MaxQueueingActions
        };
        BlockchainGatewayResiliencePolicyOptions.Validate(options);
        return options;
    }

    public static BlockchainGatewayResilienceOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(InfrastructureConfigurationKeys.BlockchainGatewayResilienceSection);
        var options = new BlockchainGatewayResilienceOptions
        {
            Enabled = section.GetValue<bool?>(nameof(Enabled)) ?? DefaultEnabled,
            OperationTimeoutSeconds = section.GetValue<int?>(nameof(OperationTimeoutSeconds)) ?? DefaultOperationTimeoutSeconds,
            FailureRatio = section.GetValue<double?>(nameof(FailureRatio)) ?? DefaultFailureRatio,
            MinimumThroughput = section.GetValue<int?>(nameof(MinimumThroughput)) ?? DefaultMinimumThroughput,
            SamplingDurationSeconds = section.GetValue<int?>(nameof(SamplingDurationSeconds)) ?? DefaultSamplingDurationSeconds,
            BreakDurationSeconds = section.GetValue<int?>(nameof(BreakDurationSeconds)) ?? DefaultBreakDurationSeconds,
            MaxParallelization = section.GetValue<int?>(nameof(MaxParallelization)) ?? DefaultMaxParallelization,
            MaxQueueingActions = section.GetValue<int?>(nameof(MaxQueueingActions)) ?? DefaultMaxQueueingActions
        };

        Validate(options);
        return options;
    }

    private static void Validate(BlockchainGatewayResilienceOptions options)
    {
        if (options.OperationTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.OperationTimeoutSeconds), options.OperationTimeoutSeconds, "OperationTimeoutSeconds must be greater than zero.");
        }

        if (options.FailureRatio <= 0d || options.FailureRatio >= 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(options.FailureRatio), options.FailureRatio, "FailureRatio must be greater than 0 and less than 1.");
        }

        if (options.MinimumThroughput <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MinimumThroughput), options.MinimumThroughput, "MinimumThroughput must be greater than one.");
        }

        if (options.SamplingDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.SamplingDurationSeconds), options.SamplingDurationSeconds, "SamplingDurationSeconds must be greater than zero.");
        }

        if (options.BreakDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.BreakDurationSeconds), options.BreakDurationSeconds, "BreakDurationSeconds must be greater than zero.");
        }

        if (options.MaxParallelization <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxParallelization), options.MaxParallelization, "MaxParallelization must be greater than zero.");
        }

        if (options.MaxQueueingActions < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxQueueingActions), options.MaxQueueingActions, "MaxQueueingActions cannot be negative.");
        }
    }
}
