using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed record KrakenBlockchainTransferGatewayOptions
{
    public const string DefaultBaseUrl = "https://api.kraken.com";
    public const int DefaultHttpTimeoutSeconds = 15;
    public const int DefaultBitcoinRequiredConfirmations = 3;
    public const int DefaultEtherRequiredConfirmations = 12;

    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public int HttpTimeoutSeconds { get; init; } = DefaultHttpTimeoutSeconds;
    public string? ApiKey { get; init; }
    public string? ApiSecret { get; init; }
    public string? BitcoinWithdrawalKey { get; init; }
    public string? EtherWithdrawalKey { get; init; }
    public int BitcoinRequiredConfirmations { get; init; } = DefaultBitcoinRequiredConfirmations;
    public int EtherRequiredConfirmations { get; init; } = DefaultEtherRequiredConfirmations;

    public static KrakenBlockchainTransferGatewayOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(InfrastructureConfigurationKeys.KrakenGatewaySection);
        var options = new KrakenBlockchainTransferGatewayOptions
        {
            Enabled = section.GetValue<bool?>(nameof(Enabled)) ?? false,
            BaseUrl = section.GetValue<string>(nameof(BaseUrl)) ?? DefaultBaseUrl,
            HttpTimeoutSeconds = section.GetValue<int?>(nameof(HttpTimeoutSeconds)) ?? DefaultHttpTimeoutSeconds,
            ApiKey = section.GetValue<string>(nameof(ApiKey)),
            ApiSecret = section.GetValue<string>(nameof(ApiSecret)),
            BitcoinWithdrawalKey = section.GetValue<string>(nameof(BitcoinWithdrawalKey)),
            EtherWithdrawalKey = section.GetValue<string>(nameof(EtherWithdrawalKey)),
            BitcoinRequiredConfirmations = section.GetValue<int?>(nameof(BitcoinRequiredConfirmations)) ?? DefaultBitcoinRequiredConfirmations,
            EtherRequiredConfirmations = section.GetValue<int?>(nameof(EtherRequiredConfirmations)) ?? DefaultEtherRequiredConfirmations
        };

        ValidateForRuntime(options);
        return options;
    }

    public string GetWithdrawalKey(AssetSymbol assetSymbol)
    {
        return assetSymbol.Value switch
        {
            "BTC" => BitcoinWithdrawalKey!,
            "ETH" => EtherWithdrawalKey!,
            _ => throw new ArgumentOutOfRangeException(nameof(assetSymbol), assetSymbol.Value, "Unsupported asset symbol for Kraken withdrawals.")
        };
    }

    public int GetRequiredConfirmations(AssetSymbol assetSymbol)
    {
        return assetSymbol.Value switch
        {
            "BTC" => BitcoinRequiredConfirmations,
            "ETH" => EtherRequiredConfirmations,
            _ => throw new ArgumentOutOfRangeException(nameof(assetSymbol), assetSymbol.Value, "Unsupported asset symbol for Kraken withdrawals.")
        };
    }

    public static void ValidateForRuntime(KrakenBlockchainTransferGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new ArgumentOutOfRangeException(nameof(options.BaseUrl), options.BaseUrl, "BaseUrl is required.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentOutOfRangeException(nameof(options.BaseUrl), options.BaseUrl, "BaseUrl must be an absolute URI.");
        }

        if (options.HttpTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.HttpTimeoutSeconds), options.HttpTimeoutSeconds, "HttpTimeoutSeconds must be greater than zero.");
        }

        if (options.BitcoinRequiredConfirmations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.BitcoinRequiredConfirmations), options.BitcoinRequiredConfirmations, "BitcoinRequiredConfirmations cannot be negative.");
        }

        if (options.EtherRequiredConfirmations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.EtherRequiredConfirmations), options.EtherRequiredConfirmations, "EtherRequiredConfirmations cannot be negative.");
        }

        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentOutOfRangeException(nameof(options.ApiKey), options.ApiKey, "ApiKey is required when Kraken gateway is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            throw new ArgumentOutOfRangeException(nameof(options.ApiSecret), options.ApiSecret, "ApiSecret is required when Kraken gateway is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.BitcoinWithdrawalKey))
        {
            throw new ArgumentOutOfRangeException(nameof(options.BitcoinWithdrawalKey), options.BitcoinWithdrawalKey, "BitcoinWithdrawalKey is required when Kraken gateway is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.EtherWithdrawalKey))
        {
            throw new ArgumentOutOfRangeException(nameof(options.EtherWithdrawalKey), options.EtherWithdrawalKey, "EtherWithdrawalKey is required when Kraken gateway is enabled.");
        }

        try
        {
            _ = Convert.FromBase64String(options.ApiSecret);
        }
        catch (FormatException)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ApiSecret), options.ApiSecret, "ApiSecret must be a base64-encoded string.");
        }
    }
}
