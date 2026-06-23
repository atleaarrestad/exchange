namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class BlockchainTransferTimeoutException : TimeoutException
{
    public BlockchainTransferTimeoutException(string message)
        : base(message)
    {
    }
}
