using Exchange.CryptoTransactions.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed record SimulatedFundsReservationOptions
{
    public const decimal DefaultBitcoinBalance = 2m;
    public const decimal DefaultEtherBalance = 20m;

    public decimal DefaultBitcoinAvailableBalance { get; init; } = DefaultBitcoinBalance;
    public decimal DefaultEtherAvailableBalance { get; init; } = DefaultEtherBalance;

    public static SimulatedFundsReservationOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new SimulatedFundsReservationOptions
        {
            DefaultBitcoinAvailableBalance = configuration.GetValue<decimal?>(nameof(DefaultBitcoinAvailableBalance)) ?? DefaultBitcoinBalance,
            DefaultEtherAvailableBalance = configuration.GetValue<decimal?>(nameof(DefaultEtherAvailableBalance)) ?? DefaultEtherBalance
        };

        Validate(options);
        return options;
    }

    public decimal ResolveDefaultBalance(AssetSymbol assetSymbol)
    {
        return assetSymbol == AssetSymbol.Bitcoin
            ? DefaultBitcoinAvailableBalance
            : DefaultEtherAvailableBalance;
    }

    private static void Validate(SimulatedFundsReservationOptions options)
    {
        if (options.DefaultBitcoinAvailableBalance < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.DefaultBitcoinAvailableBalance),
                options.DefaultBitcoinAvailableBalance,
                "DefaultBitcoinAvailableBalance cannot be negative.");
        }

        if (options.DefaultEtherAvailableBalance < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.DefaultEtherAvailableBalance),
                options.DefaultEtherAvailableBalance,
                "DefaultEtherAvailableBalance cannot be negative.");
        }
    }
}
