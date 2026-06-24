using System.Collections.Frozen;

namespace Exchange.FiatTransactions.Domain.ValueObjects;

public readonly record struct FiatCurrency
{
    private static readonly FrozenSet<string> SupportedValues =
        new[] { "NOK" }.ToFrozenSet(StringComparer.Ordinal);

    public static FiatCurrency NorwegianKrone { get; } = new("NOK");

    public string Value { get; }

    private FiatCurrency(string value)
    {
        Value = value;
    }

    public static FiatCurrency Parse(string value, string? paramName = null)
    {
        if (!TryParse(value, out var fiatCurrency))
        {
            throw new ArgumentException($"Fiat currency '{value}' is not supported.", paramName ?? nameof(value));
        }

        return fiatCurrency;
    }

    public static bool TryParse(string? value, out FiatCurrency fiatCurrency)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fiatCurrency = default;
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (!SupportedValues.Contains(normalized))
        {
            fiatCurrency = default;
            return false;
        }

        fiatCurrency = new FiatCurrency(normalized);
        return true;
    }

    public override string ToString() => Value;
}
