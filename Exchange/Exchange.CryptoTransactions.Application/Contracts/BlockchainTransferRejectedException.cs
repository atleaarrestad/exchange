namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class BlockchainTransferRejectedException : Exception
{
    public BlockchainTransferRejectedException(string message)
        : base(message)
    {
    }
}
