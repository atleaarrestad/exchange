using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.DependencyInjection;

public sealed class RuntimeBrokeredTradingPolicyProvider(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    BrokeredTradingPolicy fallbackPolicy) : IBrokeredTradingPolicyProvider
{
    private readonly Lock gate = new();
    private BrokeredTradingPolicy current = fallbackPolicy ?? throw new ArgumentNullException(nameof(fallbackPolicy));

    public BrokeredTradingPolicy GetCurrent()
    {
        lock (gate)
        {
            return current;
        }
    }

    public async Task RefreshAsync(Guid? profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        CryptoSettingsProfileEntity? entity = null;
        if (profileId.HasValue)
        {
            entity = await context.CryptoSettingsProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == profileId.Value, cancellationToken);
        }

        entity ??= await context.CryptoSettingsProfiles
            .AsNoTracking()
            .OrderByDescending(candidate => candidate.UpdatedAtUtc)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var updatedPolicy = entity is null
            ? fallbackPolicy
            : new BrokeredTradingPolicy
            {
                QuoteTtlSeconds = entity.QuoteTtlSeconds,
                InternalOnlySpreadBasisPoints = entity.InternalOnlySpreadBasisPoints,
                ExternalHedgeSpreadBasisPoints = entity.ExternalHedgeSpreadBasisPoints,
                MaxAllowedSlippageBasisPoints = entity.MaxAllowedSlippageBasisPoints,
                MaxBufferedHedgeCustomerBuys = entity.MaxBufferedHedgeCustomerBuys,
                MaxBufferedHedgeDelaySeconds = entity.MaxBufferedHedgeDelaySeconds
            };
        updatedPolicy.Validate();

        lock (gate)
        {
            current = updatedPolicy;
        }
    }
}
