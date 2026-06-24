using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Tests;

[TestClass]
public sealed class CryptoTransferTimeoutReconcilerTests
{
    [TestMethod]
    public async Task ReconcileAsync_WhenGatewayShowsSubmitted_MarksCompletedAndCommitsReservation()
    {
        var operation = new PendingCryptoTransferOperation(
            "account-1",
            AssetSymbol.Bitcoin,
            "idem-1",
            "fingerprint-1",
            1.25m,
            "bc1destination",
            1.2m,
            0.05m,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-10));
        var idempotencyStore = new TrackingIdempotencyStore(operation);
        var gateway = new TrackingGateway(new BlockchainTransferStatus(
            BlockchainTransferStatusKind.Submitted,
            "gateway-tx-1",
            DateTimeOffset.UtcNow.AddMinutes(-9),
            3));
        var fundsGateway = new TrackingFundsGateway();
        var reconciler = new CryptoTransferTimeoutReconciler(gateway, fundsGateway, idempotencyStore);

        await reconciler.ReconcileAsync(DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.AreEqual(1, idempotencyStore.AcquirePendingCount);
        Assert.AreEqual(1, idempotencyStore.MarkCompletedCount);
        Assert.AreEqual(1, fundsGateway.CommitCount);
        Assert.AreEqual(0, fundsGateway.ReleaseCount);
    }

    [TestMethod]
    public async Task ReconcileAsync_WhenGatewayShowsNotSubmitted_ReleasesReservation()
    {
        var operation = new PendingCryptoTransferOperation(
            "account-1",
            AssetSymbol.Ether,
            "idem-2",
            "fingerprint-2",
            0.25m,
            "0xabc",
            0.2m,
            0.05m,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-10));
        var idempotencyStore = new TrackingIdempotencyStore(operation);
        var gateway = new TrackingGateway(new BlockchainTransferStatus(BlockchainTransferStatusKind.NotSubmitted));
        var fundsGateway = new TrackingFundsGateway();
        var reconciler = new CryptoTransferTimeoutReconciler(gateway, fundsGateway, idempotencyStore);

        await reconciler.ReconcileAsync(DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.AreEqual(1, idempotencyStore.AcquirePendingCount);
        Assert.AreEqual(0, idempotencyStore.MarkCompletedCount);
        Assert.AreEqual(1, idempotencyStore.ReleasePendingCount);
        Assert.AreEqual(0, fundsGateway.CommitCount);
        Assert.AreEqual(1, fundsGateway.ReleaseCount);
    }

    [TestMethod]
    public async Task ReconcileAsync_WhenGatewayShowsNotSubmittedInsideSafetyWindow_DoesNotReleaseReservation()
    {
        var operation = new PendingCryptoTransferOperation(
            "account-1",
            AssetSymbol.Ether,
            "idem-young",
            "fingerprint-young",
            0.25m,
            "0xabc",
            0.2m,
            0.05m,
            DateTimeOffset.UtcNow.AddSeconds(-30),
            DateTimeOffset.UtcNow.AddSeconds(-30));
        var idempotencyStore = new TrackingIdempotencyStore(operation);
        var gateway = new TrackingGateway(new BlockchainTransferStatus(BlockchainTransferStatusKind.NotSubmitted));
        var fundsGateway = new TrackingFundsGateway();
        var reconciler = new CryptoTransferTimeoutReconciler(gateway, fundsGateway, idempotencyStore);

        await reconciler.ReconcileAsync(DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.AreEqual(1, idempotencyStore.AcquirePendingCount);
        Assert.AreEqual(0, idempotencyStore.MarkCompletedCount);
        Assert.AreEqual(0, idempotencyStore.ReleasePendingCount);
        Assert.AreEqual(0, fundsGateway.CommitCount);
        Assert.AreEqual(0, fundsGateway.ReleaseCount);
    }

    [TestMethod]
    public async Task ReconcileAsync_WhenGatewayStatusUnknown_ThrowsSpecificExceptionAndLeavesPendingUntouched()
    {
        var operation = new PendingCryptoTransferOperation(
            "account-1",
            AssetSymbol.Ether,
            "idem-3",
            "fingerprint-3",
            0.75m,
            "0xabc",
            0.7m,
            0.05m,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-10));
        var idempotencyStore = new TrackingIdempotencyStore(operation);
        var gateway = new TrackingGateway(new BlockchainTransferStatus(BlockchainTransferStatusKind.Unknown));
        var fundsGateway = new TrackingFundsGateway();
        var reconciler = new CryptoTransferTimeoutReconciler(gateway, fundsGateway, idempotencyStore);

        await Assert.ThrowsExactlyAsync<UnknownBlockchainTransferStatusException>(() =>
            reconciler.ReconcileAsync(DateTimeOffset.UtcNow.AddMinutes(-1)));

        Assert.AreEqual(1, idempotencyStore.AcquirePendingCount);
        Assert.AreEqual(0, idempotencyStore.MarkCompletedCount);
        Assert.AreEqual(0, idempotencyStore.ReleasePendingCount);
        Assert.AreEqual(0, fundsGateway.CommitCount);
        Assert.AreEqual(0, fundsGateway.ReleaseCount);
    }

