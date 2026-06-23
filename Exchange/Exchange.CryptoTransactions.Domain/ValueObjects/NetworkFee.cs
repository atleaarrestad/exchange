namespace Exchange.CryptoTransactions.Domain.ValueObjects;

public sealed record NetworkFee
{
    private const int MaxScale = 18;

    public decimal Value { get; }

    public NetworkFee(decimal value)
    {
        ValidateScale(value, nameof(value));

        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Network fee cannot be negative.");
        }

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
