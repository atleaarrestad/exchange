using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreExternalHedgeSettlementService(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    TimeProvider timeProvider) : IExternalHedgeSettlementService
{
    public async Task RegisterExecutionAsync(ExternalHedgeExecutionObservation observation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateObservation(observation);

        var normalizedExternalOrderId = observation.ExternalOrderId.Trim();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.ExternalHedgeExecutionRecords
            .SingleOrDefaultAsync(candidate => candidate.ExternalOrderId == normalizedExternalOrderId, cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingExecutionRecord(existing, observation, normalizedExternalOrderId);
            return;
        }

        var now = timeProvider.GetUtcNow();
        context.ExternalHedgeExecutionRecords.Add(new ExternalHedgeExecutionRecordEntity
        {
            Id = Guid.CreateVersion7(),
            ExternalOrderId = normalizedExternalOrderId,
            AssetSymbol = observation.AssetSymbol.Value,
            QuoteCurrency = observation.QuoteCurrency.Value,
            ExecutedQuantity = observation.ExecutedQuantity,
            ExecutedUnitPrice = observation.ExecutedUnitPrice,
            ExecutedAtUtc = observation.ExecutedAtUtc,
            SettledAtUtc = null,
            SettlementLedgerTransactionId = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            var duplicate = await context.ExternalHedgeExecutionRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.ExternalOrderId == normalizedExternalOrderId, cancellationToken);
            if (duplicate is null)
            {
                throw;
            }

            EnsureMatchingExecutionRecord(duplicate, observation, normalizedExternalOrderId);
        }
    }

    public async Task SettleAsync(string externalOrderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalOrderId);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedExternalOrderId = externalOrderId.Trim();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var executionRecord = await context.ExternalHedgeExecutionRecords
            .SingleOrDefaultAsync(candidate => candidate.ExternalOrderId == normalizedExternalOrderId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"External hedge execution '{normalizedExternalOrderId}' was not found. Settlement requires a persisted execution record.");

        if (executionRecord.SettledAtUtc.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var now = timeProvider.GetUtcNow();
        var settlementTransactionId = Guid.CreateVersion7();
        context.CryptoLedgerTransactions.Add(new CryptoLedgerTransactionEntity
        {
            Id = settlementTransactionId,
            OperationType = CryptoLedgerOperationTypes.ExternalHedgeSettlement,
            OperationId = normalizedExternalOrderId,
            ExecutedAtUtc = executionRecord.ExecutedAtUtc,
            CreatedAtUtc = now
        });

        context.CryptoLedgerEntries.AddRange(
            new CryptoLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = settlementTransactionId,
                Sequence = 1,
                AssetSymbol = executionRecord.AssetSymbol,
                AccountKind = CryptoLedgerAccountKinds.PlatformExternalHedgePending,
                AccountId = null,
                Direction = CryptoLedgerEntryDirection.Debit,
                Quantity = executionRecord.ExecutedQuantity,
                CreatedAtUtc = now
            },
            new CryptoLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = settlementTransactionId,
                Sequence = 2,
                AssetSymbol = executionRecord.AssetSymbol,
                AccountKind = CryptoLedgerAccountKinds.PlatformInventory,
                AccountId = null,
                Direction = CryptoLedgerEntryDirection.Credit,
                Quantity = executionRecord.ExecutedQuantity,
                CreatedAtUtc = now
            });

        await UpsertPlatformInventoryAsync(
            context,
            executionRecord.AssetSymbol,
            executionRecord.ExecutedQuantity,
            now,
            cancellationToken);

        executionRecord.SettledAtUtc = now;
        executionRecord.SettlementLedgerTransactionId = settlementTransactionId;
        executionRecord.UpdatedAtUtc = now;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            var currentState = await context.ExternalHedgeExecutionRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.ExternalOrderId == normalizedExternalOrderId, cancellationToken);
            if (currentState?.SettledAtUtc is not null)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Concurrent settlement conflict detected for external hedge execution '{normalizedExternalOrderId}'.",
                exception);
        }
    }

    private static async Task UpsertPlatformInventoryAsync(
        CryptoTransactionsDbContext context,
        string assetSymbol,
        decimal quantity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updatedRows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
              UPDATE platform_inventory_positions
              SET available_quantity = available_quantity + {quantity},
                  updated_at_utc = {now}
              WHERE asset_symbol = {assetSymbol};
              """,
            cancellationToken);
        if (updatedRows > 0)
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
              INSERT INTO platform_inventory_positions (asset_symbol, available_quantity, updated_at_utc)
              VALUES ({assetSymbol}, {quantity}, {now})
              ON CONFLICT (asset_symbol)
              DO UPDATE SET
                  available_quantity = platform_inventory_positions.available_quantity + EXCLUDED.available_quantity,
                  updated_at_utc = EXCLUDED.updated_at_utc;
              """,
            cancellationToken);
    }

    private static void ValidateObservation(ExternalHedgeExecutionObservation observation)
    {
        if (string.IsNullOrWhiteSpace(observation.ExternalOrderId))
        {
            throw new ArgumentException("ExternalOrderId is required.", nameof(observation.ExternalOrderId));
        }

        if (observation.ExecutedQuantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observation.ExecutedQuantity),
                observation.ExecutedQuantity,
                "ExecutedQuantity must be greater than zero.");
        }

        if (observation.ExecutedUnitPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observation.ExecutedUnitPrice),
                observation.ExecutedUnitPrice,
                "ExecutedUnitPrice must be greater than zero.");
        }
    }

    private static void EnsureMatchingExecutionRecord(
        ExternalHedgeExecutionRecordEntity existing,
        ExternalHedgeExecutionObservation observation,
        string normalizedExternalOrderId)
    {
        if (!string.Equals(existing.AssetSymbol, observation.AssetSymbol.Value, StringComparison.Ordinal) ||
            !string.Equals(existing.QuoteCurrency, observation.QuoteCurrency.Value, StringComparison.Ordinal) ||
            existing.ExecutedQuantity != observation.ExecutedQuantity ||
            existing.ExecutedUnitPrice != observation.ExecutedUnitPrice ||
            existing.ExecutedAtUtc != observation.ExecutedAtUtc)
        {
            throw new IdempotencyKeyConflictException(
                $"External hedge execution '{normalizedExternalOrderId}' was already registered with different execution details.");
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is null)
        {
            return false;
        }

        var exceptionType = exception.InnerException.GetType();
        var sqlState = exceptionType.GetProperty("SqlState")?.GetValue(exception.InnerException) as string;
        if (string.Equals(sqlState, "23505", StringComparison.Ordinal))
        {
            return true;
        }

        var message = exception.InnerException.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase);
    }
}
