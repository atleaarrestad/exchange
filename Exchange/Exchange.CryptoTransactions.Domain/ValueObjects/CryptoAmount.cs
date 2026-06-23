namespace Exchange.CryptoTransactions.Domain.ValueObjects;

public sealed record CryptoAmount
{
    private const int MaxScale = 18;

    public string AssetSymbol { get; }
    public decimal Value { get; }

    public CryptoAmount(string assetSymbol, decimal value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol);
        ValidateScale(value, nameof(value));

        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Transfer amount must be greater than zero.");
        }

        AssetSymbol = assetSymbol.Trim().ToUpperInvariant();
        Value = value;
    }

    private static void ValidateScale(decimal value, string paramName)
    {
        var scale = (decimal.GetBits(value)[3] >> 16) & 0x7F;
        if (scale > MaxScale)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Amount scale cannot exceed {MaxScale} decimal places.");
        }
    }
}
