namespace Exchange.CryptoTransactions.Application.Contracts;

public sealed class UnknownBlockchainTransferStatusException : Exception
{
    public UnknownBlockchainTransferStatusException(string message)
        : base(message)
    {
    }
}
