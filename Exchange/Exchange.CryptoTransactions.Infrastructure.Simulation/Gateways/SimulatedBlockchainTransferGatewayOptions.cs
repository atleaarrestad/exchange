using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed record SimulatedBlockchainTransferGatewayOptions
{
    public const int DefaultMinLatencyMs = 20;
    public const int DefaultMaxLatencyMs = 150;
    public const decimal DefaultRejectRate = 0.02m;
    public const decimal DefaultTimeoutRate = 0.01m;

    public int MinLatencyMs { get; init; } = DefaultMinLatencyMs;
    public int MaxLatencyMs { get; init; } = DefaultMaxLatencyMs;
    public decimal RejectRate { get; init; } = DefaultRejectRate;
    public decimal TimeoutRate { get; init; } = DefaultTimeoutRate;

    public static SimulatedBlockchainTransferGatewayOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new SimulatedBlockchainTransferGatewayOptions
        {
            MinLatencyMs = configuration.GetValue<int?>(nameof(MinLatencyMs)) ?? DefaultMinLatencyMs,
            MaxLatencyMs = configuration.GetValue<int?>(nameof(MaxLatencyMs)) ?? DefaultMaxLatencyMs,
            RejectRate = configuration.GetValue<decimal?>(nameof(RejectRate)) ?? DefaultRejectRate,
            TimeoutRate = configuration.GetValue<decimal?>(nameof(TimeoutRate)) ?? DefaultTimeoutRate
        };

        Validate(options);
        return options;
    }

    private static void Validate(SimulatedBlockchainTransferGatewayOptions options)
    {
        if (options.MinLatencyMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MinLatencyMs), options.MinLatencyMs, "MinLatencyMs cannot be negative.");
        }

        if (options.MaxLatencyMs < options.MinLatencyMs)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxLatencyMs), options.MaxLatencyMs, "MaxLatencyMs must be greater than or equal to MinLatencyMs.");
        }

        if (options.RejectRate is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.RejectRate), options.RejectRate, "RejectRate must be between 0 and 1.");
        }

        if (options.TimeoutRate is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TimeoutRate), options.TimeoutRate, "TimeoutRate must be between 0 and 1.");
        }

        if (options.RejectRate + options.TimeoutRate > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.TimeoutRate), options.TimeoutRate, "RejectRate + TimeoutRate cannot exceed 1.");
        }
    }
}