    [TestMethod]
    public async Task ReconcileAsync_WhenPendingCannotBeAcquired_SkipsSideEffects()
    {
        var operation = new PendingCryptoTransferOperation(
            "account-1",
            AssetSymbol.Bitcoin,
            "idem-4",
            "fingerprint-4",
            0.33m,
            "bc1destination",
            0.3m,
            0.03m,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-10));
        var idempotencyStore = new TrackingIdempotencyStore(operation)
        {
            TryAcquireResult = false
        };
        var gateway = new TrackingGateway(new BlockchainTransferStatus(
            BlockchainTransferStatusKind.Submitted,
            "gateway-tx-4",
            DateTimeOffset.UtcNow.AddMinutes(-9),
            2));
        var fundsGateway = new TrackingFundsGateway();
        var reconciler = new CryptoTransferTimeoutReconciler(gateway, fundsGateway, idempotencyStore);

        await reconciler.ReconcileAsync(DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.AreEqual(1, idempotencyStore.AcquirePendingCount);
        Assert.AreEqual(0, idempotencyStore.MarkCompletedCount);
        Assert.AreEqual(0, idempotencyStore.ReleasePendingCount);
        Assert.AreEqual(0, fundsGateway.CommitCount);
        Assert.AreEqual(0, fundsGateway.ReleaseCount);
    }

    private sealed class TrackingGateway(BlockchainTransferStatus status) : IBlockchainTransferGateway
    {
        public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new BlockchainTransferResult("unused", DateTimeOffset.UtcNow));
        }

        public Task<BlockchainTransferStatus> GetTransferStatusAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(status);
        }
    }

    private sealed class TrackingIdempotencyStore(PendingCryptoTransferOperation operation) : ICryptoTransferIdempotencyStore
    {
        public bool TryAcquireResult { get; init; } = true;
        public int AcquirePendingCount { get; private set; }
        public int MarkCompletedCount { get; private set; }
        public int ReleasePendingCount { get; private set; }

        public Task<CryptoTransferIdempotencyRegistration> RegisterPendingAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            string requestFingerprint,
            decimal totalDebit,
            string destinationAddress,
            decimal amount,
            decimal networkFee,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CryptoTransferIdempotencyRegistration(true, null));
        }

        public Task<CryptoTransferReceipt> ExecuteOnceAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            string requestFingerprint,
            decimal totalDebit,
            Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return transferFactory(cancellationToken);
        }

        public Task<IReadOnlyList<PendingCryptoTransferOperation>> GetPendingOlderThanAsync(
            DateTimeOffset olderThanUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<PendingCryptoTransferOperation>>(new[] { operation });
        }

        public Task<bool> TryMarkCompletedAsync(
            PendingCryptoTransferOperation pendingOperation,
            CryptoTransferReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MarkCompletedCount++;
            return Task.FromResult(true);
        }

        public Task<bool> TryAcquirePendingAsync(
            PendingCryptoTransferOperation pendingOperation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AcquirePendingCount++;
            return Task.FromResult(TryAcquireResult);
        }

        public Task<bool> TryReleasePendingAsync(
            PendingCryptoTransferOperation pendingOperation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleasePendingCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class TrackingFundsGateway : ICryptoTransferFundsReservationGateway
    {
        public int CommitCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public Task ReserveAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            decimal totalDebit,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task CommitAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseCount++;
            return Task.CompletedTask;
        }
    }
}
