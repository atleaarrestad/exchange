using Exchange.CryptoTransactions.Infrastructure.Simulation.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed record SimulatedMarketPricingOptions
{
    public const int DefaultMinLatencyMs = 5;
    public const int DefaultMaxLatencyMs = 50;
    public const decimal DefaultMaxDeviationBasisPoints = 25m;
    public const decimal DefaultBitcoinReferencePriceNok = 1_000_000m;
    public const decimal DefaultEtherReferencePriceNok = 50_000m;

    public int MinLatencyMs { get; init; } = DefaultMinLatencyMs;
    public int MaxLatencyMs { get; init; } = DefaultMaxLatencyMs;
    public decimal MaxDeviationBasisPoints { get; init; } = DefaultMaxDeviationBasisPoints;
    public decimal BitcoinReferencePriceNok { get; init; } = DefaultBitcoinReferencePriceNok;
    public decimal EtherReferencePriceNok { get; init; } = DefaultEtherReferencePriceNok;

    public static SimulatedMarketPricingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SimulationConfigurationKeys.BrokeredTradingSimulationSection);
        var options = new SimulatedMarketPricingOptions
        {
            MinLatencyMs = section.GetValue<int?>(nameof(MinLatencyMs)) ?? DefaultMinLatencyMs,
            MaxLatencyMs = section.GetValue<int?>(nameof(MaxLatencyMs)) ?? DefaultMaxLatencyMs,
            MaxDeviationBasisPoints = section.GetValue<decimal?>(nameof(MaxDeviationBasisPoints)) ?? DefaultMaxDeviationBasisPoints,
            BitcoinReferencePriceNok = section.GetValue<decimal?>(nameof(BitcoinReferencePriceNok)) ?? DefaultBitcoinReferencePriceNok,
            EtherReferencePriceNok = section.GetValue<decimal?>(nameof(EtherReferencePriceNok)) ?? DefaultEtherReferencePriceNok
        };

        Validate(options);
        return options;
    }

    private static void Validate(SimulatedMarketPricingOptions options)
    {
        if (options.MinLatencyMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MinLatencyMs), options.MinLatencyMs, "MinLatencyMs cannot be negative.");
        }

        if (options.MaxLatencyMs < options.MinLatencyMs)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxLatencyMs), options.MaxLatencyMs, "MaxLatencyMs must be greater than or equal to MinLatencyMs.");
        }

        if (options.MaxDeviationBasisPoints < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxDeviationBasisPoints), options.MaxDeviationBasisPoints, "MaxDeviationBasisPoints cannot be negative.");
        }

        if (options.BitcoinReferencePriceNok <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.BitcoinReferencePriceNok), options.BitcoinReferencePriceNok, "BitcoinReferencePriceNok must be greater than zero.");
        }

        if (options.EtherReferencePriceNok <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.EtherReferencePriceNok), options.EtherReferencePriceNok, "EtherReferencePriceNok must be greater than zero.");
        }
    }
}
