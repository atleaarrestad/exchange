using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Contracts;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class InMemoryExternalHedgeBatchQueue(
    IExternalLiquidityHedgingGateway externalLiquidityHedgingGateway,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    TimeProvider timeProvider) : IExternalHedgeBatchQueue
{
    private readonly Lock gate = new();
    private readonly Dictionary<BatchKey, List<BufferedExternalHedgeRequest>> pendingByBatch = new();
    private readonly HashSet<RegisteredOrderKey> registeredOrderKeys = [];
    private readonly SemaphoreSlim executeGate = new(1, 1);

    public Task RegisterAsync(BufferedExternalHedgeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Quantity), request.Quantity, "Quantity must be greater than zero.");
        }

        var normalizedCustomerAccountId = request.CustomerAccountId.Trim();
        if (normalizedCustomerAccountId.Length == 0)
        {
            throw new ArgumentException("CustomerAccountId is required.", nameof(request.CustomerAccountId));
        }

        var normalizedClientOrderId = request.ClientOrderId.Trim();
        if (normalizedClientOrderId.Length == 0)
        {
            throw new ArgumentException("ClientOrderId is required.", nameof(request.ClientOrderId));
        }

        var normalized = request with
        {
            CustomerAccountId = normalizedCustomerAccountId,
            ClientOrderId = normalizedClientOrderId
        };
        var key = BatchKey.Create(normalized.AssetSymbol, normalized.QuoteCurrency);
        var registeredOrderKey = RegisteredOrderKey.Create(normalized.CustomerAccountId, normalized.ClientOrderId);

        lock (gate)
        {
            if (!registeredOrderKeys.Add(registeredOrderKey))
            {
                return Task.CompletedTask;
            }

            if (!pendingByBatch.TryGetValue(key, out var pending))
            {
                pending = [];
                pendingByBatch[key] = pending;
            }

            pending.Add(normalized);
        }

        return Task.CompletedTask;
    }

    public async Task ExecuteDueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await executeGate.WaitAsync(cancellationToken);
        try
        {
            while (TryTakeDueBatch(out var batch))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var aggregateQuantity = SumQuantity(batch.Items);
                HedgePurchaseResult result;
                try
                {
                    result = await externalLiquidityHedgingGateway.BuyAsync(
                        new HedgePurchaseRequest(
                            $"hedge-batch-{batch.BatchId}",
                            batch.BatchKey.AssetSymbol,
                            batch.BatchKey.QuoteCurrency,
                            aggregateQuantity),
                        cancellationToken);
                }
                catch
                {
                    Restore(batch);
                    throw;
                }

                MarkExecuted(batch, result);
            }
        }
        finally
        {
            executeGate.Release();
        }
    }

    private bool TryTakeDueBatch(out PendingBatch batch)
    {
        var now = timeProvider.GetUtcNow();
        var tradingPolicy = tradingPolicyProvider.GetCurrent();
        lock (gate)
        {
            foreach (var entry in pendingByBatch)
            {
                var pending = entry.Value;
                if (pending.Count == 0)
                {
                    continue;
                }

                var oldestRequestedAtUtc = pending[0].RequestedAtUtc;
                var reachedCountThreshold = pending.Count >= tradingPolicy.MaxBufferedHedgeCustomerBuys;
                var reachedTimeThreshold = oldestRequestedAtUtc + tradingPolicy.MaxBufferedHedgeDelay <= now;

                if (!reachedCountThreshold && !reachedTimeThreshold)
                {
                    continue;
                }

                var snapshot = pending.ToArray();
                pending.Clear();
                batch = new PendingBatch(Guid.CreateVersion7(), entry.Key, snapshot);
                return true;
            }
        }

        batch = null!;
        return false;
    }

    private void Restore(PendingBatch batch)
    {
        lock (gate)
        {
            if (!pendingByBatch.TryGetValue(batch.BatchKey, out var pending))
            {
                pending = [];
                pendingByBatch[batch.BatchKey] = pending;
            }

            pending.InsertRange(0, batch.Items);
        }
    }

    private static decimal SumQuantity(IReadOnlyList<BufferedExternalHedgeRequest> items)
    {
        var total = 0m;
        foreach (var item in items)
        {
            total = checked(total + item.Quantity);
        }

        return total;
    }

    private void MarkExecuted(PendingBatch batch, HedgePurchaseResult _)
    {
        // Executed order keys are retained in registeredOrderKeys to enforce idempotent registration.
        lock (gate)
        {
            if (pendingByBatch.TryGetValue(batch.BatchKey, out var pending) && pending.Count == 0)
            {
                pendingByBatch.Remove(batch.BatchKey);
            }
        }
    }

    private sealed record BatchKey(Exchange.CryptoTransactions.Domain.ValueObjects.AssetSymbol AssetSymbol, Exchange.CryptoTransactions.Domain.ValueObjects.QuoteCurrency QuoteCurrency)
    {
        public static BatchKey Create(Exchange.CryptoTransactions.Domain.ValueObjects.AssetSymbol assetSymbol, Exchange.CryptoTransactions.Domain.ValueObjects.QuoteCurrency quoteCurrency)
            => new(assetSymbol, quoteCurrency);
    }

    private sealed record RegisteredOrderKey(string CustomerAccountId, string ClientOrderId)
    {
        public static RegisteredOrderKey Create(string customerAccountId, string clientOrderId)
            => new(customerAccountId, clientOrderId);
    }

    private sealed record PendingBatch(Guid BatchId, BatchKey BatchKey, IReadOnlyList<BufferedExternalHedgeRequest> Items);
}
