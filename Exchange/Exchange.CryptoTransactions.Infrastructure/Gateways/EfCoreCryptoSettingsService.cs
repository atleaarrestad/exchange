using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreCryptoSettingsService(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    BrokeredTradingOptions brokeredTradingOptions,
    TimeoutReconciliationOptions timeoutReconciliationOptions,
    IConfiguration configuration,
    IBrokeredTradingPolicyProvider tradingPolicyProvider,
    ICryptoSettingsCommandValidator commandValidator)
    : ICryptoSettingsService
{
    private const string SimulationSection = "CryptoTransactions:Simulation";
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public async Task<IReadOnlyList<CryptoSettingsProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.CryptoSettingsProfiles
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(MapProfile).ToArray();
    }

    public async Task<CryptoSettingsProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoSettingsProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return entity is null ? null : MapProfile(entity);
    }

    public async Task<CryptoSettingsProfile> CreateAsync(CreateCryptoSettingsProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        commandValidator.Validate(command);
        await EnsureInitializedAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entity = new CryptoSettingsProfileEntity
        {
            Id = Guid.CreateVersion7(),
            Name = command.Name.Trim(),
            QuoteTtlSeconds = command.QuoteTtlSeconds,
            InternalOnlySpreadBasisPoints = command.InternalOnlySpreadBasisPoints,
            ExternalHedgeSpreadBasisPoints = command.ExternalHedgeSpreadBasisPoints,
            MaxAllowedSlippageBasisPoints = command.MaxAllowedSlippageBasisPoints,
            BitcoinReferencePriceNok = command.BitcoinReferencePriceNok,
            EtherReferencePriceNok = command.EtherReferencePriceNok,
            InitialBitcoinInventory = command.InitialBitcoinInventory,
            InitialEtherInventory = command.InitialEtherInventory,
            MaxBufferedHedgeCustomerBuys = command.MaxBufferedHedgeCustomerBuys,
            MaxBufferedHedgeDelaySeconds = command.MaxBufferedHedgeDelaySeconds,
            TimeoutReconciliationScanIntervalSeconds = command.TimeoutReconciliationScanIntervalSeconds,
            TimeoutReconciliationStaleAfterSeconds = command.TimeoutReconciliationStaleAfterSeconds,
            SimulationMinLatencyMs = command.SimulationMinLatencyMs,
            SimulationMaxLatencyMs = command.SimulationMaxLatencyMs,
            SimulationRejectRate = command.SimulationRejectRate,
            SimulationTimeoutRate = command.SimulationTimeoutRate,
            SimulationDefaultBitcoinAvailableBalance = command.SimulationDefaultBitcoinAvailableBalance,
            SimulationDefaultEtherAvailableBalance = command.SimulationDefaultEtherAvailableBalance,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.CryptoSettingsProfiles.Add(entity);
        context.SettingsChangeOutboxEntries.Add(SettingsChangeOutboxEntryFactory.Create(
            SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged,
            new CryptoSettingsProfileChangedIntegrationEvent(entity.Id, SettingsProfileChangeType.Created, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await tradingPolicyProvider.RefreshAsync(entity.Id, cancellationToken);
        return MapProfile(entity);
    }

    public async Task<CryptoSettingsProfile?> UpdateAsync(
        Guid id,
        UpdateCryptoSettingsProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        commandValidator.Validate(command);
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoSettingsProfiles
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.Name = command.Name.Trim();
        entity.QuoteTtlSeconds = command.QuoteTtlSeconds;
        entity.InternalOnlySpreadBasisPoints = command.InternalOnlySpreadBasisPoints;
        entity.ExternalHedgeSpreadBasisPoints = command.ExternalHedgeSpreadBasisPoints;
        entity.MaxAllowedSlippageBasisPoints = command.MaxAllowedSlippageBasisPoints;
        entity.BitcoinReferencePriceNok = command.BitcoinReferencePriceNok;
        entity.EtherReferencePriceNok = command.EtherReferencePriceNok;
        entity.InitialBitcoinInventory = command.InitialBitcoinInventory;
        entity.InitialEtherInventory = command.InitialEtherInventory;
        entity.MaxBufferedHedgeCustomerBuys = command.MaxBufferedHedgeCustomerBuys;
        entity.MaxBufferedHedgeDelaySeconds = command.MaxBufferedHedgeDelaySeconds;
        entity.TimeoutReconciliationScanIntervalSeconds = command.TimeoutReconciliationScanIntervalSeconds;
        entity.TimeoutReconciliationStaleAfterSeconds = command.TimeoutReconciliationStaleAfterSeconds;
        entity.SimulationMinLatencyMs = command.SimulationMinLatencyMs;
        entity.SimulationMaxLatencyMs = command.SimulationMaxLatencyMs;
        entity.SimulationRejectRate = command.SimulationRejectRate;
        entity.SimulationTimeoutRate = command.SimulationTimeoutRate;
        entity.SimulationDefaultBitcoinAvailableBalance = command.SimulationDefaultBitcoinAvailableBalance;
        entity.SimulationDefaultEtherAvailableBalance = command.SimulationDefaultEtherAvailableBalance;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        context.SettingsChangeOutboxEntries.Add(SettingsChangeOutboxEntryFactory.Create(
            SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged,
            new CryptoSettingsProfileChangedIntegrationEvent(entity.Id, SettingsProfileChangeType.Updated, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await tradingPolicyProvider.RefreshAsync(entity.Id, cancellationToken);
        return MapProfile(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoSettingsProfiles
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        context.CryptoSettingsProfiles.Remove(entity);
        context.SettingsChangeOutboxEntries.Add(SettingsChangeOutboxEntryFactory.Create(
            SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged,
            new CryptoSettingsProfileChangedIntegrationEvent(id, SettingsProfileChangeType.Deleted, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await tradingPolicyProvider.RefreshAsync(null, cancellationToken);
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
            await tradingPolicyProvider.RefreshAsync(null, cancellationToken);
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private static CryptoSettingsProfile MapProfile(CryptoSettingsProfileEntity entity)
    {
        return new CryptoSettingsProfile(
            entity.Id,
            entity.Name,
            entity.QuoteTtlSeconds,
            entity.InternalOnlySpreadBasisPoints,
            entity.ExternalHedgeSpreadBasisPoints,
            entity.MaxAllowedSlippageBasisPoints,
            entity.BitcoinReferencePriceNok,
            entity.EtherReferencePriceNok,
            entity.InitialBitcoinInventory,
            entity.InitialEtherInventory,
            entity.MaxBufferedHedgeCustomerBuys,
            entity.MaxBufferedHedgeDelaySeconds,
            entity.TimeoutReconciliationScanIntervalSeconds,
            entity.TimeoutReconciliationStaleAfterSeconds,
            entity.SimulationMinLatencyMs,
            entity.SimulationMaxLatencyMs,
            entity.SimulationRejectRate,
            entity.SimulationTimeoutRate,
            entity.SimulationDefaultBitcoinAvailableBalance,
            entity.SimulationDefaultEtherAvailableBalance,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

    private async Task EnsureDefaultProfileExistsAsync(
        CryptoTransactionsDbContext context,
        CancellationToken cancellationToken)
    {
        var hasProfiles = await context.CryptoSettingsProfiles.AnyAsync(cancellationToken);
        if (hasProfiles)
        {
            return;
        }

        var simulationSection = configuration.GetSection(SimulationSection);
        var now = DateTimeOffset.UtcNow;
        context.CryptoSettingsProfiles.Add(new CryptoSettingsProfileEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "Configured defaults",
            QuoteTtlSeconds = brokeredTradingOptions.QuoteTtlSeconds,
            InternalOnlySpreadBasisPoints = brokeredTradingOptions.InternalOnlySpreadBasisPoints,
            ExternalHedgeSpreadBasisPoints = brokeredTradingOptions.ExternalHedgeSpreadBasisPoints,
            MaxAllowedSlippageBasisPoints = brokeredTradingOptions.MaxAllowedSlippageBasisPoints,
            BitcoinReferencePriceNok = brokeredTradingOptions.BitcoinReferencePriceNok,
            EtherReferencePriceNok = brokeredTradingOptions.EtherReferencePriceNok,
            InitialBitcoinInventory = brokeredTradingOptions.InitialBitcoinInventory,
            InitialEtherInventory = brokeredTradingOptions.InitialEtherInventory,
            MaxBufferedHedgeCustomerBuys = brokeredTradingOptions.MaxBufferedHedgeCustomerBuys,
            MaxBufferedHedgeDelaySeconds = brokeredTradingOptions.MaxBufferedHedgeDelaySeconds,
            TimeoutReconciliationScanIntervalSeconds = timeoutReconciliationOptions.ScanIntervalSeconds,
            TimeoutReconciliationStaleAfterSeconds = timeoutReconciliationOptions.StaleAfterSeconds,
            SimulationMinLatencyMs = simulationSection.GetValue<int?>(nameof(CryptoSettingsProfileEntity.SimulationMinLatencyMs)) ?? 20,
            SimulationMaxLatencyMs = simulationSection.GetValue<int?>(nameof(CryptoSettingsProfileEntity.SimulationMaxLatencyMs)) ?? 120,
            SimulationRejectRate = simulationSection.GetValue<decimal?>(nameof(CryptoSettingsProfileEntity.SimulationRejectRate)) ?? 0.02m,
            SimulationTimeoutRate = simulationSection.GetValue<decimal?>(nameof(CryptoSettingsProfileEntity.SimulationTimeoutRate)) ?? 0.01m,
            SimulationDefaultBitcoinAvailableBalance = simulationSection.GetValue<decimal?>(nameof(CryptoSettingsProfileEntity.SimulationDefaultBitcoinAvailableBalance)) ?? 2m,
            SimulationDefaultEtherAvailableBalance = simulationSection.GetValue<decimal?>(nameof(CryptoSettingsProfileEntity.SimulationDefaultEtherAvailableBalance)) ?? 20m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await context.SaveChangesAsync(cancellationToken);
    }

}
