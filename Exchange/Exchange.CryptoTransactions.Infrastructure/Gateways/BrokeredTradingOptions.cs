using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed record BrokeredTradingOptions
{
    public const int DefaultQuoteTtlSeconds = 15;
    public const decimal DefaultInternalOnlySpreadBasisPoints = 35m;
    public const decimal DefaultExternalHedgeSpreadBasisPoints = 90m;
    public const decimal DefaultMaxAllowedSlippageBasisPoints = 200m;
    public const decimal DefaultBitcoinReferencePriceNok = 1_000_000m;
    public const decimal DefaultEtherReferencePriceNok = 50_000m;
    public const decimal DefaultInitialBitcoinInventory = 2m;
    public const decimal DefaultInitialEtherInventory = 25m;
    public const int DefaultMaxBufferedHedgeCustomerBuys = 10;
    public const int DefaultMaxBufferedHedgeDelaySeconds = 30;

    public int QuoteTtlSeconds { get; init; } = DefaultQuoteTtlSeconds;
    public decimal InternalOnlySpreadBasisPoints { get; init; } = DefaultInternalOnlySpreadBasisPoints;
    public decimal ExternalHedgeSpreadBasisPoints { get; init; } = DefaultExternalHedgeSpreadBasisPoints;
    public decimal MaxAllowedSlippageBasisPoints { get; init; } = DefaultMaxAllowedSlippageBasisPoints;
    public decimal BitcoinReferencePriceNok { get; init; } = DefaultBitcoinReferencePriceNok;
    public decimal EtherReferencePriceNok { get; init; } = DefaultEtherReferencePriceNok;
    public decimal InitialBitcoinInventory { get; init; } = DefaultInitialBitcoinInventory;
    public decimal InitialEtherInventory { get; init; } = DefaultInitialEtherInventory;
    public int MaxBufferedHedgeCustomerBuys { get; init; } = DefaultMaxBufferedHedgeCustomerBuys;
    public int MaxBufferedHedgeDelaySeconds { get; init; } = DefaultMaxBufferedHedgeDelaySeconds;

    public static BrokeredTradingOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(InfrastructureConfigurationKeys.BrokeredTradingSection);
        var options = new BrokeredTradingOptions
        {
            QuoteTtlSeconds = section.GetValue<int?>(nameof(QuoteTtlSeconds)) ?? DefaultQuoteTtlSeconds,
            InternalOnlySpreadBasisPoints = section.GetValue<decimal?>(nameof(InternalOnlySpreadBasisPoints)) ?? DefaultInternalOnlySpreadBasisPoints,
            ExternalHedgeSpreadBasisPoints = section.GetValue<decimal?>(nameof(ExternalHedgeSpreadBasisPoints)) ?? DefaultExternalHedgeSpreadBasisPoints,
            MaxAllowedSlippageBasisPoints = section.GetValue<decimal?>(nameof(MaxAllowedSlippageBasisPoints)) ?? DefaultMaxAllowedSlippageBasisPoints,
            BitcoinReferencePriceNok = section.GetValue<decimal?>(nameof(BitcoinReferencePriceNok)) ?? DefaultBitcoinReferencePriceNok,
            EtherReferencePriceNok = section.GetValue<decimal?>(nameof(EtherReferencePriceNok)) ?? DefaultEtherReferencePriceNok,
            InitialBitcoinInventory = section.GetValue<decimal?>(nameof(InitialBitcoinInventory)) ?? DefaultInitialBitcoinInventory,
            InitialEtherInventory = section.GetValue<decimal?>(nameof(InitialEtherInventory)) ?? DefaultInitialEtherInventory,
            MaxBufferedHedgeCustomerBuys = section.GetValue<int?>(nameof(MaxBufferedHedgeCustomerBuys)) ?? DefaultMaxBufferedHedgeCustomerBuys,
            MaxBufferedHedgeDelaySeconds = section.GetValue<int?>(nameof(MaxBufferedHedgeDelaySeconds)) ?? DefaultMaxBufferedHedgeDelaySeconds
        };

        Validate(options);
        return options;
    }

    public decimal GetReferencePrice(AssetSymbol assetSymbol, QuoteCurrency quoteCurrency)
    {
        if (quoteCurrency != QuoteCurrency.NorwegianKrone)
        {
            throw new ArgumentOutOfRangeException(nameof(quoteCurrency), quoteCurrency.Value, "Only NOK quote currency is supported.");
        }

        return assetSymbol.Value switch
        {
            "BTC" => BitcoinReferencePriceNok,
            "ETH" => EtherReferencePriceNok,
            _ => throw new ArgumentOutOfRangeException(nameof(assetSymbol), assetSymbol.Value, "Unsupported asset symbol.")
        };
    }

    public decimal GetInitialInventory(AssetSymbol assetSymbol)
    {
        return assetSymbol.Value switch
        {
            "BTC" => InitialBitcoinInventory,
            "ETH" => InitialEtherInventory,
            _ => throw new ArgumentOutOfRangeException(nameof(assetSymbol), assetSymbol.Value, "Unsupported asset symbol.")
        };
    }

    private static void Validate(BrokeredTradingOptions options)
    {
        if (options.QuoteTtlSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.QuoteTtlSeconds), options.QuoteTtlSeconds, "QuoteTtlSeconds must be greater than zero.");
        }

        if (options.InternalOnlySpreadBasisPoints < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.InternalOnlySpreadBasisPoints), options.InternalOnlySpreadBasisPoints, "InternalOnlySpreadBasisPoints cannot be negative.");
        }

        if (options.ExternalHedgeSpreadBasisPoints < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ExternalHedgeSpreadBasisPoints), options.ExternalHedgeSpreadBasisPoints, "ExternalHedgeSpreadBasisPoints cannot be negative.");
        }

        if (options.MaxAllowedSlippageBasisPoints < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxAllowedSlippageBasisPoints), options.MaxAllowedSlippageBasisPoints, "MaxAllowedSlippageBasisPoints cannot be negative.");
        }

        if (options.BitcoinReferencePriceNok <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.BitcoinReferencePriceNok), options.BitcoinReferencePriceNok, "BitcoinReferencePriceNok must be greater than zero.");
        }

        if (options.EtherReferencePriceNok <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.EtherReferencePriceNok), options.EtherReferencePriceNok, "EtherReferencePriceNok must be greater than zero.");
        }

        if (options.InitialBitcoinInventory < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.InitialBitcoinInventory), options.InitialBitcoinInventory, "InitialBitcoinInventory cannot be negative.");
        }

        if (options.InitialEtherInventory < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(options.InitialEtherInventory), options.InitialEtherInventory, "InitialEtherInventory cannot be negative.");
        }

        if (options.MaxBufferedHedgeCustomerBuys <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxBufferedHedgeCustomerBuys), options.MaxBufferedHedgeCustomerBuys, "MaxBufferedHedgeCustomerBuys must be greater than zero.");
        }

        if (options.MaxBufferedHedgeDelaySeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxBufferedHedgeDelaySeconds), options.MaxBufferedHedgeDelaySeconds, "MaxBufferedHedgeDelaySeconds must be greater than zero.");
        }
    }
}
