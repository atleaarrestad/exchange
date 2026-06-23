namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class IdempotencyKeyConflictException : Exception
{
    public IdempotencyKeyConflictException(string message)
        : base(message)
    {
    }
}
