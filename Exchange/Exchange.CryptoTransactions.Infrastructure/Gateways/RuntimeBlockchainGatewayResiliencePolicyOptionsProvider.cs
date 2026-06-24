using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Exchange.CryptoTransactions.Resilience.Gateways;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class RuntimeBlockchainGatewayResiliencePolicyOptionsProvider(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    BlockchainGatewayResiliencePolicyOptions fallbackOptions) : IBlockchainGatewayResiliencePolicyOptionsProvider
{
    private readonly Lock gate = new();
    private BlockchainGatewayResiliencePolicyOptions current = fallbackOptions ?? throw new ArgumentNullException(nameof(fallbackOptions));

    public BlockchainGatewayResiliencePolicyOptions GetCurrent()
    {
        lock (gate)
        {
            return current;
        }
    }

    public async Task RefreshAsync(Guid? profileId, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var selected = await SelectProfileAsync(context, profileId, cancellationToken);
        var updated = selected is null
            ? fallbackOptions
            : new BlockchainGatewayResiliencePolicyOptions
            {
                Enabled = selected.Enabled,
                OperationTimeout = TimeSpan.FromSeconds(selected.OperationTimeoutSeconds),
                RetryCount = selected.RetryCount,
                RetryDelay = TimeSpan.FromMilliseconds(selected.RetryDelayMilliseconds),
                FailureRatio = selected.FailureRatio,
                MinimumThroughput = selected.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(selected.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(selected.BreakDurationSeconds),
                MaxParallelization = selected.MaxParallelization,
                MaxQueueingActions = selected.MaxQueueingActions
            };
        BlockchainGatewayResiliencePolicyOptions.Validate(updated);

        lock (gate)
        {
            current = updated;
        }
    }

    private static async Task<CryptoGatewayResilienceSettingsProfileEntity?> SelectProfileAsync(
        CryptoTransactionsDbContext context,
        Guid? profileId,
        CancellationToken cancellationToken)
    {
        var query = context.CryptoGatewayResilienceSettingsProfiles.AsNoTracking();

        if (profileId.HasValue)
        {
            var explicitProfile = await query
                .SingleOrDefaultAsync(candidate => candidate.Id == profileId.Value, cancellationToken);
            if (explicitProfile is not null)
            {
                return explicitProfile;
            }
        }

        return await query
            .OrderByDescending(candidate => candidate.UpdatedAtUtc)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
