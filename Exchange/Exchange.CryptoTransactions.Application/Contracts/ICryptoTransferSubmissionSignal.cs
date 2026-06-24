namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ICryptoTransferSubmissionSignal
{
    Task SignalPendingAsync(PendingCryptoTransferOperation operation, CancellationToken cancellationToken = default);
}
