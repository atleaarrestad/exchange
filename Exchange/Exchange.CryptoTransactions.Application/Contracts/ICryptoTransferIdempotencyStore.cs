using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ICryptoTransferIdempotencyStore
{
    Task<CryptoTransferReceipt> ExecuteOnceAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
        CancellationToken cancellationToken = default);
}
