using Exchange.CryptoTransactions.Application.Contracts;
using Exchange.CryptoTransactions.Domain.ValueObjects;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class RuntimeKrakenBlockchainTransferGateway(
    IKrakenGatewayOptionsProvider optionsProvider,
    TimeProvider timeProvider) : IBlockchainTransferGateway
{
    private readonly Lock gate = new();
    private string? cachedOptionsKey;
    private KrakenBlockchainTransferGateway? gateway;
    private HttpClient? httpClient;

    public Task<BlockchainTransferResult> SubmitAsync(BlockchainTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var configuredGateway = GetConfiguredGateway();
        return configuredGateway.SubmitAsync(request, cancellationToken);
    }

    public Task<BlockchainTransferStatus> GetTransferStatusAsync(
        string sourceAccountId,
        AssetSymbol assetSymbol,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var configuredGateway = GetConfiguredGateway();
        return configuredGateway.GetTransferStatusAsync(sourceAccountId, assetSymbol, idempotencyKey, cancellationToken);
    }

    private KrakenBlockchainTransferGateway GetConfiguredGateway()
    {
        var options = optionsProvider.GetCurrent();
        if (!options.Enabled)
        {
            throw new ExternalDependencyNotConfiguredException(
                "No blockchain transfer gateway is configured. Enable simulation or provide a real gateway implementation.");
        }

        var optionsKey = BuildOptionsKey(options);
        lock (gate)
        {
            if (gateway is not null && string.Equals(cachedOptionsKey, optionsKey, StringComparison.Ordinal))
            {
                return gateway;
            }

            httpClient?.Dispose();
            httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.BaseUrl),
                // The resilience wrapper controls request timeouts via cancellation tokens.
                Timeout = Timeout.InfiniteTimeSpan
            };
            gateway = new KrakenBlockchainTransferGateway(options, httpClient, timeProvider);
            cachedOptionsKey = optionsKey;
            return gateway;
        }
    }

    private static string BuildOptionsKey(KrakenBlockchainTransferGatewayOptions options)
    {
        return string.Join(
            '|',
            options.Enabled ? "1" : "0",
            options.BaseUrl,
            options.HttpTimeoutSeconds.ToString(),
            options.ApiKey ?? string.Empty,
            options.ApiSecret ?? string.Empty,
            options.BitcoinWithdrawalKey ?? string.Empty,
            options.EtherWithdrawalKey ?? string.Empty,
            options.BitcoinRequiredConfirmations.ToString(),
            options.EtherRequiredConfirmations.ToString());
    }
}
