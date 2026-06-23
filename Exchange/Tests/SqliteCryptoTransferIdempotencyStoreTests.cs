using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Gateways;
using Microsoft.Data.Sqlite;

namespace Tests;

[TestClass]
public sealed class SqliteCryptoTransferIdempotencyStoreTests
{
    [TestMethod]
    public async Task ExecuteOnceAsync_WithSameKey_ReturnsStoredReceipt()
    {
        var store = CreateStore(out var dbPath);
        try
        {
            var callCount = 0;

            Task<CryptoTransferReceipt> Factory(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                callCount++;
                return Task.FromResult(new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    "gateway-1",
                    DateTimeOffset.UtcNow,
                    1.001m,
                    3));
            }

            var first = await store.ExecuteOnceAsync("account-1", AssetSymbol.Bitcoin, "idem-1", "req-1", Factory);
            var second = await store.ExecuteOnceAsync("account-1", AssetSymbol.Bitcoin, "idem-1", "req-1", Factory);

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(first.TransferId, second.TransferId);
            Assert.AreEqual(first.GatewayTransactionId, second.GatewayTransactionId);
            Assert.AreEqual(first.TotalDebit, second.TotalDebit);
        }
        finally
        {
            DeleteFileIfExists(dbPath);
        }
    }

    [TestMethod]
    public async Task ExecuteOnceAsync_WhenFactoryFails_DoesNotPersistFailedResult()
    {
        var store = CreateStore(out var dbPath);
        try
        {
            var callCount = 0;

            async Task<CryptoTransferReceipt> Factory(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                callCount++;
                if (callCount == 1)
                {
                    throw new BlockchainTransferRejectedException("simulated rejection");
                }

                await Task.Yield();
                return new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    "gateway-2",
                    DateTimeOffset.UtcNow,
                    1.2m,
                    12);
            }

            await Assert.ThrowsExactlyAsync<BlockchainTransferRejectedException>(() =>
                store.ExecuteOnceAsync("account-1", AssetSymbol.Ether, "idem-2", "req-2", Factory));

            var retry = await store.ExecuteOnceAsync("account-1", AssetSymbol.Ether, "idem-2", "req-2", Factory);

            Assert.AreEqual(2, callCount);
            Assert.AreEqual("gateway-2", retry.GatewayTransactionId);
        }
        finally
        {
            DeleteFileIfExists(dbPath);
        }
    }

    [TestMethod]
    public async Task ExecuteOnceAsync_WhenFundsReservationFails_DoesNotPersistFailedResult()
    {
        var store = CreateStore(out var dbPath);
        try
        {
            var callCount = 0;

            async Task<CryptoTransferReceipt> Factory(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                callCount++;
                if (callCount == 1)
                {
                    throw new InsufficientFundsException("simulated insufficient funds");
                }

                await Task.Yield();
                return new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    "gateway-after-funds",
                    DateTimeOffset.UtcNow,
                    0.8m,
                    6);
            }

            await Assert.ThrowsExactlyAsync<InsufficientFundsException>(() =>
                store.ExecuteOnceAsync("account-funds", AssetSymbol.Ether, "idem-funds", "req-funds", Factory));

            var retry = await store.ExecuteOnceAsync("account-funds", AssetSymbol.Ether, "idem-funds", "req-funds", Factory);

            Assert.AreEqual(2, callCount);
            Assert.AreEqual("gateway-after-funds", retry.GatewayTransactionId);
        }
        finally
        {
            DeleteFileIfExists(dbPath);
        }
    }

    [TestMethod]
    public async Task ExecuteOnceAsync_ConcurrentStoresWithSameKey_ExecuteFactoryOnce()
    {
        var storeA = CreateStore(out var dbPath);
        var storeB = new SqliteCryptoTransferIdempotencyStore($"Data Source={dbPath};Pooling=False");

        try
        {
            var callCount = 0;

            async Task<CryptoTransferReceipt> Factory(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref callCount);
                await Task.Delay(120, cancellationToken);
                return new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    "gateway-concurrent",
                    DateTimeOffset.UtcNow,
                    0.25m,
                    2);
            }

            var firstTask = storeA.ExecuteOnceAsync("account-2", AssetSymbol.Bitcoin, "idem-concurrent", "req-concurrent", Factory);
            var secondTask = storeB.ExecuteOnceAsync("account-2", AssetSymbol.Bitcoin, "idem-concurrent", "req-concurrent", Factory);

            await Task.WhenAll(firstTask, secondTask);

            var first = await firstTask;
            var second = await secondTask;

            Assert.AreEqual(1, callCount);
            Assert.AreEqual(first.TransferId, second.TransferId);
            Assert.AreEqual(first.GatewayTransactionId, second.GatewayTransactionId);
        }
        finally
        {
            DeleteFileIfExists(dbPath);
        }
    }

    [TestMethod]
    public async Task ExecuteOnceAsync_WithSameKeyAndDifferentRequestFingerprint_ThrowsConflictException()
    {
        var store = CreateStore(out var dbPath);
        try
        {
            Task<CryptoTransferReceipt> Factory(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    "gateway-conflict",
                    DateTimeOffset.UtcNow,
                    1.001m,
                    3));
            }

            _ = await store.ExecuteOnceAsync("account-1", AssetSymbol.Bitcoin, "idem-conflict", "req-a", Factory);

            await Assert.ThrowsExactlyAsync<IdempotencyKeyConflictException>(() =>
                store.ExecuteOnceAsync("account-1", AssetSymbol.Bitcoin, "idem-conflict", "req-b", Factory));
        }
        finally
        {
            DeleteFileIfExists(dbPath);
        }
    }

    [TestMethod]
    public async Task ExecuteOnceAsync_WhenTimedOutTransferBecomesStale_ThrowsPendingExceptionAndPreventsReplay()
    {
        var store = CreateStore(out var dbPath);
        const string sourceAccountId = "account-3";
        const string idempotencyKey = "idem-timeout";
        const string requestFingerprint = "req-timeout";

        try
        {
            Task<CryptoTransferReceipt> TimeoutFactory(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new BlockchainTransferTimeoutException("simulated timeout");
            }

            await Assert.ThrowsExactlyAsync<BlockchainTransferTimeoutException>(() =>
                store.ExecuteOnceAsync(sourceAccountId, AssetSymbol.Bitcoin, idempotencyKey, requestFingerprint, TimeoutFactory));

            MakePendingRecordStale(dbPath, sourceAccountId, AssetSymbol.Bitcoin.Value, idempotencyKey);

            Task<CryptoTransferReceipt> SuccessFactory(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    "gateway-replay",
                    DateTimeOffset.UtcNow,
                    0.5m,
                    2));
            }

            await Assert.ThrowsExactlyAsync<IdempotencyOperationPendingException>(() =>
                store.ExecuteOnceAsync(sourceAccountId, AssetSymbol.Bitcoin, idempotencyKey, requestFingerprint, SuccessFactory));
        }
        finally
        {
            DeleteFileIfExists(dbPath);
        }
    }

    private static SqliteCryptoTransferIdempotencyStore CreateStore(out string dbPath)
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"crypto-idempotency-{Guid.NewGuid():N}.db");
        return new SqliteCryptoTransferIdempotencyStore($"Data Source={dbPath};Pooling=False");
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void MakePendingRecordStale(string dbPath, string sourceAccountId, string assetSymbol, string idempotencyKey)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE crypto_transfer_idempotency_receipts
            SET created_at_utc = $createdAtUtc
            WHERE source_account_id = $sourceAccountId
              AND asset_symbol = $assetSymbol
              AND idempotency_key = $idempotencyKey
              AND status = 0;
            """;
        command.Parameters.AddWithValue("$createdAtUtc", DateTimeOffset.UtcNow.AddMinutes(-10));
        command.Parameters.AddWithValue("$sourceAccountId", sourceAccountId);
        command.Parameters.AddWithValue("$assetSymbol", assetSymbol);
        command.Parameters.AddWithValue("$idempotencyKey", idempotencyKey);
        _ = command.ExecuteNonQuery();
    }
}
