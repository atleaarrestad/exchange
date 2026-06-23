using System.Collections.Frozen;

namespace Exchange.CryptoTransactions.Domain.ValueObjects;

public readonly record struct AssetSymbol
{
    private static readonly FrozenSet<string> SupportedValues =
        new[] { "BTC", "ETH" }.ToFrozenSet(StringComparer.Ordinal);

    public static AssetSymbol Bitcoin { get; } = new("BTC");
    public static AssetSymbol Ether { get; } = new("ETH");

    public string Value { get; }

    private AssetSymbol(string value)
    {
        Value = value;
    }

    public static AssetSymbol Parse(string value, string? paramName = null)
    {
        if (!TryParse(value, out var assetSymbol))
        {
            throw new ArgumentException($"Asset symbol '{value}' is not supported.", paramName ?? nameof(value));
        }

        return assetSymbol;
    }

    public static bool TryParse(string? value, out AssetSymbol assetSymbol)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            assetSymbol = default;
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (!SupportedValues.Contains(normalized))
        {
            assetSymbol = default;
            return false;
        }

        assetSymbol = new AssetSymbol(normalized);
        return true;
    }

    public override string ToString() => Value;
}
