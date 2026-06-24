using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.Infrastructure.Persistence;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreCryptoOwnershipLedger(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    TimeProvider timeProvider) : ICryptoOwnershipLedger
{
    public async Task<decimal> GetAvailablePlatformInventoryAsync(AssetSymbol assetSymbol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var position = await context.PlatformInventoryPositions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AssetSymbol == assetSymbol.Value, cancellationToken);
        if (position is null)
        {
            throw new InvalidOperationException(
                $"Platform inventory for asset '{assetSymbol.Value}' was not found. Ensure database migrations and seed data are applied.");
        }

        return position.AvailableQuantity;
    }

    public async Task<BrokeredCryptoBuyReceipt?> GetRecordedCustomerBuyAsync(
        string customerAccountId,
        AssetSymbol assetSymbol,
        string clientOrderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrderId);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedCustomerAccountId = customerAccountId.Trim();
        var normalizedClientOrderId = clientOrderId.Trim();

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.BrokeredCryptoBuyExecutions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerAccountId == normalizedCustomerAccountId
                    && candidate.AssetSymbol == assetSymbol.Value
                    && candidate.ClientOrderId == normalizedClientOrderId,
                cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<BrokeredCryptoBuyReceipt> RecordCustomerBuyAsync(
        OwnershipLedgerBuyRecordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(command);

        var normalizedCustomerAccountId = command.CustomerAccountId.Trim();
        var normalizedClientOrderId = command.ClientOrderId.Trim();

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var existing = await context.BrokeredCryptoBuyExecutions.SingleOrDefaultAsync(
            candidate => candidate.CustomerAccountId == normalizedCustomerAccountId
                && candidate.AssetSymbol == command.AssetSymbol.Value
                && candidate.ClientOrderId == normalizedClientOrderId,
            cancellationToken);
        if (existing is not null)
        {
            var existingReceipt = Map(existing);
            EnsureMatchingDuplicate(command, existingReceipt);
            await transaction.RollbackAsync(cancellationToken);
            return existingReceipt;
        }

        var now = timeProvider.GetUtcNow();
        await ReserveInternalInventoryAsync(
            context,
            command.AssetSymbol.Value,
            command.InternalFillQuantity,
            now,
            cancellationToken);
        await UpsertCustomerOwnershipAsync(
            context,
            normalizedCustomerAccountId,
            command.AssetSymbol.Value,
            command.Quantity,
            now,
            cancellationToken);

        context.BrokeredCryptoBuyExecutions.Add(new BrokeredCryptoBuyExecutionEntity
        {
            Id = Guid.CreateVersion7(),
            ClientOrderId = normalizedClientOrderId,
            CustomerAccountId = normalizedCustomerAccountId,
            AssetSymbol = command.AssetSymbol.Value,
            QuoteCurrency = command.QuoteCurrency.Value,
            Quantity = command.Quantity,
            InternalFillQuantity = command.InternalFillQuantity,
            ExternalHedgeQuantity = command.ExternalHedgeQuantity,
            UnitPrice = command.UnitPrice,
            TotalCost = command.TotalCost,
            ExecutedAtUtc = command.ExecutedAtUtc,
            ExternalHedgeOrderId = command.ExternalHedgeOrderId,
            CreatedAtUtc = now
        });

        var operationId = BuildBuyOperationId(normalizedCustomerAccountId, command.AssetSymbol.Value, normalizedClientOrderId);
        var ledgerTransactionId = Guid.CreateVersion7();
        context.CryptoLedgerTransactions.Add(new CryptoLedgerTransactionEntity
        {
            Id = ledgerTransactionId,
            OperationType = CryptoLedgerOperationTypes.BrokeredCryptoBuy,
            OperationId = operationId,
            ExecutedAtUtc = command.ExecutedAtUtc,
            CreatedAtUtc = now
        });

        var entries = new List<CryptoLedgerEntryEntity>(capacity: 3)
        {
            new()
            {
                Id = Guid.CreateVersion7(),
                TransactionId = ledgerTransactionId,
                Sequence = 1,
                AssetSymbol = command.AssetSymbol.Value,
                AccountKind = CryptoLedgerAccountKinds.CustomerOwnership,
                AccountId = normalizedCustomerAccountId,
                Direction = CryptoLedgerEntryDirection.Debit,
                Quantity = command.Quantity,
                CreatedAtUtc = now
            }
        };

        var sequence = 2;
        if (command.InternalFillQuantity > 0m)
        {
            entries.Add(new CryptoLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = ledgerTransactionId,
                Sequence = sequence++,
                AssetSymbol = command.AssetSymbol.Value,
                AccountKind = CryptoLedgerAccountKinds.PlatformInventory,
                AccountId = null,
                Direction = CryptoLedgerEntryDirection.Credit,
                Quantity = command.InternalFillQuantity,
                CreatedAtUtc = now
            });
        }

        if (command.ExternalHedgeQuantity > 0m)
        {
            entries.Add(new CryptoLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = ledgerTransactionId,
                Sequence = sequence,
                AssetSymbol = command.AssetSymbol.Value,
                AccountKind = CryptoLedgerAccountKinds.PlatformExternalHedgePending,
                AccountId = null,
                Direction = CryptoLedgerEntryDirection.Credit,
                Quantity = command.ExternalHedgeQuantity,
                CreatedAtUtc = now
            });
        }

        context.CryptoLedgerEntries.AddRange(entries);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            var duplicate = await GetRecordedCustomerBuyAsync(
                normalizedCustomerAccountId,
                command.AssetSymbol,
                normalizedClientOrderId,
                cancellationToken);
            if (duplicate is null)
            {
                throw;
            }

            EnsureMatchingDuplicate(command, duplicate);
            return duplicate;
        }

        return new BrokeredCryptoBuyReceipt(
            normalizedClientOrderId,
            normalizedCustomerAccountId,
            command.AssetSymbol.Value,
            command.QuoteCurrency.Value,
            command.Quantity,
            command.InternalFillQuantity,
            command.ExternalHedgeQuantity,
            command.UnitPrice,
            command.TotalCost,
            command.ExecutedAtUtc,
            command.ExternalHedgeOrderId);
    }

    private static string BuildBuyOperationId(string customerAccountId, string assetSymbol, string clientOrderId)
    {
        return $"{customerAccountId}:{assetSymbol}:{clientOrderId}";
    }

    private static async Task ReserveInternalInventoryAsync(
        CryptoTransactionsDbContext context,
        string assetSymbol,
        decimal internalFillQuantity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (internalFillQuantity <= 0m)
        {
            return;
        }

        var affectedRows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
              UPDATE platform_inventory_positions
              SET available_quantity = available_quantity - {internalFillQuantity},
                  updated_at_utc = {now}
              WHERE asset_symbol = {assetSymbol}
                AND available_quantity >= {internalFillQuantity};
              """,
            cancellationToken);
        if (affectedRows > 0)
        {
            return;
        }

        var availableInventory = await context.PlatformInventoryPositions
            .AsNoTracking()
            .Where(candidate => candidate.AssetSymbol == assetSymbol)
            .Select(candidate => candidate.AvailableQuantity)
            .SingleOrDefaultAsync(cancellationToken);
        throw new InsufficientFundsException(
            $"Insufficient internal inventory for {assetSymbol}. Available: {availableInventory}, required: {internalFillQuantity}.");
    }

    private static Task UpsertCustomerOwnershipAsync(
        CryptoTransactionsDbContext context,
        string customerAccountId,
        string assetSymbol,
        decimal quantity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
              INSERT INTO crypto_ownership_positions (customer_account_id, asset_symbol, quantity, updated_at_utc)
              VALUES ({customerAccountId}, {assetSymbol}, {quantity}, {now})
              ON CONFLICT (customer_account_id, asset_symbol)
              DO UPDATE SET
                  quantity = crypto_ownership_positions.quantity + EXCLUDED.quantity,
                  updated_at_utc = EXCLUDED.updated_at_utc;
              """,
            cancellationToken);
    }

    private static BrokeredCryptoBuyReceipt Map(BrokeredCryptoBuyExecutionEntity entity)
    {
        return new BrokeredCryptoBuyReceipt(
            entity.ClientOrderId,
            entity.CustomerAccountId,
            entity.AssetSymbol,
            entity.QuoteCurrency,
            entity.Quantity,
            entity.InternalFillQuantity,
            entity.ExternalHedgeQuantity,
            entity.UnitPrice,
            entity.TotalCost,
            entity.ExecutedAtUtc,
            entity.ExternalHedgeOrderId);
    }

    private static void Validate(OwnershipLedgerBuyRecordCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerAccountId))
        {
            throw new ArgumentException("CustomerAccountId is required.", nameof(command.CustomerAccountId));
        }

        if (string.IsNullOrWhiteSpace(command.ClientOrderId))
        {
            throw new ArgumentException("ClientOrderId is required.", nameof(command.ClientOrderId));
        }

        if (command.Quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(command.Quantity), command.Quantity, "Quantity must be greater than zero.");
        }

        if (command.InternalFillQuantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(command.InternalFillQuantity), command.InternalFillQuantity, "InternalFillQuantity cannot be negative.");
        }

        if (command.ExternalHedgeQuantity < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(command.ExternalHedgeQuantity), command.ExternalHedgeQuantity, "ExternalHedgeQuantity cannot be negative.");
        }

        if (checked(command.InternalFillQuantity + command.ExternalHedgeQuantity) != command.Quantity)
        {
            throw new ArgumentException("InternalFillQuantity plus ExternalHedgeQuantity must equal Quantity.");
        }

        if (command.UnitPrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(command.UnitPrice), command.UnitPrice, "UnitPrice must be greater than zero.");
        }

        if (command.TotalCost <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(command.TotalCost), command.TotalCost, "TotalCost must be greater than zero.");
        }
    }

    private static void EnsureMatchingDuplicate(OwnershipLedgerBuyRecordCommand command, BrokeredCryptoBuyReceipt existing)
    {
        if (existing.Quantity != command.Quantity
            || existing.InternalFillQuantity != command.InternalFillQuantity
            || existing.ExternalHedgeQuantity != command.ExternalHedgeQuantity
            || existing.UnitPrice != command.UnitPrice
            || existing.TotalCost != command.TotalCost
            || !string.Equals(existing.QuoteCurrency, command.QuoteCurrency.Value, StringComparison.Ordinal)
            || !string.Equals(existing.AssetSymbol, command.AssetSymbol.Value, StringComparison.Ordinal)
            || !string.Equals(existing.ExternalHedgeOrderId, command.ExternalHedgeOrderId, StringComparison.Ordinal))
        {
            throw new IdempotencyKeyConflictException(
                $"Client order id '{command.ClientOrderId}' was already used with a different brokered buy request.");
        }
    }

}
