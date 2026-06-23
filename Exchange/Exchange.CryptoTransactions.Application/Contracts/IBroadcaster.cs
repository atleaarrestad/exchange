namespace Exchange.CryptoTransactions.Application.Contracts;

public interface IBroadcaster
{
    Task<BlockchainTransferResult> BroadcastAsync(SignedTransaction transaction, CancellationToken cancellationToken = default);
}
