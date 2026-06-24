using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Tests;

[TestClass]
public sealed class EfCoreExternalHedgeSettlementServiceTests
{
    private const string DatabasePrefix = "exchange_external_hedge_settlement_tests_";

    [TestMethod]
    public async Task SettleAsync_PersistsExecutionRecordAndBalancesLedger()
    {
        var service = CreateService(out var databaseName, out var connectionString);
        try
        {
            var executedAtUtc = DateTimeOffset.UtcNow;
            await service.RegisterExecutionAsync(new ExternalHedgeExecutionObservation(
                "external-order-1",
                AssetSymbol.Bitcoin,
                QuoteCurrency.NorwegianKrone,
                0.75m,
                1_015_000m,
                executedAtUtc));

            await service.SettleAsync("external-order-1");

            var dbOptions = new DbContextOptionsBuilder<CryptoTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new CryptoTransactionsDbContext(dbOptions);
            var execution = await context.ExternalHedgeExecutionRecords
                .AsNoTracking()
                .SingleAsync(candidate => candidate.ExternalOrderId == "external-order-1");
            Assert.IsNotNull(execution.SettledAtUtc);
            Assert.IsNotNull(execution.SettlementLedgerTransactionId);

            var inventory = await context.PlatformInventoryPositions
                .AsNoTracking()
                .SingleAsync(candidate => candidate.AssetSymbol == AssetSymbol.Bitcoin.Value);
            Assert.AreEqual(2.75m, inventory.AvailableQuantity);

            var ledgerTransaction = await context.CryptoLedgerTransactions
                .AsNoTracking()
                .SingleAsync(candidate =>
                    candidate.OperationType == CryptoLedgerOperationTypes.ExternalHedgeSettlement &&
                    candidate.OperationId == "external-order-1");
            var entries = await context.CryptoLedgerEntries
                .AsNoTracking()
                .Where(candidate => candidate.TransactionId == ledgerTransaction.Id)
                .OrderBy(candidate => candidate.Sequence)
                .ToArrayAsync();
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(CryptoLedgerAccountKinds.PlatformExternalHedgePending, entries[0].AccountKind);
            Assert.AreEqual(CryptoLedgerEntryDirection.Debit, entries[0].Direction);
            Assert.AreEqual(0.75m, entries[0].Quantity);
            Assert.AreEqual(CryptoLedgerAccountKinds.PlatformInventory, entries[1].AccountKind);
            Assert.AreEqual(CryptoLedgerEntryDirection.Credit, entries[1].Direction);
            Assert.AreEqual(0.75m, entries[1].Quantity);
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task SettleAsync_WhenRepeated_IsIdempotent()
    {
        var service = CreateService(out var databaseName, out var connectionString);
        try
        {
            await service.RegisterExecutionAsync(new ExternalHedgeExecutionObservation(
                "external-order-2",
                AssetSymbol.Ether,
                QuoteCurrency.NorwegianKrone,
                1.2m,
                25_500m,
                DateTimeOffset.UtcNow));

            await service.SettleAsync("external-order-2");
            await service.SettleAsync("external-order-2");

            var dbOptions = new DbContextOptionsBuilder<CryptoTransactionsDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var context = new CryptoTransactionsDbContext(dbOptions);
            Assert.AreEqual(
                1,
                await context.CryptoLedgerTransactions.CountAsync(
                    candidate => candidate.OperationType == CryptoLedgerOperationTypes.ExternalHedgeSettlement &&
                                 candidate.OperationId == "external-order-2"));
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    [TestMethod]
    public async Task RegisterExecutionAsync_WhenDuplicateDiffers_ThrowsConflict()
    {
        var service = CreateService(out var databaseName, out _);
        try
        {
            await service.RegisterExecutionAsync(new ExternalHedgeExecutionObservation(
                "external-order-3",
                AssetSymbol.Bitcoin,
                QuoteCurrency.NorwegianKrone,
                0.5m,
                1_020_000m,
                DateTimeOffset.UtcNow));

            await Assert.ThrowsExactlyAsync<IdempotencyKeyConflictException>(() =>
                service.RegisterExecutionAsync(new ExternalHedgeExecutionObservation(
                    "external-order-3",
                    AssetSymbol.Bitcoin,
                    QuoteCurrency.NorwegianKrone,
                    0.6m,
                    1_020_000m,
                    DateTimeOffset.UtcNow)));
        }
        finally
        {
            DropDatabaseIfExists(databaseName);
        }
    }

    private static EfCoreExternalHedgeSettlementService CreateService(out string databaseName, out string connectionString)
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
        return new EfCoreExternalHedgeSettlementService(factory, TimeProvider.System);
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
