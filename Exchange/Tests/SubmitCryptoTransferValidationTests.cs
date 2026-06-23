using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Application.Validation;

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
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, idempotencyStore, validator);
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
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, idempotencyStore, validator);
        var command = CreateValidCommand();

        var first = await service.SubmitAsync(command);
        var second = await service.SubmitAsync(command);

        Assert.AreEqual(1, gateway.CallCount);
        Assert.AreEqual(first.TransferId, second.TransferId);
        Assert.AreEqual(first.GatewayTransactionId, second.GatewayTransactionId);
        Assert.AreEqual(first.TotalDebit, second.TotalDebit);
    }

    [TestMethod]
    public async Task Service_WithSameIdempotencyKeyAcrossAssets_CallsGatewayPerAsset()
    {
        var gateway = new TrackingGateway();
        var validator = new SubmitCryptoTransferCommandValidator();
        var idempotencyStore = new InMemoryIdempotencyStore();
        var service = new CryptoTransferService(gateway, idempotencyStore, validator);

        var btcCommand = CreateValidCommand();
        var ethCommand = btcCommand with { AssetSymbol = CryptoAssetSymbols.Ether };

        await service.SubmitAsync(btcCommand);
        await service.SubmitAsync(ethCommand);

        Assert.AreEqual(2, gateway.CallCount);
    }

    [TestMethod]
    public void Validator_WithUnsupportedAsset_ThrowsValidationException()
    {
        var validator = new SubmitCryptoTransferCommandValidator();
        var command = CreateValidCommand() with { AssetSymbol = "USDT" };

        var exception = Assert.ThrowsExactly<ApplicationValidationException>(() => validator.Validate(command));

        Assert.IsTrue(exception.Errors.ContainsKey(nameof(SubmitCryptoTransferCommand.AssetSymbol)));
    }

    private static SubmitCryptoTransferCommand CreateValidCommand()
    {
        return new SubmitCryptoTransferCommand(
            "idem-123",
            "account-123",
            "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080",
            CryptoAssetSymbols.Bitcoin,
            0.5m,
            0.001m);
    }

    private sealed class TrackingGateway : IBlockchainTransferGateway
    {
        public bool WasCalled { get; private set; }
        public int CallCount { get; private set; }

        public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            CallCount++;
            return Task.FromResult(new BlockchainTransferResult("gateway-1", DateTimeOffset.UtcNow));
        }
    }

    private sealed class InMemoryIdempotencyStore : ICryptoTransferIdempotencyStore
    {
        private readonly Dictionary<(string SourceAccountId, string AssetSymbol, string IdempotencyKey), CryptoTransferReceipt> receipts = new();

        public Task<CryptoTransferReceipt> ExecuteOnceAsync(
            string sourceAccountId,
            string assetSymbol,
            string idempotencyKey,
            Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
            CancellationToken cancellationToken = default)
        {
            var key = (sourceAccountId.Trim(), assetSymbol.Trim().ToUpperInvariant(), idempotencyKey.Trim());
            if (receipts.TryGetValue(key, out var existingReceipt))
            {
                return Task.FromResult(existingReceipt);
            }

            return StoreAndReturnAsync(key, transferFactory, cancellationToken);
        }

        private async Task<CryptoTransferReceipt> StoreAndReturnAsync(
            (string SourceAccountId, string AssetSymbol, string IdempotencyKey) key,
            Func<CancellationToken, Task<CryptoTransferReceipt>> transferFactory,
            CancellationToken cancellationToken)
        {
            var receipt = await transferFactory(cancellationToken);
            receipts[key] = receipt;
            return receipt;
        }
    }
}
