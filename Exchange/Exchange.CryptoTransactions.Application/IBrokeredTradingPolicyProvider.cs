namespace Exchange.CryptoTransactions.Application;

public interface IBrokeredTradingPolicyProvider
{
    BrokeredTradingPolicy GetCurrent();

    Task RefreshAsync(Guid? profileId, CancellationToken cancellationToken = default);
}

public sealed class StaticBrokeredTradingPolicyProvider(BrokeredTradingPolicy policy) : IBrokeredTradingPolicyProvider
{
    private readonly BrokeredTradingPolicy current = policy ?? throw new ArgumentNullException(nameof(policy));

    public BrokeredTradingPolicy GetCurrent() => current;

    public Task RefreshAsync(Guid? profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
