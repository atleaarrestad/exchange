namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class PriceProtectionExceededException : Exception
{
    public PriceProtectionExceededException(string message) : base(message)
    {
    }
}
