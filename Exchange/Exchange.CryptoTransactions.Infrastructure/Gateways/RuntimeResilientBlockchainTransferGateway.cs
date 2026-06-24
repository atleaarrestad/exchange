using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;
using Exchange.CryptoTransactions.Resilience.Gateways;
using Microsoft.Extensions.Logging;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class RuntimeResilientBlockchainTransferGateway(
    RuntimeKrakenBlockchainTransferGateway innerGateway,
    IBlockchainGatewayResiliencePolicyOptionsProvider policyOptionsProvider,
    ILoggerFactory loggerFactory) : IBlockchainTransferGateway
{
    private readonly Lock gate = new();
    private string? cachedOptionsKey;
    private ResilientBlockchainTransferGateway? gateway;
    private readonly ILogger<ResilientBlockchainTransferGateway> resilienceLogger = loggerFactory.CreateLogger<ResilientBlockchainTransferGateway>();

    public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetConfiguredGateway().SubmitAsync(request, cancellationToken);
    }

    public Task<BlockchainTransferStatus> GetTransferStatusAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return GetConfiguredGateway().GetTransferStatusAsync(sourceAccountId, assetSymbol, idempotencyKey, cancellationToken);
    }

    private ResilientBlockchainTransferGateway GetConfiguredGateway()
    {
        var options = policyOptionsProvider.GetCurrent();
        var optionsKey = BuildOptionsKey(options);
        lock (gate)
        {
            if (gateway is not null && string.Equals(cachedOptionsKey, optionsKey, StringComparison.Ordinal))
            {
                return gateway;
            }

            gateway = new ResilientBlockchainTransferGateway(innerGateway, options, resilienceLogger);
            cachedOptionsKey = optionsKey;
            return gateway;
        }
    }

    private static string BuildOptionsKey(BlockchainGatewayResiliencePolicyOptions options)
    {
        return string.Join(
            '|',
            options.Enabled ? "1" : "0",
            options.OperationTimeout.TotalMilliseconds.ToString("F0"),
            options.RetryCount.ToString(),
            options.RetryDelay.TotalMilliseconds.ToString("F0"),
            options.FailureRatio.ToString("G17"),
            options.MinimumThroughput.ToString(),
            options.SamplingDuration.TotalMilliseconds.ToString("F0"),
            options.BreakDuration.TotalMilliseconds.ToString("F0"),
            options.MaxParallelization.ToString(),
            options.MaxQueueingActions.ToString());
    }
}
