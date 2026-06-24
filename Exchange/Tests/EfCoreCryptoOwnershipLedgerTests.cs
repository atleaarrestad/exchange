using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Tests;

[TestClass]
public sealed class EfCoreCryptoOwnershipLedgerTests
{
    private const string DatabasePrefix = "exchange_ownership_ledger_tests_";

    [TestMethod]
    public async Task RecordCustomerBuyAsync_PersistsReceiptPositionsAndBalancedJournal()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            var command = new OwnershipLedgerBuyRecordCommand(
                "client-order-1",
                "customer-1",
                AssetSymbol.Bitcoin,
                QuoteCurrency.NorwegianKrone,
                2m,
                1m,
                1m,
                1_001_000m,
                2_002_000m,
                DateTimeOffset.UtcNow,
                null);

            var receipt = await ledger.RecordCustomerBuyAsync(command);
            var availableInventory = await ledger.GetAvailablePlatformInventoryAsync(AssetSymbol.Bitcoin);

            Assert.AreEqual("client-order-1", receipt.ClientOrderId);
            Assert.AreEqual(1m, availableInventory);

            var dbOptions = new DbContextOptionsBuilder<CryptoTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new CryptoTransactionsDbContext(dbOptions);
            var ownership = await context.CryptoOwnershipPositions
                .AsNoTracking()
                .SingleAsync(position => position.CustomerAccountId == "customer-1" && position.AssetSymbol == AssetSymbol.Bitcoin.Value);
            Assert.AreEqual(2m, ownership.Quantity);

            var transaction = await context.CryptoLedgerTransactions
                .AsNoTracking()
                .SingleAsync(candidate => candidate.OperationType == CryptoLedgerOperationTypes.BrokeredCryptoBuy);
            var entries = await context.CryptoLedgerEntries
                .AsNoTracking()
                .Where(entry => entry.TransactionId == transaction.Id)
                .OrderBy(entry => entry.Sequence)
                .ToArrayAsync();
            Assert.AreEqual(3, entries.Length);

            var debitTotal = entries
                .Where(entry => entry.Direction == CryptoLedgerEntryDirection.Debit)
                .Sum(entry => entry.Quantity);
            var creditTotal = entries
                .Where(entry => entry.Direction == CryptoLedgerEntryDirection.Credit)
                .Sum(entry => entry.Quantity);
            Assert.AreEqual(debitTotal, creditTotal);
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RecordCustomerBuyAsync_WhenDuplicateRequestMatches_ReturnsOriginalWithoutNewJournal()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            var command = new OwnershipLedgerBuyRecordCommand(
                "client-order-2",
                "customer-2",
                AssetSymbol.Bitcoin,
                QuoteCurrency.NorwegianKrone,
                1.5m,
                1m,
                0.5m,
                1_002_000m,
                1_503_000m,
                DateTimeOffset.UtcNow,
                null);

            var first = await ledger.RecordCustomerBuyAsync(command);
            var second = await ledger.RecordCustomerBuyAsync(command);

            Assert.AreEqual(first.ClientOrderId, second.ClientOrderId);
            Assert.AreEqual(first.TotalCost, second.TotalCost);

