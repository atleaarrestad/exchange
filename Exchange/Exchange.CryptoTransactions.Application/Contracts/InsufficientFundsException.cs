namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message)
        : base(message)
    {
    }
}
