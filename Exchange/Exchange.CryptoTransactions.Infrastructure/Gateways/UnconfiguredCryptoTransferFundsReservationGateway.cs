using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class UnconfiguredCryptoTransferFundsReservationGateway : ICryptoTransferFundsReservationGateway
{
    public Task ReserveAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        decimal totalDebit,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ExternalDependencyNotConfiguredException(
            "No funds reservation gateway is configured. Enable simulation or provide a real balance/ledger reservation implementation.");
    }

    public Task CommitAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ExternalDependencyNotConfiguredException(
            "No funds reservation gateway is configured. Enable simulation or provide a real balance/ledger reservation implementation.");
    }

    public Task ReleaseAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new ExternalDependencyNotConfiguredException(
            "No funds reservation gateway is configured. Enable simulation or provide a real balance/ledger reservation implementation.");
    }
}
