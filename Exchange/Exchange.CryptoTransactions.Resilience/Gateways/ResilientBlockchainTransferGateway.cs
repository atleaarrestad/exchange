using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Bulkhead;
using Polly.Timeout;
using Polly.Wrap;

namespace Exchange.CryptoTransactions.Resilience.Gateways;

public sealed class ResilientBlockchainTransferGateway(
    IBlockchainTransferGateway innerGateway,
    BlockchainGatewayResiliencePolicyOptions options,
    ILogger<ResilientBlockchainTransferGateway> logger) : IBlockchainTransferGateway
{
    private readonly IBlockchainTransferGateway innerGateway = innerGateway ?? throw new ArgumentNullException(nameof(innerGateway));
    private readonly AsyncPolicyWrap<BlockchainTransferResult> submitPolicy = CreatePolicyWrap<BlockchainTransferResult>(options, logger);
    private readonly AsyncPolicyWrap<BlockchainTransferStatus> statusPolicy = CreatePolicyWrap<BlockchainTransferStatus>(options, logger);

    public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return submitPolicy.ExecuteAsync(
            token => innerGateway.SubmitAsync(request, token),
            cancellationToken);
    }

    public Task<BlockchainTransferStatus> GetTransferStatusAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return statusPolicy.ExecuteAsync(
            token => innerGateway.GetTransferStatusAsync(sourceAccountId, assetSymbol, idempotencyKey, token),
            cancellationToken);
    }

    private static AsyncPolicyWrap<T> CreatePolicyWrap<T>(
        BlockchainGatewayResiliencePolicyOptions options,
        ILogger logger)
    {
        if (!options.Enabled)
        {
            return Policy.WrapAsync(Policy.NoOpAsync<T>());
        }

        var timeoutPolicy = Policy.TimeoutAsync<T>(
            options.OperationTimeout,
            TimeoutStrategy.Pessimistic,
            static (context, timeout, _, _) => Task.CompletedTask);

        var circuitBreakerPolicy = Policy<T>
            .Handle<TimeoutRejectedException>()
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<IOException>()
            .Or<BlockchainTransferRejectedException>(exception => exception.IsTransient)
            .AdvancedCircuitBreakerAsync(
                failureThreshold: options.FailureRatio,
                samplingDuration: options.SamplingDuration,
                minimumThroughput: options.MinimumThroughput,
                durationOfBreak: options.BreakDuration,
                onBreak: (outcome, duration) =>
                {
                    logger.LogWarning(
                        outcome.Exception,
                        "Blockchain gateway circuit opened for {BreakDurationSeconds} second(s).",
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    logger.LogInformation("Blockchain gateway circuit reset to closed.");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Blockchain gateway circuit moved to half-open.");
                });

        var bulkheadPolicy = Policy.BulkheadAsync<T>(
            maxParallelization: options.MaxParallelization,
            maxQueuingActions: options.MaxQueueingActions,
            onBulkheadRejectedAsync: context =>
            {
                logger.LogWarning("Blockchain gateway bulkhead rejected execution due to pressure.");
                return Task.CompletedTask;
            });

        return Policy.WrapAsync(bulkheadPolicy, circuitBreakerPolicy, timeoutPolicy);
    }
}
