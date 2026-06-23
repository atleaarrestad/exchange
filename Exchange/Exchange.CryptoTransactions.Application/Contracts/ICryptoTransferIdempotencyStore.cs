namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ICryptoTransferIdempotencyStore
{
    Task<CryptoTransferReceipt> ExecuteOnceAsync(
        string sourceAccountId,
        string assetSymbol,
        string idempotencyKey,
        Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
        CancellationToken cancellationToken = default);
}
