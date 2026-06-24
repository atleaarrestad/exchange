using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Resilience.Gateways;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.Bulkhead;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Tests;

[TestClass]
public sealed class ResilientBlockchainTransferGatewayTests
{
    [TestMethod]
    public async Task SubmitAsync_TransientFailures_OpenCircuitAndFailFast()
    {
        var callCount = 0;
        var gateway = CreateResilientGateway(
            submitAsync: (_, _) =>
            {
                callCount++;
                throw new HttpRequestException("network failure");
            },
            options: new BlockchainGatewayResiliencePolicyOptions
            {
                Enabled = true,
                OperationTimeout = TimeSpan.FromSeconds(5),
                FailureRatio = 0.5,
                MinimumThroughput = 2,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(60),
                MaxParallelization = 8,
                MaxQueueingActions = 8
            });

        var request = CreateRequest("idem-circuit");

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => gateway.SubmitAsync(request));
        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => gateway.SubmitAsync(request));
        await Assert.ThrowsExactlyAsync<BrokenCircuitException>(() => gateway.SubmitAsync(request));
        Assert.AreEqual(2, callCount);
    }

    [TestMethod]
    public async Task SubmitAsync_WhenBulkheadSaturated_RejectsAdditionalExecution()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = CreateResilientGateway(
            submitAsync: async (_, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
                return new BlockchainTransferResult("tx-1", DateTimeOffset.UtcNow, 3);
            },
            options: new BlockchainGatewayResiliencePolicyOptions
            {
                Enabled = true,
                OperationTimeout = TimeSpan.FromSeconds(30),
                FailureRatio = 0.5,
                MinimumThroughput = 100,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                MaxParallelization = 1,
                MaxQueueingActions = 0
            });

        var first = gateway.SubmitAsync(CreateRequest("idem-bulkhead-1"));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.ThrowsExactlyAsync<BulkheadRejectedException>(() => gateway.SubmitAsync(CreateRequest("idem-bulkhead-2")));
        release.TrySetResult();
        await first;
    }

    [TestMethod]
    public async Task SubmitAsync_WhenOperationTimesOut_ThrowsTimeoutRejectedException()
    {
        var gateway = CreateResilientGateway(
            submitAsync: async (_, cancellationToken) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                return new BlockchainTransferResult("tx-timeout", DateTimeOffset.UtcNow, 3);
            },
            options: new BlockchainGatewayResiliencePolicyOptions
            {
                Enabled = true,
                OperationTimeout = TimeSpan.FromSeconds(1),
                FailureRatio = 0.5,
                MinimumThroughput = 20,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                MaxParallelization = 8,
                MaxQueueingActions = 8
            });

        await Assert.ThrowsExactlyAsync<TimeoutRejectedException>(() => gateway.SubmitAsync(CreateRequest("idem-timeout")));
    }

    [TestMethod]
    public async Task SubmitAsync_WhenTransientGatewayRejection_OpenCircuitAndFailFast()
    {
        var callCount = 0;
        var gateway = CreateResilientGateway(
            submitAsync: (_, _) =>
            {
                callCount++;
                throw new BlockchainTransferRejectedException("transient upstream error", isTransient: true);
            },
            options: new BlockchainGatewayResiliencePolicyOptions
            {
                Enabled = true,
                OperationTimeout = TimeSpan.FromSeconds(5),
                FailureRatio = 0.5,
                MinimumThroughput = 2,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(60),
                MaxParallelization = 8,
                MaxQueueingActions = 8
            });

        var request = CreateRequest("idem-transient-rejection");

        await Assert.ThrowsExactlyAsync<BlockchainTransferRejectedException>(() => gateway.SubmitAsync(request));
        await Assert.ThrowsExactlyAsync<BlockchainTransferRejectedException>(() => gateway.SubmitAsync(request));
        await Assert.ThrowsExactlyAsync<BrokenCircuitException>(() => gateway.SubmitAsync(request));
        Assert.AreEqual(2, callCount);
    }

    private static ResilientBlockchainTransferGateway CreateResilientGateway(
        Func<BlockchainTransferRequest, CancellationToken, Task<BlockchainTransferResult>> submitAsync,
        BlockchainGatewayResiliencePolicyOptions options)
    {
        return new ResilientBlockchainTransferGateway(
            new StubBlockchainTransferGateway(submitAsync),
            options,
            NullLogger<ResilientBlockchainTransferGateway>.Instance);
    }

    private static BlockchainTransferRequest CreateRequest(string idempotencyKey)
    {
        return new BlockchainTransferRequest(
            idempotencyKey,
            "account-1",
            "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080",
            AssetSymbol.Bitcoin,
            0.25m,
            0.001m,
            0.251m);
    }

    private sealed class StubBlockchainTransferGateway(
        Func<BlockchainTransferRequest, CancellationToken, Task<BlockchainTransferResult>> submitAsync) : IBlockchainTransferGateway
    {
        public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
        {
            return submitAsync(request, cancellationToken);
        }

        public Task<BlockchainTransferStatus> GetTransferStatusAsync(
            string sourceAccountId,
            AssetSymbol assetSymbol,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BlockchainTransferStatus(BlockchainTransferStatusKind.NotSubmitted));
        }
    }
}
