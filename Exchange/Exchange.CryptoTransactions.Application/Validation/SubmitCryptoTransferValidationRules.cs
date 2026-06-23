using System.Collections.Frozen;

namespace Exchange.CryptoTransactions.Application.Validation;

public static class SubmitCryptoTransferValidationRules
{
    public const int IdempotencyKeyMaxLength = 128;
    public const int SourceAccountIdMinLength = 3;
    public const int SourceAccountIdMaxLength = 64;
    public const int DestinationAddressMinLength = 16;
    public const int DestinationAddressMaxLength = 128;
    public const int AssetSymbolMinLength = 2;
    public const int AssetSymbolMaxLength = 10;
    public const decimal MaxAmount = 1_000_000m;
    public const decimal MaxNetworkFee = 10m;
    public const int MaxScale = 18;

    public static FrozenSet<string> SupportedAssets { get; } =
        new[] { "BTC", "ETH", "USDT" }.ToFrozenSet(StringComparer.Ordinal);
}
