using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.Infrastructure.Persistence;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreExternalHedgeBatchQueue(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    IExternalLiquidityHedgingGateway externalLiquidityHedgingGateway,
    IExternalHedgeSettlementService externalHedgeSettlementService,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    TimeProvider timeProvider) : IExternalHedgeBatchQueue
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim executeGate = new(1, 1);
    private readonly string workerId = $"{Environment.MachineName.ToLowerInvariant()}-{Environment.ProcessId}-{Guid.CreateVersion7()}";

    public async Task RegisterAsync(BufferedExternalHedgeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), request.Quantity, "Quantity must be greater than zero.");
        }

        var normalizedCustomerAccountId = request.CustomerAccountId.Trim();
        if (normalizedCustomerAccountId.Length == 0)
        {
            throw new ArgumentException("CustomerAccountId is required.", nameof(request.CustomerAccountId));
        }

        var normalizedClientOrderId = request.ClientOrderId.Trim();
        if (normalizedClientOrderId.Length == 0)
        {
            throw new ArgumentException("ClientOrderId is required.", nameof(request.ClientOrderId));
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.ExternalHedgeBatchEntries.Add(new ExternalHedgeBatchEntryEntity
        {
            Id = Guid.CreateVersion7(),
            CustomerAccountId = normalizedCustomerAccountId,
            ClientOrderId = normalizedClientOrderId,
            AssetSymbol = request.AssetSymbol.Value,
            QuoteCurrency = request.QuoteCurrency.Value,
            Quantity = request.Quantity,
            RequestedAtUtc = request.RequestedAtUtc.ToUniversalTime(),
            ExecutedAtUtc = null,
            ExecutedExternalOrderId = null
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception))
        {
            // Idempotent duplicate registration: same customer/client-order pair already buffered or executed.
        }
    }

    public async Task ExecuteDueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await executeGate.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                var claimedBatch = await TryClaimDueBatchAsync(cancellationToken);
                if (claimedBatch is null)
                {
                    return;
                }

                HedgePurchaseResult result;
                try
                {
                    result = await externalLiquidityHedgingGateway.BuyAsync(
                        new HedgePurchaseRequest(
                            $"hedge-batch-{claimedBatch.BatchId}",
                            claimedBatch.AssetSymbol,
                            claimedBatch.QuoteCurrency,
                            claimedBatch.Quantity),
                        cancellationToken);
                }
                catch
                {
                    await ReleaseBatchLeaseAsync(claimedBatch.LeaseToken, cancellationToken);
                    throw;
                }

                await externalHedgeSettlementService.RegisterExecutionAsync(
                    new ExternalHedgeExecutionObservation(
                        result.ExternalOrderId,
                        claimedBatch.AssetSymbol,
                        claimedBatch.QuoteCurrency,
                        result.ExecutedQuantity,
                        result.ExecutedUnitPrice,
                        result.ExecutedAtUtc),
                    cancellationToken);
                await MarkBatchExecutedAsync(claimedBatch.LeaseToken, result, cancellationToken);
                await externalHedgeSettlementService.SettleAsync(result.ExternalOrderId, cancellationToken);
                if (result.ExecutedQuantity != claimedBatch.Quantity)
                {
                    throw new ExternalHedgeExecutionQuantityMismatchException(
                        $"External hedge execution '{result.ExternalOrderId}' settled quantity {result.ExecutedQuantity} for a claimed batch quantity of {claimedBatch.Quantity}. Manual investigation is required.");
                }
            }
        }
        finally
        {
            executeGate.Release();
        }
    }

    private async Task<ClaimedBatch?> TryClaimDueBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var policy = tradingPolicyProvider.GetCurrent();

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidateGroupRows = await context.ExternalHedgeBatchEntries
            .AsNoTracking()
            .Where(entry => entry.ExecutedAtUtc == null
                && (entry.LeaseExpiresAtUtc == null || entry.LeaseExpiresAtUtc < now))
            .GroupBy(entry => new { entry.AssetSymbol, entry.QuoteCurrency })
            .Select(group => new
            {
                group.Key.AssetSymbol,
                group.Key.QuoteCurrency,
                PendingCount = group.Count(),
                OldestRequestedAtUtc = group.Min(entry => entry.RequestedAtUtc)
            })
            .OrderBy(group => group.OldestRequestedAtUtc)
            .ToArrayAsync(cancellationToken);
        var candidateGroups = candidateGroupRows
            .Select(group => new CandidateBatchGroup(
                group.AssetSymbol,
                group.QuoteCurrency,
                group.PendingCount,
                group.OldestRequestedAtUtc))
            .ToArray();

        var dueGroup = candidateGroups.FirstOrDefault(group =>
            group.PendingCount >= policy.MaxBufferedHedgeCustomerBuys
            || group.OldestRequestedAtUtc + policy.MaxBufferedHedgeDelay <= now);
        if (dueGroup is null)
        {
            return null;
        }

        var leaseToken = Guid.CreateVersion7();
        var leaseExpiresAtUtc = now.Add(LeaseDuration);

        await context.ExternalHedgeBatchEntries
            .Where(entry => entry.ExecutedAtUtc == null
                && entry.AssetSymbol == dueGroup.AssetSymbol
                && entry.QuoteCurrency == dueGroup.QuoteCurrency
                && (entry.LeaseExpiresAtUtc == null || entry.LeaseExpiresAtUtc < now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.LeaseOwnerId, workerId)
                    .SetProperty(entry => entry.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(entry => entry.LeaseToken, leaseToken),
                cancellationToken);

        var leasedRows = await context.ExternalHedgeBatchEntries
            .AsNoTracking()
            .Where(entry => entry.LeaseToken == leaseToken && entry.ExecutedAtUtc == null)
            .ToArrayAsync(cancellationToken);
        if (leasedRows.Length == 0)
        {
            return null;
        }

        var totalQuantity = 0m;
        foreach (var entry in leasedRows)
        {
            totalQuantity = checked(totalQuantity + entry.Quantity);
        }

        return new ClaimedBatch(
            Guid.CreateVersion7(),
            AssetSymbol.Parse(dueGroup.AssetSymbol),
            QuoteCurrency.Parse(dueGroup.QuoteCurrency),
            totalQuantity,
            leaseToken);
    }

    private async Task MarkBatchExecutedAsync(Guid leaseToken, HedgePurchaseResult result, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.ExternalHedgeBatchEntries
            .Where(entry => entry.LeaseToken == leaseToken && entry.ExecutedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.ExecutedAtUtc, result.ExecutedAtUtc)
                    .SetProperty(entry => entry.ExecutedExternalOrderId, result.ExternalOrderId)
                    .SetProperty(entry => entry.LeaseOwnerId, (string?)null)
                    .SetProperty(entry => entry.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(entry => entry.LeaseToken, (Guid?)null),
                cancellationToken);
    }

    private async Task ReleaseBatchLeaseAsync(Guid leaseToken, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await context.ExternalHedgeBatchEntries
            .Where(entry => entry.LeaseToken == leaseToken && entry.ExecutedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.LeaseOwnerId, (string?)null)
                    .SetProperty(entry => entry.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(entry => entry.LeaseToken, (Guid?)null),
                cancellationToken);
    }

    private sealed record CandidateBatchGroup(
        string AssetSymbol,
        string QuoteCurrency,
        int PendingCount,
        DateTimeOffset OldestRequestedAtUtc);

    private sealed record ClaimedBatch(
        Guid BatchId,
        AssetSymbol AssetSymbol,
        QuoteCurrency QuoteCurrency,
        decimal Quantity,
        Guid LeaseToken);
}