            var dbOptions = new DbContextOptionsBuilder<CryptoTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new CryptoTransactionsDbContext(dbOptions);
            Assert.AreEqual(1, await context.BrokeredCryptoBuyExecutions.CountAsync());
            Assert.AreEqual(1, await context.CryptoLedgerTransactions.CountAsync());
            Assert.AreEqual(3, await context.CryptoLedgerEntries.CountAsync());
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RecordCustomerBuyAsync_WhenDuplicateRequestDiffers_ThrowsConflict()
    {
        var ledger = CreateLedger(out var databaseName, out _);
        try
        {
            await ledger.RecordCustomerBuyAsync(new OwnershipLedgerBuyRecordCommand(
                "client-order-3",
                "customer-3",
                AssetSymbol.Bitcoin,
                QuoteCurrency.NorwegianKrone,
                1m,
                1m,
                0m,
                1_003_000m,
                1_003_000m,
                DateTimeOffset.UtcNow,
                null));

            await Assert.ThrowsExactlyAsync<IdempotencyKeyConflictException>(() =>
                ledger.RecordCustomerBuyAsync(new OwnershipLedgerBuyRecordCommand(
                    "client-order-3",
                    "customer-3",
                    AssetSymbol.Bitcoin,
                    QuoteCurrency.NorwegianKrone,
                    1.2m,
                    1m,
                    0.2m,
                    1_003_000m,
                    1_203_600m,
                    DateTimeOffset.UtcNow,
                    null)));
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RecordCustomerBuyAsync_ConcurrentLargeInternalFills_DoNotOversellInventory()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            var commandA = new OwnershipLedgerBuyRecordCommand(
                "client-order-concurrent-a",
                "customer-concurrent-a",
                AssetSymbol.Bitcoin,
                QuoteCurrency.NorwegianKrone,
                2m,
                2m,
                0m,
                1_010_000m,
                2_020_000m,
                DateTimeOffset.UtcNow,
                null);
            var commandB = new OwnershipLedgerBuyRecordCommand(
                "client-order-concurrent-b",
                "customer-concurrent-b",
                AssetSymbol.Bitcoin,
                QuoteCurrency.NorwegianKrone,
                2m,
                2m,
                0m,
                1_011_000m,
                2_022_000m,
                DateTimeOffset.UtcNow,
                null);

            var firstTask = ledger.RecordCustomerBuyAsync(commandA);
            var secondTask = ledger.RecordCustomerBuyAsync(commandB);

            var successful = 0;
            var insufficientFunds = 0;
            foreach (var result in await Task.WhenAll(Capture(firstTask), Capture(secondTask)))
            {
                if (result is BrokeredCryptoBuyReceipt)
                {
                    successful++;
                }
                else if (result is InsufficientFundsException)
                {
                    insufficientFunds++;
                }
                else if (result is Exception exception)
                {
                    Assert.Fail($"Unexpected exception type: {exception.GetType().Name} - {exception.Message}");
                }
            }

            Assert.AreEqual(1, successful);
            Assert.AreEqual(1, insufficientFunds);
            Assert.AreEqual(0m, await ledger.GetAvailablePlatformInventoryAsync(AssetSymbol.Bitcoin));

            var dbOptions = new DbContextOptionsBuilder<CryptoTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new CryptoTransactionsDbContext(dbOptions);
            Assert.AreEqual(1, await context.BrokeredCryptoBuyExecutions.CountAsync());
            var totalOwned = await context.CryptoOwnershipPositions
                .AsNoTracking()
                .Where(position => position.AssetSymbol == AssetSymbol.Bitcoin.Value)
                .SumAsync(position => position.Quantity);
            Assert.AreEqual(2m, totalOwned);
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RecordCustomerBuyAsync_ConcurrentSameCustomerBuys_AccumulatesOwnershipWithoutLoss()
    {
        var ledger = CreateLedger(out var databaseName, out var connectionString);
        try
        {
            var executionTasks = Enumerable.Range(1, 20)
                .Select(index => ledger.RecordCustomerBuyAsync(new OwnershipLedgerBuyRecordCommand(
                    $"client-order-burst-{index}",
                    "customer-burst",
                    AssetSymbol.Ether,
                    QuoteCurrency.NorwegianKrone,
                    1m,
                    0m,
                    1m,
                    25_000m,
                    25_000m,
                    DateTimeOffset.UtcNow,
                    null)))
                .ToArray();

            await Task.WhenAll(executionTasks);

            var dbOptions = new DbContextOptionsBuilder<CryptoTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new CryptoTransactionsDbContext(dbOptions);
            var ownership = await context.CryptoOwnershipPositions
                .AsNoTracking()
                .SingleAsync(position => position.CustomerAccountId == "customer-burst" && position.AssetSymbol == AssetSymbol.Ether.Value);

            Assert.AreEqual(20m, ownership.Quantity);
            Assert.AreEqual(20, await context.BrokeredCryptoBuyExecutions.CountAsync());
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    private static EfCoreCryptoOwnershipLedger CreateLedger(out string databaseName, out string connectionString)
    {
        EnsurePostgresReachable();
        databaseName = $"{DatabasePrefix}{Guid.NewGuid():N}";
        connectionString = BuildConnectionString(databaseName);

        using var adminConnection = new NpgsqlConnection(BuildAdminConnectionString());
        adminConnection.Open();
        using var createDatabaseCommand = adminConnection.CreateCommand();
        createDatabaseCommand.CommandText = $"CREATE DATABASE \"{databaseName}\";";
        _ = createDatabaseCommand.ExecuteNonQuery();

        var dbOptions = new DbContextOptionsBuilder<CryptoTransactionsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        using (var context = new CryptoTransactionsDbContext(dbOptions))
        {
            context.Database.EnsureCreated();
            context.PlatformInventoryPositions.AddRange(
                new PlatformInventoryPositionEntity
                {
                    AssetSymbol = AssetSymbol.Bitcoin.Value,
                    AvailableQuantity = 2m,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                },
                new PlatformInventoryPositionEntity
                {
                    AssetSymbol = AssetSymbol.Ether.Value,
                    AvailableQuantity = 10m,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            context.SaveChanges();
        }

        var factory = new StaticDbContextFactory(dbOptions);
        return new EfCoreCryptoOwnershipLedger(
            factory,
            TimeProvider.System);
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

    private static async Task<object?> Capture(Task<BrokeredCryptoBuyReceipt> task)
    {
        try
        {
            return await task;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed class StaticDbContextFactory(DbContextOptions<CryptoTransactionsDbContext> options)
        : IDbContextFactory<CryptoTransactionsDbContext>
    {
        public CryptoTransactionsDbContext CreateDbContext()
        {
            return new CryptoTransactionsDbContext(options);
        }

        public Task<CryptoTransactionsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
