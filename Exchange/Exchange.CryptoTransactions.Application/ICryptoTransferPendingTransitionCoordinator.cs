using Exchange.CryptoTransactions.Application.Contracts;

namespace Exchange.CryptoTransactions.Application;

public interface ICryptoTransferPendingTransitionCoordinator
{
    Task CommitAndMarkCompletedAsync(
        PendingCryptoTransferOperation operation,
        CryptoTransferReceipt receipt,
        CancellationToken cancellationToken = default);

    Task ReleaseAndDropPendingAsync(
        PendingCryptoTransferOperation operation,
        string failureContext,
        CancellationToken cancellationToken = default);
}
