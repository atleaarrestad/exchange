using Exchange.FiatTransactions.Application;
using Exchange.FiatTransactions.Application.Contracts;
using Exchange.FiatTransactions.Domain.ValueObjects;
using Exchange.Infrastructure.Persistence;
using Exchange.FiatTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.FiatTransactions.Infrastructure.Gateways;

public sealed class EfCoreFiatLedger(
    IDbContextFactory<FiatTransactionsDbContext> dbContextFactory,
    TimeProvider timeProvider) : IFiatLedger
{
    public async Task<decimal> GetCustomerAvailableBalanceAsync(
        string customerAccountId,
        FiatCurrency fiatCurrency,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerAccountId);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedCustomerAccountId = customerAccountId.Trim();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var amount = await context.FiatBalancePositions
            .AsNoTracking()
            .Where(candidate => candidate.FiatCurrency == fiatCurrency.Value
                && candidate.AccountKind == FiatLedgerAccountKinds.CustomerAvailable
                && candidate.AccountId == normalizedCustomerAccountId)
            .Select(candidate => candidate.AvailableAmount)
            .SingleOrDefaultAsync(cancellationToken);
        return amount;
    }

    public async Task<decimal> GetPlatformTradeClearingBalanceAsync(
        FiatCurrency fiatCurrency,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var amount = await context.FiatBalancePositions
            .AsNoTracking()
            .Where(candidate => candidate.FiatCurrency == fiatCurrency.Value
                && candidate.AccountKind == FiatLedgerAccountKinds.PlatformTradeClearing
                && candidate.AccountId == FiatLedgerAccountIds.Platform)
            .Select(candidate => candidate.AvailableAmount)
            .SingleOrDefaultAsync(cancellationToken);
        return amount;
    }

    public async Task<FiatBrokeredBuySettlementReceipt?> GetRecordedBrokeredBuySettlementAsync(
        string customerAccountId,
        string clientOrderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrderId);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedCustomerAccountId = customerAccountId.Trim();
        var normalizedClientOrderId = clientOrderId.Trim();
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settlement = await context.BrokeredCryptoBuySettlements
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerAccountId == normalizedCustomerAccountId
                    && candidate.ClientOrderId == normalizedClientOrderId,
                cancellationToken);
        return settlement is null ? null : Map(settlement);
    }

    public async Task<FiatBrokeredBuyReservationReceipt?> GetRecordedBrokeredBuyReservationAsync(
        string customerAccountId,
        string clientOrderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientOrderId);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedCustomerAccountId = customerAccountId.Trim();
        var normalizedClientOrderId = clientOrderId.Trim();
        var operationId = BuildOperationId(normalizedCustomerAccountId, normalizedClientOrderId);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var transaction = await context.FiatLedgerTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OperationType == FiatLedgerOperationTypes.BrokeredCryptoBuyReservation
                    && candidate.OperationId == operationId,
                cancellationToken);
        if (transaction is null)
        {
            return null;
        }

        var reservedEntry = await context.FiatLedgerEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.TransactionId == transaction.Id
                    && candidate.Sequence == 2
                    && candidate.AccountKind == FiatLedgerAccountKinds.CustomerReserved
                    && candidate.AccountId == normalizedCustomerAccountId
                    && candidate.Direction == FiatLedgerEntryDirection.Increase,
                cancellationToken);
        if (reservedEntry is null)
        {
            throw new InvalidOperationException(
                $"Fiat reservation transaction '{transaction.Id}' is missing reserved balance journal entry.");
        }

        return new FiatBrokeredBuyReservationReceipt(
            normalizedClientOrderId,
            normalizedCustomerAccountId,
            reservedEntry.FiatCurrency,
            reservedEntry.Amount,
            transaction.ExecutedAtUtc);
    }

    public async Task<FiatBrokeredBuyReservationReceipt> ReserveBrokeredBuyFundsAsync(
        FiatLedgerBrokeredBuyReservationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(command);

        var normalizedCustomerAccountId = command.CustomerAccountId.Trim();
        var normalizedClientOrderId = command.ClientOrderId.Trim();
        var operationId = BuildOperationId(normalizedCustomerAccountId, normalizedClientOrderId);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var existingTransaction = await context.FiatLedgerTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OperationType == FiatLedgerOperationTypes.BrokeredCryptoBuyReservation
                    && candidate.OperationId == operationId,
                cancellationToken);
        if (existingTransaction is not null)
        {
            var existing = await GetRecordedBrokeredBuyReservationAsync(
                normalizedCustomerAccountId,
                normalizedClientOrderId,
                cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException(
                    $"Reservation transaction '{existingTransaction.Id}' exists without a materialized receipt.");
            }

            EnsureMatchingDuplicate(command, existing);
            await transaction.RollbackAsync(cancellationToken);
            return existing;
        }

        var now = timeProvider.GetUtcNow();
        await DecreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.CustomerAvailable,
            normalizedCustomerAccountId,
            command.ReservedAmount,
            now,
            cancellationToken);
        await IncreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.CustomerReserved,
            normalizedCustomerAccountId,
            command.ReservedAmount,
            now,
            cancellationToken);

        var transactionId = Guid.CreateVersion7();
        context.FiatLedgerTransactions.Add(new FiatLedgerTransactionEntity
        {
            Id = transactionId,
            OperationType = FiatLedgerOperationTypes.BrokeredCryptoBuyReservation,
            OperationId = operationId,
            ExecutedAtUtc = command.ReservedAtUtc,
            CreatedAtUtc = now
        });
        context.FiatLedgerEntries.AddRange(
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 1,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.CustomerAvailable,
                AccountId = normalizedCustomerAccountId,
                Direction = FiatLedgerEntryDirection.Decrease,
                Amount = command.ReservedAmount,
                CreatedAtUtc = now
            },
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 2,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.CustomerReserved,
                AccountId = normalizedCustomerAccountId,
                Direction = FiatLedgerEntryDirection.Increase,
                Amount = command.ReservedAmount,
                CreatedAtUtc = now
            });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            var duplicate = await GetRecordedBrokeredBuyReservationAsync(
                normalizedCustomerAccountId,
                normalizedClientOrderId,
                cancellationToken);
            if (duplicate is null)
            {
                throw;
            }

            EnsureMatchingDuplicate(command, duplicate);
            return duplicate;
        }

        return new FiatBrokeredBuyReservationReceipt(
            normalizedClientOrderId,
            normalizedCustomerAccountId,
            command.FiatCurrency.Value,
            command.ReservedAmount,
            command.ReservedAtUtc);
    }

    public async Task<FiatBrokeredBuySettlementReceipt> CaptureReservedBrokeredBuySettlementAsync(
        FiatLedgerBrokeredBuyReservationCaptureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(command);

        var normalizedCustomerAccountId = command.CustomerAccountId.Trim();
        var normalizedClientOrderId = command.ClientOrderId.Trim();

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var existing = await context.BrokeredCryptoBuySettlements
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerAccountId == normalizedCustomerAccountId
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
        await DecreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.CustomerReserved,
            normalizedCustomerAccountId,
            command.CustomerDebitAmount,
            now,
            cancellationToken);
        await IncreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.PlatformTradeClearing,
            FiatLedgerAccountIds.Platform,
            command.CustomerDebitAmount,
            now,
            cancellationToken);

        context.BrokeredCryptoBuySettlements.Add(new BrokeredCryptoBuySettlementEntity
        {
            Id = Guid.CreateVersion7(),
            ClientOrderId = normalizedClientOrderId,
            CustomerAccountId = normalizedCustomerAccountId,
            FiatCurrency = command.FiatCurrency.Value,
            CustomerDebitAmount = command.CustomerDebitAmount,
            ExecutedAtUtc = command.ExecutedAtUtc,
            CreatedAtUtc = now
        });

        var operationId = BuildOperationId(normalizedCustomerAccountId, normalizedClientOrderId);
        var transactionId = Guid.CreateVersion7();
        context.FiatLedgerTransactions.Add(new FiatLedgerTransactionEntity
        {
            Id = transactionId,
            OperationType = FiatLedgerOperationTypes.BrokeredCryptoBuySettlement,
            OperationId = operationId,
            ExecutedAtUtc = command.ExecutedAtUtc,
            CreatedAtUtc = now
        });
        context.FiatLedgerEntries.AddRange(
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 1,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.CustomerReserved,
                AccountId = normalizedCustomerAccountId,
                Direction = FiatLedgerEntryDirection.Decrease,
                Amount = command.CustomerDebitAmount,
                CreatedAtUtc = now
            },
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 2,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.PlatformTradeClearing,
                AccountId = FiatLedgerAccountIds.Platform,
                Direction = FiatLedgerEntryDirection.Increase,
                Amount = command.CustomerDebitAmount,
                CreatedAtUtc = now
            });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            var duplicate = await GetRecordedBrokeredBuySettlementAsync(
                normalizedCustomerAccountId,
                normalizedClientOrderId,
                cancellationToken);
            if (duplicate is null)
            {
                throw;
            }

            EnsureMatchingDuplicate(command, duplicate);
            return duplicate;
        }

        return new FiatBrokeredBuySettlementReceipt(
            normalizedClientOrderId,
            normalizedCustomerAccountId,
            command.FiatCurrency.Value,
            command.CustomerDebitAmount,
            command.ExecutedAtUtc);
    }

    public async Task<FiatBrokeredBuyReservationReleaseReceipt> ReleaseReservedBrokeredBuyFundsAsync(
        FiatLedgerBrokeredBuyReservationReleaseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(command);

        var normalizedCustomerAccountId = command.CustomerAccountId.Trim();
        var normalizedClientOrderId = command.ClientOrderId.Trim();
        var operationId = BuildOperationId(normalizedCustomerAccountId, normalizedClientOrderId);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var existingTransaction = await context.FiatLedgerTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OperationType == FiatLedgerOperationTypes.BrokeredCryptoBuyReservationRelease
                    && candidate.OperationId == operationId,
                cancellationToken);
        if (existingTransaction is not null)
        {
            var existingReservedEntry = await context.FiatLedgerEntries
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.TransactionId == existingTransaction.Id
                        && candidate.Sequence == 2
                        && candidate.AccountKind == FiatLedgerAccountKinds.CustomerAvailable
                        && candidate.AccountId == normalizedCustomerAccountId
                        && candidate.Direction == FiatLedgerEntryDirection.Increase,
                    cancellationToken);
            if (existingReservedEntry is null)
            {
                throw new InvalidOperationException(
                    $"Fiat release transaction '{existingTransaction.Id}' is missing customer available journal entry.");
            }

            var existing = new FiatBrokeredBuyReservationReleaseReceipt(
                normalizedClientOrderId,
                normalizedCustomerAccountId,
                existingReservedEntry.FiatCurrency,
                existingReservedEntry.Amount,
                existingTransaction.ExecutedAtUtc);
            EnsureMatchingDuplicate(command, existing);
            await transaction.RollbackAsync(cancellationToken);
            return existing;
        }

        var now = timeProvider.GetUtcNow();
        await DecreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.CustomerReserved,
            normalizedCustomerAccountId,
            command.ReleasedAmount,
            now,
            cancellationToken);
        await IncreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.CustomerAvailable,
            normalizedCustomerAccountId,
            command.ReleasedAmount,
            now,
            cancellationToken);

        var transactionId = Guid.CreateVersion7();
        context.FiatLedgerTransactions.Add(new FiatLedgerTransactionEntity
        {
            Id = transactionId,
            OperationType = FiatLedgerOperationTypes.BrokeredCryptoBuyReservationRelease,
            OperationId = operationId,
            ExecutedAtUtc = command.ReleasedAtUtc,
            CreatedAtUtc = now
        });
        context.FiatLedgerEntries.AddRange(
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 1,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.CustomerReserved,
                AccountId = normalizedCustomerAccountId,
                Direction = FiatLedgerEntryDirection.Decrease,
                Amount = command.ReleasedAmount,
                CreatedAtUtc = now
            },
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 2,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.CustomerAvailable,
                AccountId = normalizedCustomerAccountId,
                Direction = FiatLedgerEntryDirection.Increase,
                Amount = command.ReleasedAmount,
                CreatedAtUtc = now
            });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            var duplicateTransaction = await context.FiatLedgerTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.OperationType == FiatLedgerOperationTypes.BrokeredCryptoBuyReservationRelease
                        && candidate.OperationId == operationId,
                    cancellationToken);
            if (duplicateTransaction is null)
            {
                throw;
            }

            var duplicateEntry = await context.FiatLedgerEntries
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.TransactionId == duplicateTransaction.Id
                        && candidate.Sequence == 2
                        && candidate.AccountKind == FiatLedgerAccountKinds.CustomerAvailable
                        && candidate.AccountId == normalizedCustomerAccountId
                        && candidate.Direction == FiatLedgerEntryDirection.Increase,
                    cancellationToken);
            if (duplicateEntry is null)
            {
                throw;
            }

            var duplicate = new FiatBrokeredBuyReservationReleaseReceipt(
                normalizedClientOrderId,
                normalizedCustomerAccountId,
                duplicateEntry.FiatCurrency,
                duplicateEntry.Amount,
                duplicateTransaction.ExecutedAtUtc);
            EnsureMatchingDuplicate(command, duplicate);
            return duplicate;
        }

        return new FiatBrokeredBuyReservationReleaseReceipt(
            normalizedClientOrderId,
            normalizedCustomerAccountId,
            command.FiatCurrency.Value,
            command.ReleasedAmount,
            command.ReleasedAtUtc);
    }

    public async Task<FiatBrokeredBuySettlementReceipt> RecordBrokeredBuySettlementAsync(
        FiatLedgerBrokeredBuyPostingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(command);

        var normalizedCustomerAccountId = command.CustomerAccountId.Trim();
        var normalizedClientOrderId = command.ClientOrderId.Trim();

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var existing = await context.BrokeredCryptoBuySettlements
            .SingleOrDefaultAsync(
                candidate => candidate.CustomerAccountId == normalizedCustomerAccountId
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
        await DecreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.CustomerAvailable,
            normalizedCustomerAccountId,
            command.CustomerDebitAmount,
            now,
            cancellationToken);
        await IncreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.PlatformTradeClearing,
            FiatLedgerAccountIds.Platform,
            command.CustomerDebitAmount,
            now,
            cancellationToken);

        context.BrokeredCryptoBuySettlements.Add(new BrokeredCryptoBuySettlementEntity
        {
            Id = Guid.CreateVersion7(),
            ClientOrderId = normalizedClientOrderId,
            CustomerAccountId = normalizedCustomerAccountId,
            FiatCurrency = command.FiatCurrency.Value,
            CustomerDebitAmount = command.CustomerDebitAmount,
            ExecutedAtUtc = command.ExecutedAtUtc,
            CreatedAtUtc = now
        });

        var operationId = BuildOperationId(normalizedCustomerAccountId, normalizedClientOrderId);
        var transactionId = Guid.CreateVersion7();
        context.FiatLedgerTransactions.Add(new FiatLedgerTransactionEntity
        {
            Id = transactionId,
            OperationType = FiatLedgerOperationTypes.BrokeredCryptoBuySettlement,
            OperationId = operationId,
            ExecutedAtUtc = command.ExecutedAtUtc,
            CreatedAtUtc = now
        });
        context.FiatLedgerEntries.AddRange(
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 1,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.CustomerAvailable,
                AccountId = normalizedCustomerAccountId,
                Direction = FiatLedgerEntryDirection.Decrease,
                Amount = command.CustomerDebitAmount,
                CreatedAtUtc = now
            },
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 2,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.PlatformTradeClearing,
                AccountId = FiatLedgerAccountIds.Platform,
                Direction = FiatLedgerEntryDirection.Increase,
                Amount = command.CustomerDebitAmount,
                CreatedAtUtc = now
            });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            var duplicate = await GetRecordedBrokeredBuySettlementAsync(
                normalizedCustomerAccountId,
                normalizedClientOrderId,
                cancellationToken);
            if (duplicate is null)
            {
                throw;
            }

            EnsureMatchingDuplicate(command, duplicate);
            return duplicate;
        }

        return new FiatBrokeredBuySettlementReceipt(
            normalizedClientOrderId,
            normalizedCustomerAccountId,
            command.FiatCurrency.Value,
            command.CustomerDebitAmount,
            command.ExecutedAtUtc);
    }

    public async Task<FiatBankSettlementReceipt?> GetRecordedBankSettlementAsync(
        string bankReferenceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankReferenceId);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedBankReferenceId = bankReferenceId.Trim();
        var operationId = BuildOperationId(normalizedBankReferenceId);
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var transaction = await context.FiatLedgerTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OperationType == FiatLedgerOperationTypes.BankSettlement
                    && candidate.OperationId == operationId,
                cancellationToken);
        if (transaction is null)
        {
            return null;
        }

        var bankEntry = await context.FiatLedgerEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.TransactionId == transaction.Id
                    && candidate.Sequence == 2
                    && candidate.AccountKind == FiatLedgerAccountKinds.PlatformBankCash
                    && candidate.AccountId == FiatLedgerAccountIds.Platform
                    && candidate.Direction == FiatLedgerEntryDirection.Increase,
                cancellationToken);
        if (bankEntry is null)
        {
            throw new InvalidOperationException(
                $"Fiat ledger transaction '{transaction.Id}' is missing bank settlement journal entry.");
        }

        return new FiatBankSettlementReceipt(
            normalizedBankReferenceId,
            bankEntry.FiatCurrency,
            bankEntry.Amount,
            transaction.ExecutedAtUtc);
    }

    public async Task<FiatBankSettlementReceipt> RecordBankSettlementAsync(
        FiatLedgerBankSettlementPostingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(command);

        var normalizedBankReferenceId = command.BankReferenceId.Trim();
        var operationId = BuildOperationId(normalizedBankReferenceId);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var existingTransaction = await context.FiatLedgerTransactions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OperationType == FiatLedgerOperationTypes.BankSettlement
                    && candidate.OperationId == operationId,
                cancellationToken);
        FiatBankSettlementReceipt? existing = null;
        if (existingTransaction is not null)
        {
            var existingBankEntry = await context.FiatLedgerEntries
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.TransactionId == existingTransaction.Id
                        && candidate.Sequence == 2
                        && candidate.AccountKind == FiatLedgerAccountKinds.PlatformBankCash
                        && candidate.AccountId == FiatLedgerAccountIds.Platform
                        && candidate.Direction == FiatLedgerEntryDirection.Increase,
                    cancellationToken);
            if (existingBankEntry is null)
            {
                throw new InvalidOperationException(
                    $"Fiat ledger transaction '{existingTransaction.Id}' is missing bank settlement journal entry.");
            }

            existing = new FiatBankSettlementReceipt(
                normalizedBankReferenceId,
                existingBankEntry.FiatCurrency,
                existingBankEntry.Amount,
                existingTransaction.ExecutedAtUtc);
        }
        if (existing is not null)
        {
            EnsureMatchingDuplicate(command, existing);
            await transaction.RollbackAsync(cancellationToken);
            return existing;
        }

        var now = timeProvider.GetUtcNow();
        await DecreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.PlatformTradeClearing,
            FiatLedgerAccountIds.Platform,
            command.Amount,
            now,
            cancellationToken);
        await IncreaseBalanceAsync(
            context,
            command.FiatCurrency.Value,
            FiatLedgerAccountKinds.PlatformBankCash,
            FiatLedgerAccountIds.Platform,
            command.Amount,
            now,
            cancellationToken);

        var transactionId = Guid.CreateVersion7();
        context.FiatLedgerTransactions.Add(new FiatLedgerTransactionEntity
        {
            Id = transactionId,
            OperationType = FiatLedgerOperationTypes.BankSettlement,
            OperationId = operationId,
            ExecutedAtUtc = command.ExecutedAtUtc,
            CreatedAtUtc = now
        });
        context.FiatLedgerEntries.AddRange(
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 1,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.PlatformTradeClearing,
                AccountId = FiatLedgerAccountIds.Platform,
                Direction = FiatLedgerEntryDirection.Decrease,
                Amount = command.Amount,
                CreatedAtUtc = now
            },
            new FiatLedgerEntryEntity
            {
                Id = Guid.CreateVersion7(),
                TransactionId = transactionId,
                Sequence = 2,
                FiatCurrency = command.FiatCurrency.Value,
                AccountKind = FiatLedgerAccountKinds.PlatformBankCash,
                AccountId = FiatLedgerAccountIds.Platform,
                Direction = FiatLedgerEntryDirection.Increase,
                Amount = command.Amount,
                CreatedAtUtc = now
            });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            var duplicate = await GetRecordedBankSettlementAsync(normalizedBankReferenceId, cancellationToken);
            if (duplicate is null)
            {
                throw;
            }

            EnsureMatchingDuplicate(command, duplicate);
            return duplicate;
        }

        return new FiatBankSettlementReceipt(
            normalizedBankReferenceId,
            command.FiatCurrency.Value,
            command.Amount,
            command.ExecutedAtUtc);
    }

    private static string BuildOperationId(params string[] segments)
    {
        var normalizedSegments = segments.Select(segment => segment.Trim()).ToArray();
        return string.Join("|", normalizedSegments.Select(segment => $"{segment.Length}:{segment}"));
    }

    private static async Task IncreaseBalanceAsync(
        FiatTransactionsDbContext context,
        string fiatCurrency,
        string accountKind,
        string accountId,
        decimal amount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updatedRows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
              UPDATE fiat_balance_positions
              SET available_amount = available_amount + {amount},
                  updated_at_utc = {now}
              WHERE fiat_currency = {fiatCurrency}
                AND account_kind = {accountKind}
                AND account_id = {accountId};
              """,
            cancellationToken);
        if (updatedRows > 0)
        {
            return;
        }

        try
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                  INSERT INTO fiat_balance_positions (fiat_currency, account_kind, account_id, available_amount, updated_at_utc)
                  VALUES ({fiatCurrency}, {accountKind}, {accountId}, {amount}, {now});
                  """,
                cancellationToken);
        }
        catch (Exception exception) when (UniqueConstraintViolationDetector.IsUniqueConstraintViolation(exception))
        {
            // A concurrent transaction inserted the same balance row first; apply the increment on retry.
            var retriedRows = await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                  UPDATE fiat_balance_positions
                  SET available_amount = available_amount + {amount},
                      updated_at_utc = {now}
                  WHERE fiat_currency = {fiatCurrency}
                    AND account_kind = {accountKind}
                    AND account_id = {accountId};
                  """,
                cancellationToken);
            if (retriedRows == 0)
            {
                throw;
            }
        }
    }

    private static async Task DecreaseBalanceAsync(
        FiatTransactionsDbContext context,
        string fiatCurrency,
        string accountKind,
        string accountId,
        decimal amount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updatedRows = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
              UPDATE fiat_balance_positions
              SET available_amount = available_amount - {amount},
                  updated_at_utc = {now}
              WHERE fiat_currency = {fiatCurrency}
                AND account_kind = {accountKind}
                AND account_id = {accountId}
                AND available_amount >= {amount};
              """,
            cancellationToken);
        if (updatedRows > 0)
        {
            return;
        }

        var currentAmount = await context.FiatBalancePositions
            .AsNoTracking()
            .Where(candidate => candidate.FiatCurrency == fiatCurrency
                && candidate.AccountKind == accountKind
                && candidate.AccountId == accountId)
            .Select(candidate => candidate.AvailableAmount)
            .SingleOrDefaultAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Insufficient fiat balance for {fiatCurrency} on {accountKind}/{accountId}. Available: {currentAmount}, required: {amount}.");
    }

    private static FiatBrokeredBuySettlementReceipt Map(BrokeredCryptoBuySettlementEntity entity)
    {
        return new FiatBrokeredBuySettlementReceipt(
            entity.ClientOrderId,
            entity.CustomerAccountId,
            entity.FiatCurrency,
            entity.CustomerDebitAmount,
            entity.ExecutedAtUtc);
    }

    private static void Validate(FiatLedgerBrokeredBuyPostingCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerAccountId))
        {
            throw new ArgumentException("CustomerAccountId is required.", nameof(command.CustomerAccountId));
        }

        if (string.IsNullOrWhiteSpace(command.ClientOrderId))
        {
            throw new ArgumentException("ClientOrderId is required.", nameof(command.ClientOrderId));
        }

        if (command.CustomerDebitAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.CustomerDebitAmount),
                command.CustomerDebitAmount,
                "CustomerDebitAmount must be greater than zero.");
        }

        if (!FiatCurrency.TryParse(command.FiatCurrency.Value, out _))
        {
            throw new ArgumentException("FiatCurrency is required and must be supported.", nameof(command.FiatCurrency));
        }
    }

    private static void Validate(FiatLedgerBrokeredBuyReservationCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerAccountId))
        {
            throw new ArgumentException("CustomerAccountId is required.", nameof(command.CustomerAccountId));
        }

        if (string.IsNullOrWhiteSpace(command.ClientOrderId))
        {
            throw new ArgumentException("ClientOrderId is required.", nameof(command.ClientOrderId));
        }

        if (command.ReservedAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.ReservedAmount),
                command.ReservedAmount,
                "ReservedAmount must be greater than zero.");
        }

        if (!FiatCurrency.TryParse(command.FiatCurrency.Value, out _))
        {
            throw new ArgumentException("FiatCurrency is required and must be supported.", nameof(command.FiatCurrency));
        }
    }

    private static void Validate(FiatLedgerBrokeredBuyReservationCaptureCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerAccountId))
        {
            throw new ArgumentException("CustomerAccountId is required.", nameof(command.CustomerAccountId));
        }

        if (string.IsNullOrWhiteSpace(command.ClientOrderId))
        {
            throw new ArgumentException("ClientOrderId is required.", nameof(command.ClientOrderId));
        }

        if (command.CustomerDebitAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.CustomerDebitAmount),
                command.CustomerDebitAmount,
                "CustomerDebitAmount must be greater than zero.");
        }

        if (!FiatCurrency.TryParse(command.FiatCurrency.Value, out _))
        {
            throw new ArgumentException("FiatCurrency is required and must be supported.", nameof(command.FiatCurrency));
        }
    }

    private static void Validate(FiatLedgerBrokeredBuyReservationReleaseCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerAccountId))
        {
            throw new ArgumentException("CustomerAccountId is required.", nameof(command.CustomerAccountId));
        }

        if (string.IsNullOrWhiteSpace(command.ClientOrderId))
        {
            throw new ArgumentException("ClientOrderId is required.", nameof(command.ClientOrderId));
        }

        if (command.ReleasedAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.ReleasedAmount),
                command.ReleasedAmount,
                "ReleasedAmount must be greater than zero.");
        }

        if (!FiatCurrency.TryParse(command.FiatCurrency.Value, out _))
        {
            throw new ArgumentException("FiatCurrency is required and must be supported.", nameof(command.FiatCurrency));
        }
    }

    private static void Validate(FiatLedgerBankSettlementPostingCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.BankReferenceId))
        {
            throw new ArgumentException("BankReferenceId is required.", nameof(command.BankReferenceId));
        }

        if (command.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.Amount),
                command.Amount,
                "Amount must be greater than zero.");
        }

        if (!FiatCurrency.TryParse(command.FiatCurrency.Value, out _))
        {
            throw new ArgumentException("FiatCurrency is required and must be supported.", nameof(command.FiatCurrency));
        }
    }

    private static void EnsureMatchingDuplicate(
        FiatLedgerBrokeredBuyPostingCommand command,
        FiatBrokeredBuySettlementReceipt existing)
    {
        if (existing.CustomerDebitAmount != command.CustomerDebitAmount ||
            !string.Equals(existing.FiatCurrency, command.FiatCurrency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Client order id '{command.ClientOrderId}' was already used with a different fiat settlement request.");
        }
    }

    private static void EnsureMatchingDuplicate(
        FiatLedgerBrokeredBuyReservationCommand command,
        FiatBrokeredBuyReservationReceipt existing)
    {
        if (existing.ReservedAmount != command.ReservedAmount ||
            !string.Equals(existing.FiatCurrency, command.FiatCurrency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Client order id '{command.ClientOrderId}' was already used with a different fiat reservation request.");
        }
    }

    private static void EnsureMatchingDuplicate(
        FiatLedgerBrokeredBuyReservationCaptureCommand command,
        FiatBrokeredBuySettlementReceipt existing)
    {
        if (existing.CustomerDebitAmount != command.CustomerDebitAmount ||
            !string.Equals(existing.FiatCurrency, command.FiatCurrency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Client order id '{command.ClientOrderId}' was already used with a different fiat reservation capture request.");
        }
    }

    private static void EnsureMatchingDuplicate(
        FiatLedgerBrokeredBuyReservationReleaseCommand command,
        FiatBrokeredBuyReservationReleaseReceipt existing)
    {
        if (existing.ReleasedAmount != command.ReleasedAmount ||
            !string.Equals(existing.FiatCurrency, command.FiatCurrency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Client order id '{command.ClientOrderId}' was already used with a different fiat reservation release request.");
        }
    }

    private static void EnsureMatchingDuplicate(
        FiatLedgerBankSettlementPostingCommand command,
        FiatBankSettlementReceipt existing)
    {
        if (existing.Amount != command.Amount ||
            !string.Equals(existing.FiatCurrency, command.FiatCurrency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Bank reference id '{command.BankReferenceId}' was already used with a different bank settlement request.");
        }
    }

}
