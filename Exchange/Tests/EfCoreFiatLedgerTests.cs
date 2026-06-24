using Exchange.FiatTransactions.Application.Contracts;
using Exchange.FiatTransactions.Domain.ValueObjects;
using Exchange.FiatTransactions.Infrastructure.Gateways;
using Exchange.FiatTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Tests;

[TestClass]
public sealed class EfCoreFiatLedgerTests
{
    private const string DatabasePrefix = "exchange_fiat_ledger_tests_";

    [TestMethod]
    public async Task RecordBrokeredBuySettlementAsync_PersistsBalancesAndJournal()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            var command = new FiatLedgerBrokeredBuyPostingCommand(
                "client-order-fiat-1",
                "customer-fiat-1",
                FiatCurrency.NorwegianKrone,
                1_500m,
                DateTimeOffset.UtcNow);

            var receipt = await ledger.RecordBrokeredBuySettlementAsync(command);

            Assert.AreEqual("client-order-fiat-1", receipt.ClientOrderId);

            var dbOptions = new DbContextOptionsBuilder<FiatTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new FiatTransactionsDbContext(dbOptions);
            var customerBalance = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.CustomerAvailable &&
                    position.AccountId == "customer-fiat-1");
            var clearingBalance = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.PlatformTradeClearing &&
                    position.AccountId == FiatLedgerAccountIds.Platform);
            Assert.AreEqual(48_500m, customerBalance.AvailableAmount);
            Assert.AreEqual(1_500m, clearingBalance.AvailableAmount);

            var transaction = await context.FiatLedgerTransactions
                .AsNoTracking()
                .SingleAsync(candidate => candidate.OperationType == FiatLedgerOperationTypes.BrokeredCryptoBuySettlement);
            var entries = await context.FiatLedgerEntries
                .AsNoTracking()
                .Where(entry => entry.TransactionId == transaction.Id)
                .OrderBy(entry => entry.Sequence)
                .ToArrayAsync();
            Assert.AreEqual(2, entries.Length);

            var increaseTotal = entries
                .Where(entry => entry.Direction == FiatLedgerEntryDirection.Increase)
                .Sum(entry => entry.Amount);
            var decreaseTotal = entries
                .Where(entry => entry.Direction == FiatLedgerEntryDirection.Decrease)
                .Sum(entry => entry.Amount);
            Assert.AreEqual(increaseTotal, decreaseTotal);
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RecordBrokeredBuySettlementAsync_WhenDuplicateRequestMatches_ReturnsOriginalWithoutNewJournal()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            var command = new FiatLedgerBrokeredBuyPostingCommand(
                "client-order-fiat-2",
                "customer-fiat-2",
                FiatCurrency.NorwegianKrone,
                2_000m,
                DateTimeOffset.UtcNow);

            var first = await ledger.RecordBrokeredBuySettlementAsync(command);
            var second = await ledger.RecordBrokeredBuySettlementAsync(command);

            Assert.AreEqual(first.ClientOrderId, second.ClientOrderId);
            Assert.AreEqual(first.CustomerDebitAmount, second.CustomerDebitAmount);

            var dbOptions = new DbContextOptionsBuilder<FiatTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new FiatTransactionsDbContext(dbOptions);
            Assert.AreEqual(1, await context.BrokeredCryptoBuySettlements.CountAsync());
            Assert.AreEqual(1, await context.FiatLedgerTransactions.CountAsync());
            Assert.AreEqual(2, await context.FiatLedgerEntries.CountAsync());
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RecordBrokeredBuySettlementAsync_WhenFiatCurrencyIsDefault_ThrowsArgumentException()
    {
        var ledger = CreateLedger(out var databaseName, out _);
        try
        {
            var command = new FiatLedgerBrokeredBuyPostingCommand(
                "client-order-fiat-invalid-currency",
                "customer-fiat-1",
                default,
                100m,
                DateTimeOffset.UtcNow);

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => ledger.RecordBrokeredBuySettlementAsync(command));
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RecordBankSettlementAsync_PersistsBalancesAndJournal()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            await ledger.RecordBrokeredBuySettlementAsync(new FiatLedgerBrokeredBuyPostingCommand(
                "client-order-fiat-bank-prime",
                "customer-fiat-1",
                FiatCurrency.NorwegianKrone,
                1_200m,
                DateTimeOffset.UtcNow));

            var command = new FiatLedgerBankSettlementPostingCommand(
                "bank-ref-1",
                FiatCurrency.NorwegianKrone,
                1_000m,
                DateTimeOffset.UtcNow);

            var receipt = await ledger.RecordBankSettlementAsync(command);
            Assert.AreEqual("bank-ref-1", receipt.BankReferenceId);

            var dbOptions = new DbContextOptionsBuilder<FiatTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new FiatTransactionsDbContext(dbOptions);

            var clearingBalance = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.PlatformTradeClearing &&
                    position.AccountId == FiatLedgerAccountIds.Platform);
            Assert.AreEqual(200m, clearingBalance.AvailableAmount);

            var bankCashBalance = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.PlatformBankCash &&
                    position.AccountId == FiatLedgerAccountIds.Platform);
            Assert.AreEqual(1_000m, bankCashBalance.AvailableAmount);

            var bankTransaction = await context.FiatLedgerTransactions
                .AsNoTracking()
                .SingleAsync(candidate => candidate.OperationType == FiatLedgerOperationTypes.BankSettlement);
            var bankEntries = await context.FiatLedgerEntries
                .AsNoTracking()
                .Where(entry => entry.TransactionId == bankTransaction.Id)
                .OrderBy(entry => entry.Sequence)
                .ToArrayAsync();
            Assert.AreEqual(2, bankEntries.Length);
            Assert.AreEqual(FiatLedgerAccountKinds.PlatformTradeClearing, bankEntries[0].AccountKind);
            Assert.AreEqual(FiatLedgerEntryDirection.Decrease, bankEntries[0].Direction);
            Assert.AreEqual(FiatLedgerAccountKinds.PlatformBankCash, bankEntries[1].AccountKind);
            Assert.AreEqual(FiatLedgerEntryDirection.Increase, bankEntries[1].Direction);
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RecordBankSettlementAsync_WhenDuplicateRequestMatches_ReturnsOriginalWithoutNewJournal()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            await ledger.RecordBrokeredBuySettlementAsync(new FiatLedgerBrokeredBuyPostingCommand(
                "client-order-fiat-bank-prime-dup",
                "customer-fiat-2",
                FiatCurrency.NorwegianKrone,
                2_500m,
                DateTimeOffset.UtcNow));

            var command = new FiatLedgerBankSettlementPostingCommand(
                "bank-ref-2",
                FiatCurrency.NorwegianKrone,
                2_000m,
                DateTimeOffset.UtcNow);

            var first = await ledger.RecordBankSettlementAsync(command);
            var second = await ledger.RecordBankSettlementAsync(command);

            Assert.AreEqual(first.BankReferenceId, second.BankReferenceId);
            Assert.AreEqual(first.Amount, second.Amount);

            var dbOptions = new DbContextOptionsBuilder<FiatTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new FiatTransactionsDbContext(dbOptions);
            Assert.AreEqual(1, await context.FiatLedgerTransactions.CountAsync(candidate => candidate.OperationType == FiatLedgerOperationTypes.BankSettlement));
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task ReserveCaptureAndReleaseBrokeredBuyFundsAsync_MovesBalancesBetweenExpectedAccounts()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            var reserved = await ledger.ReserveBrokeredBuyFundsAsync(
                new FiatLedgerBrokeredBuyReservationCommand(
                    "client-order-fiat-reserve-1",
                    "customer-fiat-1",
                    FiatCurrency.NorwegianKrone,
                    1_100m,
                    DateTimeOffset.UtcNow));
            Assert.AreEqual(1_100m, reserved.ReservedAmount);

            await ledger.CaptureReservedBrokeredBuySettlementAsync(
                new FiatLedgerBrokeredBuyReservationCaptureCommand(
                    "client-order-fiat-reserve-1",
                    "customer-fiat-1",
                    FiatCurrency.NorwegianKrone,
                    1_100m,
                    DateTimeOffset.UtcNow));

            await ledger.ReserveBrokeredBuyFundsAsync(
                new FiatLedgerBrokeredBuyReservationCommand(
                    "client-order-fiat-reserve-2",
                    "customer-fiat-2",
                    FiatCurrency.NorwegianKrone,
                    900m,
                    DateTimeOffset.UtcNow));
            await ledger.ReleaseReservedBrokeredBuyFundsAsync(
                new FiatLedgerBrokeredBuyReservationReleaseCommand(
                    "client-order-fiat-reserve-2",
                    "customer-fiat-2",
                    FiatCurrency.NorwegianKrone,
                    900m,
                    DateTimeOffset.UtcNow));

            var dbOptions = new DbContextOptionsBuilder<FiatTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new FiatTransactionsDbContext(dbOptions);

            var customer1Available = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.CustomerAvailable &&
                    position.AccountId == "customer-fiat-1");
            Assert.AreEqual(48_900m, customer1Available.AvailableAmount);

            var customer1Reserved = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.CustomerReserved &&
                    position.AccountId == "customer-fiat-1");
            Assert.AreEqual(0m, customer1Reserved.AvailableAmount);

            var customer2Available = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.CustomerAvailable &&
                    position.AccountId == "customer-fiat-2");
            Assert.AreEqual(50_000m, customer2Available.AvailableAmount);

            var customer2Reserved = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.CustomerReserved &&
                    position.AccountId == "customer-fiat-2");
            Assert.AreEqual(0m, customer2Reserved.AvailableAmount);

            var clearing = await context.FiatBalancePositions
                .AsNoTracking()
                .SingleAsync(position =>
                    position.FiatCurrency == FiatCurrency.NorwegianKrone.Value &&
                    position.AccountKind == FiatLedgerAccountKinds.PlatformTradeClearing &&
                    position.AccountId == FiatLedgerAccountIds.Platform);
            Assert.AreEqual(1_100m, clearing.AvailableAmount);
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    private static EfCoreFiatLedger CreateLedger(out string databaseName, out string connectionString)
    {
        EnsurePostgresReachable();
        databaseName = $"{DatabasePrefix}{Guid.NewGuid():N}";
        connectionString = BuildConnectionString(databaseName);

        using var adminConnection = new NpgsqlConnection(BuildAdminConnectionString());
        adminConnection.Open();
        using var createDatabaseCommand = adminConnection.CreateCommand();
        createDatabaseCommand.CommandText = $"CREATE DATABASE \"{databaseName}\";";
        _ = createDatabaseCommand.ExecuteNonQuery();

        var dbOptions = new DbContextOptionsBuilder<FiatTransactionsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        using (var context = new FiatTransactionsDbContext(dbOptions))
        {
            context.Database.EnsureCreated();
            context.FiatBalancePositions.Add(new FiatBalancePositionEntity
            {
                FiatCurrency = FiatCurrency.NorwegianKrone.Value,
                AccountKind = FiatLedgerAccountKinds.CustomerAvailable,
                AccountId = "customer-fiat-1",
                AvailableAmount = 50_000m,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            context.FiatBalancePositions.Add(new FiatBalancePositionEntity
            {
                FiatCurrency = FiatCurrency.NorwegianKrone.Value,
                AccountKind = FiatLedgerAccountKinds.CustomerAvailable,
                AccountId = "customer-fiat-2",
                AvailableAmount = 50_000m,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            context.FiatBalancePositions.Add(new FiatBalancePositionEntity
            {
                FiatCurrency = FiatCurrency.NorwegianKrone.Value,
                AccountKind = FiatLedgerAccountKinds.PlatformTradeClearing,
                AccountId = FiatLedgerAccountIds.Platform,
                AvailableAmount = 0m,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            context.FiatBalancePositions.Add(new FiatBalancePositionEntity
            {
                FiatCurrency = FiatCurrency.NorwegianKrone.Value,
                AccountKind = FiatLedgerAccountKinds.PlatformBankCash,
                AccountId = FiatLedgerAccountIds.Platform,
                AvailableAmount = 0m,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            context.SaveChanges();
        }

        var factory = new StaticDbContextFactory(dbOptions);
        return new EfCoreFiatLedger(factory, TimeProvider.System);
    }

    private static void DropDatabaseIfExists(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        using var adminConnection = new NpgsqlConnection(BuildAdminConnectionString());
        adminConnection.Open();

        using var terminateSessionsCommand = adminConnection.CreateCommand();
        terminateSessionsCommand.CommandText = """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @databaseName
              AND pid <> pg_backend_pid();
            """;
        terminateSessionsCommand.Parameters.AddWithValue("@databaseName", databaseName);
        _ = terminateSessionsCommand.ExecuteNonQuery();

        using var dropDatabaseCommand = adminConnection.CreateCommand();
        dropDatabaseCommand.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\";";
        _ = dropDatabaseCommand.ExecuteNonQuery();
    }

    private static void EnsurePostgresReachable()
    {
        try
        {
            using var connection = new NpgsqlConnection(BuildAdminConnectionString());
            connection.Open();
        }
        catch (Exception exception)
        {
            throw new AssertInconclusiveException(
                "PostgreSQL test server is not reachable. Configure localhost:5432 with exchange credentials or set EXCHANGE_TEST_POSTGRES_CONNECTION_STRING.",
                exception);
        }
    }

    private static string BuildConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(BuildAdminConnectionString())
        {
            Database = databaseName,
            Pooling = false
        };
        return builder.ConnectionString;
    }

    private static string BuildAdminConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("EXCHANGE_TEST_POSTGRES_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = 5432,
            Username = "exchange",
            Password = "exchange",
            Database = "postgres",
            Pooling = false
        };
        return builder.ConnectionString;
    }

    private sealed class StaticDbContextFactory(DbContextOptions<FiatTransactionsDbContext> options)
        : IDbContextFactory<FiatTransactionsDbContext>
    {
        public FiatTransactionsDbContext CreateDbContext()
        {
            return new FiatTransactionsDbContext(options);
        }

        public Task<FiatTransactionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
