namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class IdempotencyOperationPendingException : Exception
{
    public IdempotencyOperationPendingException(string message)
        : base(message)
    {
    }
}
