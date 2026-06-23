namespace Exchange.CryptoTransactions.Application;

public interface ICryptoTransferTimeoutReconciler
{
    Task ReconcileAsync(DateTimeOffset staleBeforeUtc, CancellationToken cancellationToken = default);
}
