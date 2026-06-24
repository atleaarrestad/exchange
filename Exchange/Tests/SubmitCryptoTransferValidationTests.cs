using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Tests;

[TestClass]
public sealed class SubmitCryptoTransferValidationTests
{
    [TestMethod]
    public void Validator_WithValidCommand_DoesNotThrow()
    {
        var validator = new SubmitCryptoTransferCommandValidator();
        validator.Validate(CreateValidCommand());
    }

    [TestMethod]
    public void Validator_WithInvalidCommand_ThrowsValidationExceptionWithErrors()
    {
        var validator = new SubmitCryptoTransferCommandValidator();
        var command = new SubmitCryptoTransferCommand(
            "",
            "%%%invalid-account",
            "abc",
            "DOGE",
            -1m,
            20m);

        var exception = Assert.ThrowsExactly<ApplicationValidationException>(() => validator.Validate(command));

        Assert.IsTrue(exception.Errors.ContainsKey(nameof(SubmitCryptoTransferCommand.IdempotencyKey)));
        Assert.IsTrue(exception.Errors.ContainsKey(nameof(SubmitCryptoTransferCommand.SourceAccountId)));
        Assert.IsTrue(exception.Errors.ContainsKey(nameof(SubmitCryptoTransferCommand.DestinationAddress)));
        Assert.IsTrue(exception.Errors.ContainsKey(nameof(SubmitCryptoTransferCommand.AssetSymbol)));
        Assert.IsTrue(exception.Errors.ContainsKey(nameof(SubmitCryptoTransferCommand.Amount)));
        Assert.IsTrue(exception.Errors.ContainsKey(nameof(SubmitCryptoTransferCommand.NetworkFee)));
    }

    [TestMethod]
    public async Task Service_WithSameIdempotencyKey_ReturnsPendingReceiptAndReservesOnce()
    {
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(fundsReservationGateway, idempotencyStore, validator);
        var command = CreateValidCommand();

        var first = await service.SubmitAsync(command);
        var second = await service.SubmitAsync(command);

        Assert.AreEqual(CryptoTransferReceiptStatus.Pending, first.Status);
        Assert.AreEqual(CryptoTransferReceiptStatus.Pending, second.Status);
        Assert.AreEqual(first.TransferId, second.TransferId);
        Assert.AreEqual(1, fundsReservationGateway.ReserveCallCount);
    }

    [TestMethod]
    public async Task Service_WithSameIdempotencyKeyAcrossAssets_ReservesPerAsset()
    {
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(fundsReservationGateway, idempotencyStore, validator);

        await service.SubmitAsync(CreateValidCommand());
        await service.SubmitAsync(CreateValidCommand() with { AssetSymbol = AssetSymbol.Ether.Value });

        Assert.AreEqual(2, fundsReservationGateway.ReserveCallCount);
    }

    [TestMethod]
    public async Task Service_WithSameIdempotencyKeyAndDifferentAmount_ThrowsConflictException()
    {
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(fundsReservationGateway, idempotencyStore, validator);

        _ = await service.SubmitAsync(CreateValidCommand());

        await Assert.ThrowsExactlyAsync<IdempotencyKeyConflictException>(() =>
            service.SubmitAsync(CreateValidCommand() with { Amount = 0.75m }));
    }

    [TestMethod]
    public async Task Service_WithInsufficientFunds_ReleasesPendingRecordAndThrows()
    {
        var fundsReservationGateway = new TrackingFundsReservationGateway
        {
            ThrowInsufficientFunds = true
        };
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(fundsReservationGateway, idempotencyStore, validator);

        await Assert.ThrowsExactlyAsync<InsufficientFundsException>(() => service.SubmitAsync(CreateValidCommand()));

        Assert.AreEqual(1, fundsReservationGateway.ReserveCallCount);
        Assert.AreEqual(1, idempotencyStore.ReleaseCallCount);
    }

    [TestMethod]
    public async Task Service_WithUnsupportedAsset_ThrowsValidationException()
    {
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(fundsReservationGateway, idempotencyStore, validator);

        await Assert.ThrowsExactlyAsync<ApplicationValidationException>(() =>
            service.SubmitAsync(CreateValidCommand() with { AssetSymbol = "USDT" }));
    }

    private static SubmitCryptoTransferCommand CreateValidCommand()
    {
        return new SubmitCryptoTransferCommand(
            "idem-123",
            "account-123",
            "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080",
            AssetSymbol.Bitcoin.Value,
            0.5m,
            0.001m);
    }

    private sealed class InMemoryIdempotencyStore : ICryptoTransferIdempotencyStore
    {
        private readonly Dictionary<(string SourceAccountId, AssetSymbol AssetSymbol, string IdempotencyKey), (string RequestFingerprint, bool Completed, CryptoTransferReceipt? Receipt)> entries = new();
        public int ReleaseCallCount { get; private set; }

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
            var key = (sourceAccountId.Trim(), assetSymbol, idempotencyKey.Trim());
            var normalizedFingerprint = requestFingerprint.Trim();

            if (entries.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.RequestFingerprint, normalizedFingerprint, StringComparison.Ordinal))
                {
                    throw new IdempotencyKeyConflictException(
                        $"Idempotency key '{key.Item3}' was already used with a different transfer request.");
                }

                return Task.FromResult(new CryptoTransferIdempotencyRegistration(
                    CreatedPending: false,
                    CompletedReceipt: existing.Completed ? existing.Receipt : null));
            }

            entries[key] = (normalizedFingerprint, Completed: false, Receipt: null);
            return Task.FromResult(new CryptoTransferIdempotencyRegistration(CreatedPending: true, CompletedReceipt: null));
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
            return Task.FromResult<IReadOnlyList<PendingCryptoTransferOperation>>([]);
        }

        public Task<bool> TryMarkCompletedAsync(
            PendingCryptoTransferOperation operation,
            CryptoTransferReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        public Task<bool> TryAcquirePendingAsync(
            PendingCryptoTransferOperation operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }

        public Task<bool> TryReleasePendingAsync(
            PendingCryptoTransferOperation operation,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseCallCount++;
            var key = (operation.SourceAccountId.Trim(), operation.AssetSymbol, operation.IdempotencyKey.Trim());
            return Task.FromResult(entries.Remove(key));
        }
    }

    private sealed class TrackingFundsReservationGateway : ICryptoTransferFundsReservationGateway
    {
        public bool ThrowInsufficientFunds { get; init; }
        public int ReserveCallCount { get; private set; }

        public Task ReserveAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            decimal totalDebit,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReserveCallCount++;
            if (ThrowInsufficientFunds)
            {
                throw new InsufficientFundsException("simulated insufficient funds");
            }

            return Task.CompletedTask;
        }

        public Task CommitAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
