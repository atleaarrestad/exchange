using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreCryptoGatewayResilienceSettingsService(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    BlockchainGatewayResilienceOptions fallbackOptions,
    IBlockchainGatewayResiliencePolicyOptionsProvider policyOptionsProvider,
    ICryptoGatewayResilienceSettingsCommandValidator commandValidator)
    : ICryptoGatewayResilienceSettingsService
{
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public async Task<IReadOnlyList<CryptoGatewayResilienceSettingsProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.CryptoGatewayResilienceSettingsProfiles
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(MapProfile).ToArray();
    }

    public async Task<CryptoGatewayResilienceSettingsProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoGatewayResilienceSettingsProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return entity is null ? null : MapProfile(entity);
    }

    public async Task<CryptoGatewayResilienceSettingsProfile> CreateAsync(
        CreateCryptoGatewayResilienceSettingsProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        commandValidator.Validate(command);
        await EnsureInitializedAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entity = new CryptoGatewayResilienceSettingsProfileEntity
        {
            Id = Guid.CreateVersion7(),
            Name = command.Name.Trim(),
            Enabled = command.Enabled,
            OperationTimeoutSeconds = command.OperationTimeoutSeconds,
            RetryCount = command.RetryCount,
            RetryDelayMilliseconds = command.RetryDelayMilliseconds,
            FailureRatio = command.FailureRatio,
            MinimumThroughput = command.MinimumThroughput,
            SamplingDurationSeconds = command.SamplingDurationSeconds,
            BreakDurationSeconds = command.BreakDurationSeconds,
            MaxParallelization = command.MaxParallelization,
            MaxQueueingActions = command.MaxQueueingActions,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.CryptoGatewayResilienceSettingsProfiles.Add(entity);
        context.SettingsChangeOutboxEntries.Add(SettingsChangeOutboxEntryFactory.Create(
            SettingsChangeOutboxMessageTypes.CryptoGatewayResilienceSettingsProfileChanged,
            new CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent(entity.Id, SettingsProfileChangeType.Created, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await policyOptionsProvider.RefreshAsync(entity.Id, cancellationToken);
        return MapProfile(entity);
    }

    public async Task<CryptoGatewayResilienceSettingsProfile?> UpdateAsync(
        Guid id,
        UpdateCryptoGatewayResilienceSettingsProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        commandValidator.Validate(command);
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoGatewayResilienceSettingsProfiles
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Name = command.Name.Trim();
        entity.Enabled = command.Enabled;
        entity.OperationTimeoutSeconds = command.OperationTimeoutSeconds;
        entity.RetryCount = command.RetryCount;
        entity.RetryDelayMilliseconds = command.RetryDelayMilliseconds;
        entity.FailureRatio = command.FailureRatio;
        entity.MinimumThroughput = command.MinimumThroughput;
        entity.SamplingDurationSeconds = command.SamplingDurationSeconds;
        entity.BreakDurationSeconds = command.BreakDurationSeconds;
        entity.MaxParallelization = command.MaxParallelization;
        entity.MaxQueueingActions = command.MaxQueueingActions;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        context.SettingsChangeOutboxEntries.Add(SettingsChangeOutboxEntryFactory.Create(
            SettingsChangeOutboxMessageTypes.CryptoGatewayResilienceSettingsProfileChanged,
            new CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent(entity.Id, SettingsProfileChangeType.Updated, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await policyOptionsProvider.RefreshAsync(entity.Id, cancellationToken);
        return MapProfile(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoGatewayResilienceSettingsProfiles
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        context.CryptoGatewayResilienceSettingsProfiles.Remove(entity);
        context.SettingsChangeOutboxEntries.Add(SettingsChangeOutboxEntryFactory.Create(
            SettingsChangeOutboxMessageTypes.CryptoGatewayResilienceSettingsProfileChanged,
            new CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent(id, SettingsProfileChangeType.Deleted, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await policyOptionsProvider.RefreshAsync(null, cancellationToken);
        return true;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (isInitialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (isInitialized)
            {
                return;
            }

            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await EnsureDefaultProfileExistsAsync(context, cancellationToken);
            await policyOptionsProvider.RefreshAsync(null, cancellationToken);
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private async Task EnsureDefaultProfileExistsAsync(CryptoTransactionsDbContext context, CancellationToken cancellationToken)
    {
        var hasProfiles = await context.CryptoGatewayResilienceSettingsProfiles.AnyAsync(cancellationToken);
        if (hasProfiles)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        context.CryptoGatewayResilienceSettingsProfiles.Add(new CryptoGatewayResilienceSettingsProfileEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "Configured defaults",
            Enabled = fallbackOptions.Enabled,
            OperationTimeoutSeconds = fallbackOptions.OperationTimeoutSeconds,
            RetryCount = fallbackOptions.RetryCount,
            RetryDelayMilliseconds = fallbackOptions.RetryDelayMilliseconds,
            FailureRatio = fallbackOptions.FailureRatio,
            MinimumThroughput = fallbackOptions.MinimumThroughput,
            SamplingDurationSeconds = fallbackOptions.SamplingDurationSeconds,
            BreakDurationSeconds = fallbackOptions.BreakDurationSeconds,
            MaxParallelization = fallbackOptions.MaxParallelization,
            MaxQueueingActions = fallbackOptions.MaxQueueingActions,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static CryptoGatewayResilienceSettingsProfile MapProfile(CryptoGatewayResilienceSettingsProfileEntity entity)
    {
        return new CryptoGatewayResilienceSettingsProfile(
            entity.Id,
            entity.Name,
            entity.Enabled,
            entity.OperationTimeoutSeconds,
            entity.RetryCount,
            entity.RetryDelayMilliseconds,
            entity.FailureRatio,
            entity.MinimumThroughput,
            entity.SamplingDurationSeconds,
            entity.BreakDurationSeconds,
            entity.MaxParallelization,
            entity.MaxQueueingActions,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

}
