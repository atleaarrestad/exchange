using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Application.Contracts;

public interface ICryptoTransferFundsReservationGateway
{
    Task ReserveAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        decimal totalDebit,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task CommitAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
