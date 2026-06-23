using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Infrastructure.Gateways;

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
                    throw new InvalidOperationException("simulated failure");
                }

                await Task.Yield();
                return new CryptoTransferReceipt(
                    Guid.CreateVersion7(),
                    "gateway-2",
                    DateTimeOffset.UtcNow,
                    1.2m,
                    12);
            }

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
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
}
