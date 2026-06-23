using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace Exchange.CryptoTransactions.Infrastructure.Simulation.Gateways;

public sealed class SimulatedCryptoTransferFundsReservationGateway(
    SimulatedFundsReservationOptions options) : ICryptoTransferFundsReservationGateway
{
    private readonly object gate = new();
    private readonly ConcurrentDictionary<(string AccountId, AssetSymbol AssetSymbol), decimal> availableBalances = new();
    private readonly ConcurrentDictionary<(string AccountId, AssetSymbol AssetSymbol, string IdempotencyKey), decimal> activeReservations = new();

    public Task ReserveAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        decimal totalDebit,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (totalDebit <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalDebit), totalDebit, "Total debit must be greater than zero.");
        }

        var normalizedSourceAccountId = sourceAccountId.Trim();
        var normalizedIdempotencyKey = idempotencyKey.Trim();
        var reservationKey = (normalizedSourceAccountId, assetSymbol, normalizedIdempotencyKey);
        var balanceKey = (normalizedSourceAccountId, assetSymbol);

        lock (gate)
        {
            if (activeReservations.TryGetValue(reservationKey, out var existingReservation))
            {
                if (existingReservation != totalDebit)
                {
                    throw new IdempotencyKeyConflictException(
                        $"Idempotency key '{normalizedIdempotencyKey}' was already used with a different funds reservation amount.");
                }

                return Task.CompletedTask;
            }

            var availableBalance = availableBalances.GetOrAdd(balanceKey, _ => options.ResolveDefaultBalance(assetSymbol));
            if (availableBalance < totalDebit)
            {
                throw new InsufficientFundsException(
                    $"Insufficient {assetSymbol.Value} balance for account '{normalizedSourceAccountId}'. Required debit: {totalDebit}, available: {availableBalance}.");
            }

            availableBalances[balanceKey] = availableBalance - totalDebit;
            activeReservations[reservationKey] = totalDebit;
        }

        return Task.CompletedTask;
    }

    public Task CommitAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSourceAccountId = sourceAccountId.Trim();
        var normalizedIdempotencyKey = idempotencyKey.Trim();
        var reservationKey = (normalizedSourceAccountId, assetSymbol, normalizedIdempotencyKey);
        _ = activeReservations.TryRemove(reservationKey, out _);
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSourceAccountId = sourceAccountId.Trim();
        var normalizedIdempotencyKey = idempotencyKey.Trim();
        var reservationKey = (normalizedSourceAccountId, assetSymbol, normalizedIdempotencyKey);
        var balanceKey = (normalizedSourceAccountId, assetSymbol);

        lock (gate)
        {
            if (!activeReservations.TryRemove(reservationKey, out var reservedAmount))
            {
                return Task.CompletedTask;
            }

            var availableBalance = availableBalances.GetOrAdd(balanceKey, _ => options.ResolveDefaultBalance(assetSymbol));
            availableBalances[balanceKey] = availableBalance + reservedAmount;
        }

        return Task.CompletedTask;
    }
}
