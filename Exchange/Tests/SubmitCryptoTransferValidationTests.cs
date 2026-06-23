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
        var command = CreateValidCommand();

        validator.Validate(command);
    }

    [TestMethod]
    public void Validator_WithInvalidCommand_ThrowsValidationExceptionWithErrors()
    {
        var validator = new SubmitCryptoTransferCommandValidator();
        var command = new SubmitCryptoTransferCommand(
            "",
            "%%%",
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
    public async Task Service_WithInvalidCommand_DoesNotCallGateway()
    {
        var gateway = new TrackingGateway();
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, fundsReservationGateway, idempotencyStore, validator);
        var command = new SubmitCryptoTransferCommand(
            "",
            "account-1",
            "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080",
            "BTC",
            1m,
            0.1m);

        await Assert.ThrowsExactlyAsync<ApplicationValidationException>(() => service.SubmitAsync(command));
        Assert.IsFalse(gateway.WasCalled);
    }

    [TestMethod]
    public async Task Service_WithSameIdempotencyKey_CallsGatewayOnceAndReturnsSameReceipt()
    {
        var gateway = new TrackingGateway();
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, fundsReservationGateway, idempotencyStore, validator);
        var command = CreateValidCommand();

        var first = await service.SubmitAsync(command);
        var second = await service.SubmitAsync(command);

        Assert.AreEqual(1, gateway.CallCount);
        Assert.AreEqual(first.TransferId, second.TransferId);
        Assert.AreEqual(first.GatewayTransactionId, second.GatewayTransactionId);
        Assert.AreEqual(first.TotalDebit, second.TotalDebit);
        Assert.AreEqual(1, fundsReservationGateway.ReserveCallCount);
    }

    [TestMethod]
    public async Task Service_WithSameIdempotencyKeyAcrossAssets_CallsGatewayPerAsset()
    {
        var gateway = new TrackingGateway();
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, fundsReservationGateway, idempotencyStore, validator);

        var btcCommand = CreateValidCommand();
        var ethCommand = btcCommand with { AssetSymbol = AssetSymbol.Ether.Value };

        await service.SubmitAsync(btcCommand);
        await service.SubmitAsync(ethCommand);

        Assert.AreEqual(2, gateway.CallCount);
    }

    [TestMethod]
    public async Task Service_WithSameIdempotencyKeyAndDifferentAmount_ThrowsConflictException()
    {
        var gateway = new TrackingGateway();
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, fundsReservationGateway, idempotencyStore, validator);

        var firstCommand = CreateValidCommand();
        var secondCommand = firstCommand with { Amount = 0.75m };

        _ = await service.SubmitAsync(firstCommand);

        await Assert.ThrowsExactlyAsync<IdempotencyKeyConflictException>(() =>
            service.SubmitAsync(secondCommand));
    }

    [TestMethod]
    public void Validator_WithUnsupportedAsset_ThrowsValidationException()
    {
        var validator = new SubmitCryptoTransferCommandValidator();
        var command = CreateValidCommand() with { AssetSymbol = "USDT" };

        var exception = Assert.ThrowsExactly<ApplicationValidationException>(() => validator.Validate(command));

        Assert.IsTrue(exception.Errors.ContainsKey(nameof(SubmitCryptoTransferCommand.AssetSymbol)));
    }

    [TestMethod]
    public async Task Service_WithInsufficientFunds_DoesNotCallGateway()
    {
        var gateway = new TrackingGateway();
        var fundsReservationGateway = new TrackingFundsReservationGateway
        {
            ThrowInsufficientFunds = true
        };
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, fundsReservationGateway, idempotencyStore, validator);

        await Assert.ThrowsExactlyAsync<InsufficientFundsException>(() => service.SubmitAsync(CreateValidCommand()));

        Assert.IsFalse(gateway.WasCalled);
    }

    [TestMethod]
    public async Task Service_WithUnconfiguredGateway_ReleasesReservationAndThrows()
    {
        var gateway = new TrackingGateway
        {
            ThrowDependencyNotConfigured = true
        };
        var fundsReservationGateway = new TrackingFundsReservationGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, fundsReservationGateway, idempotencyStore, validator);

        await Assert.ThrowsExactlyAsync<ExternalDependencyNotConfiguredException>(() => service.SubmitAsync(CreateValidCommand()));

        Assert.AreEqual(1, fundsReservationGateway.ReserveCallCount);
        Assert.AreEqual(1, fundsReservationGateway.ReleaseCallCount);
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

    private sealed class TrackingGateway : IBlockchainTransferGateway
    {
        public bool ThrowDependencyNotConfigured { get; init; }
        public bool WasCalled { get; private set; }
        public int CallCount { get; private set; }

        public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            CallCount++;
            if (ThrowDependencyNotConfigured)
            {
                throw new ExternalDependencyNotConfiguredException("simulated missing gateway");
            }

            return Task.FromResult(new BlockchainTransferResult("gateway-1", DateTimeOffset.UtcNow));
        }

        public Task<BlockchainTransferStatus> GetTransferStatusAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new BlockchainTransferStatus(BlockchainTransferStatusKind.Unknown));
        }
    }

    private sealed class InMemoryIdempotencyStore : ICryptoTransferIdempotencyStore
    {
        private readonly Dictionary<(string SourceAccountId, AssetSymbol AssetSymbol, string IdempotencyKey), (string RequestFingerprint, CryptoTransferReceipt Receipt)> receipts = new();

        public Task<CryptoTransferReceipt> ExecuteOnceAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            string requestFingerprint,
            decimal totalDebit,
            Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
            CancellationToken cancellationToken = default)
        {
            var key = (
                SourceAccountId: sourceAccountId.Trim(),
                AssetSymbol: assetSymbol,
                IdempotencyKey: idempotencyKey.Trim());
            var normalizedRequestFingerprint = requestFingerprint.Trim();

            if (receipts.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.RequestFingerprint, normalizedRequestFingerprint, StringComparison.Ordinal))
                {
                    throw new IdempotencyKeyConflictException(
                        $"Idempotency key '{key.IdempotencyKey}' was already used with a different transfer request.");
                }

                return Task.FromResult(existing.Receipt);
            }

            return StoreAndReturnAsync(key, normalizedRequestFingerprint, transferFactory, cancellationToken);
        }

        public Task<IReadOnlyList<PendingCryptoTransferOperation>> GetPendingOlderThanAsync(
            DateTimeOffset olderThanUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<PendingCryptoTransferOperation>>(Array.Empty<PendingCryptoTransferOperation>());
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
            return Task.FromResult(false);
        }

        private async Task<CryptoTransferReceipt> StoreAndReturnAsync(
            (string SourceAccountId, AssetSymbol AssetSymbol, string IdempotencyKey) key,
            string requestFingerprint,
            Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
            CancellationToken cancellationToken)
        {
            var receipt = await transferFactory(cancellationToken);
            receipts[key] = (requestFingerprint, receipt);
            return receipt;
        }

    }

    private sealed class TrackingFundsReservationGateway : ICryptoTransferFundsReservationGateway
    {
        public bool ThrowInsufficientFunds { get; init; }
        public int ReserveCallCount { get; private set; }
        public int ReleaseCallCount { get; private set; }

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
            ReleaseCallCount++;
            return Task.CompletedTask;
        }
    }
}
