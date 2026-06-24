using System.Collections.Frozen;

namespace Exchange.CryptoTransactions.Domain.ValueObjects;

public readonly record struct QuoteCurrency
{
    private static readonly FrozenSet<string> SupportedValues =
        new[] { "NOK" }.ToFrozenSet(StringComparer.Ordinal);

    public static QuoteCurrency NorwegianKrone { get; } = new("NOK");

    public string Value { get; }

    private QuoteCurrency(string value)
    {
        Value = value;
    }

    public static QuoteCurrency Parse(string value, string? paramName = null)
    {
        if (!TryParse(value, out var quoteCurrency))
        {
            throw new ArgumentException($"Quote currency '{value}' is not supported.", paramName ?? nameof(value));
        }

        return quoteCurrency;
    }

    public static bool TryParse(string? value, out QuoteCurrency quoteCurrency)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            quoteCurrency = default;
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (!SupportedValues.Contains(normalized))
        {
            quoteCurrency = default;
            return false;
        }

        quoteCurrency = new QuoteCurrency(normalized);
        return true;
    }

    public override string ToString() => Value;
}
