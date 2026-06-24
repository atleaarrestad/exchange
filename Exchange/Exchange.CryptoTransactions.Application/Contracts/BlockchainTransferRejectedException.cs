namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class BlockchainTransferRejectedException : Exception
{
    public bool IsTransient { get; }

    public BlockchainTransferRejectedException(string message)
        : base(message)
    {
    }

    public BlockchainTransferRejectedException(string message, bool isTransient)
        : base(message)
    {
        IsTransient = isTransient;
    }

    public BlockchainTransferRejectedException(string message, bool isTransient, Exception innerException)
        : base(message, innerException)
    {
        IsTransient = isTransient;
    }
}
