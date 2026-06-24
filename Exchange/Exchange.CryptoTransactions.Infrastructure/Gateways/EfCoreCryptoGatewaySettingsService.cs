using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Application.Validation;
using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class EfCoreCryptoGatewaySettingsService(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    KrakenBlockchainTransferGatewayOptions krakenOptions,
    IKrakenGatewayOptionsProvider krakenGatewayOptionsProvider,
    ICryptoGatewaySettingsCommandValidator commandValidator)
    : ICryptoGatewaySettingsService
{
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool isInitialized;

    public async Task<IReadOnlyList<CryptoGatewaySettingsProfile>> GetAllAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.CryptoGatewaySettingsProfiles
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(MapProfile).ToArray();
    }

    public async Task<CryptoGatewaySettingsProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoGatewaySettingsProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return entity is null ? null : MapProfile(entity);
    }

    public async Task<CryptoGatewaySettingsProfile> CreateAsync(
        CreateCryptoGatewaySettingsProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        commandValidator.Validate(command);
        EnsureCanEnable(command.Enabled, apiKey: null, apiSecret: null);
        await EnsureInitializedAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entity = new CryptoGatewaySettingsProfileEntity
        {
            Id = Guid.CreateVersion7(),
            Name = command.Name.Trim(),
            Provider = command.Provider.Trim().ToLowerInvariant(),
            Enabled = command.Enabled,
            BaseUrl = command.BaseUrl.Trim(),
            HttpTimeoutSeconds = command.HttpTimeoutSeconds,
            ProviderSettingsJson = command.ProviderSettingsJson.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.CryptoGatewaySettingsProfiles.Add(entity);
        context.SettingsChangeOutboxEntries.Add(CreateOutboxEntry(
            SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged,
            new CryptoGatewaySettingsProfileChangedIntegrationEvent(entity.Id, SettingsProfileChangeType.Created, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await krakenGatewayOptionsProvider.RefreshAsync(entity.Id, cancellationToken);
        return MapProfile(entity);
    }

    public async Task<CryptoGatewaySettingsProfile?> UpdateAsync(
        Guid id,
        UpdateCryptoGatewaySettingsProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        commandValidator.Validate(command);
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoGatewaySettingsProfiles.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        EnsureCanEnable(command.Enabled, entity.ApiKey, entity.ApiSecret);
        entity.Name = command.Name.Trim();
        entity.Provider = command.Provider.Trim().ToLowerInvariant();
        entity.Enabled = command.Enabled;
        entity.BaseUrl = command.BaseUrl.Trim();
        entity.HttpTimeoutSeconds = command.HttpTimeoutSeconds;
        entity.ProviderSettingsJson = command.ProviderSettingsJson.Trim();
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        context.SettingsChangeOutboxEntries.Add(CreateOutboxEntry(
            SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged,
            new CryptoGatewaySettingsProfileChangedIntegrationEvent(entity.Id, SettingsProfileChangeType.Updated, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await krakenGatewayOptionsProvider.RefreshAsync(entity.Id, cancellationToken);
        return MapProfile(entity);
    }

    public async Task<bool> SaveCredentialsAsync(
        Guid id,
        SaveCryptoGatewayCredentialsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var apiKey = NormalizeRequired(command.ApiKey, nameof(command.ApiKey));
        var apiSecret = NormalizeRequired(command.ApiSecret, nameof(command.ApiSecret));
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoGatewaySettingsProfiles.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.ApiKey = apiKey;
        entity.ApiSecret = apiSecret;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        context.SettingsChangeOutboxEntries.Add(CreateOutboxEntry(
            SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged,
            new CryptoGatewaySettingsProfileChangedIntegrationEvent(entity.Id, SettingsProfileChangeType.CredentialsUpdated, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await krakenGatewayOptionsProvider.RefreshAsync(entity.Id, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.CryptoGatewaySettingsProfiles
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        context.CryptoGatewaySettingsProfiles.Remove(entity);
        context.SettingsChangeOutboxEntries.Add(CreateOutboxEntry(
            SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged,
            new CryptoGatewaySettingsProfileChangedIntegrationEvent(id, SettingsProfileChangeType.Deleted, DateTimeOffset.UtcNow)));
        await context.SaveChangesAsync(cancellationToken);
        await krakenGatewayOptionsProvider.RefreshAsync(null, cancellationToken);
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
            await context.Database.MigrateAsync(cancellationToken);
            await EnsureDefaultGatewayProfileExistsAsync(context, cancellationToken);
            isInitialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    private async Task EnsureDefaultGatewayProfileExistsAsync(
        CryptoTransactionsDbContext context,
        CancellationToken cancellationToken)
    {
        var hasProfiles = await context.CryptoGatewaySettingsProfiles.AnyAsync(cancellationToken);
        if (hasProfiles)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        context.CryptoGatewaySettingsProfiles.Add(new CryptoGatewaySettingsProfileEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "Kraken default",
            Provider = GatewayProviders.Kraken,
            Enabled = krakenOptions.Enabled,
            BaseUrl = krakenOptions.BaseUrl,
            HttpTimeoutSeconds = krakenOptions.HttpTimeoutSeconds,
            ApiKey = NormalizeOptional(krakenOptions.ApiKey),
            ApiSecret = NormalizeOptional(krakenOptions.ApiSecret),
            ProviderSettingsJson = BuildKrakenSettingsJson(krakenOptions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static CryptoGatewaySettingsProfile MapProfile(CryptoGatewaySettingsProfileEntity entity)
    {
        return new CryptoGatewaySettingsProfile(
            entity.Id,
            entity.Name,
            entity.Provider,
            entity.Enabled,
            entity.BaseUrl,
            entity.HttpTimeoutSeconds,
            entity.ProviderSettingsJson,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);
    }

    private static string BuildKrakenSettingsJson(KrakenBlockchainTransferGatewayOptions options)
    {
        return JsonSerializer.Serialize(new
        {
            bitcoinWithdrawalKey = NormalizeOptional(options.BitcoinWithdrawalKey) ?? string.Empty,
            etherWithdrawalKey = NormalizeOptional(options.EtherWithdrawalKey) ?? string.Empty,
            bitcoinRequiredConfirmations = options.BitcoinRequiredConfirmations,
            etherRequiredConfirmations = options.EtherRequiredConfirmations
        });
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeRequired(string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApplicationValidationException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [key] = [$"{key} is required."]
            });
        }

        return value.Trim();
    }

    private static void EnsureCanEnable(bool enabled, string? apiKey, string? apiSecret)
    {
        if (!enabled)
        {
            return;
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            errors[nameof(CryptoGatewaySettingsProfileEntity.ApiKey)] = ["ApiKey is required before enabling the profile."];
        }

        if (string.IsNullOrWhiteSpace(apiSecret))
        {
            errors[nameof(CryptoGatewaySettingsProfileEntity.ApiSecret)] = ["ApiSecret is required before enabling the profile."];
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }

    private static SettingsChangeOutboxEntryEntity CreateOutboxEntry(string messageType, object payload)
    {
        return new SettingsChangeOutboxEntryEntity
        {
            Id = Guid.CreateVersion7(),
            MessageType = messageType,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PublishedAtUtc = null,
            PublishAttemptCount = 0
        };
    }
}
