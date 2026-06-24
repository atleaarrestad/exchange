using Exchange.CryptoTransactions.Application;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Exchange.CryptoTransactions.Infrastructure.Gateways;

public sealed class RuntimeKrakenGatewayOptionsProvider(
    IDbContextFactory<CryptoTransactionsDbContext> dbContextFactory,
    KrakenBlockchainTransferGatewayOptions fallbackOptions) : IKrakenGatewayOptionsProvider
{
    private readonly Lock gate = new();
    private KrakenBlockchainTransferGatewayOptions current = fallbackOptions ?? throw new ArgumentNullException(nameof(fallbackOptions));

    public KrakenBlockchainTransferGatewayOptions GetCurrent()
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
            : BuildKrakenOptions(selected, fallbackOptions);
        KrakenBlockchainTransferGatewayOptions.ValidateForRuntime(updated);

        lock (gate)
        {
            current = updated;
        }
    }

    private static async Task<CryptoGatewaySettingsProfileEntity?> SelectProfileAsync(
        CryptoTransactionsDbContext context,
        Guid? profileId,
        CancellationToken cancellationToken)
    {
        var query = context.CryptoGatewaySettingsProfiles
            .AsNoTracking()
            .Where(candidate => candidate.Provider == GatewayProviders.Kraken);

        if (profileId.HasValue)
        {
            var explicitProfile = await query
                .SingleOrDefaultAsync(candidate => candidate.Id == profileId.Value, cancellationToken);
            if (explicitProfile is not null && explicitProfile.Enabled)
            {
                return explicitProfile;
            }
        }

        return await query
            .OrderByDescending(candidate => candidate.Enabled)
            .ThenByDescending(candidate => candidate.UpdatedAtUtc)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static KrakenBlockchainTransferGatewayOptions BuildKrakenOptions(
        CryptoGatewaySettingsProfileEntity source,
        KrakenBlockchainTransferGatewayOptions fallback)
    {
        var providerSettings = ReadProviderSettings(source.ProviderSettingsJson);
        return new KrakenBlockchainTransferGatewayOptions
        {
            Enabled = source.Enabled,
            BaseUrl = source.BaseUrl,
            HttpTimeoutSeconds = source.HttpTimeoutSeconds,
            ApiKey = source.ApiKey,
            ApiSecret = source.ApiSecret,
            BitcoinWithdrawalKey = providerSettings.BitcoinWithdrawalKey ?? fallback.BitcoinWithdrawalKey,
            EtherWithdrawalKey = providerSettings.EtherWithdrawalKey ?? fallback.EtherWithdrawalKey,
            BitcoinRequiredConfirmations = providerSettings.BitcoinRequiredConfirmations ?? fallback.BitcoinRequiredConfirmations,
            EtherRequiredConfirmations = providerSettings.EtherRequiredConfirmations ?? fallback.EtherRequiredConfirmations
        };
    }

    private static ProviderSettings ReadProviderSettings(string providerSettingsJson)
    {
        if (string.IsNullOrWhiteSpace(providerSettingsJson))
        {
            return new ProviderSettings(null, null, null, null);
        }

        using var document = JsonDocument.Parse(providerSettingsJson);
        var root = document.RootElement;

        return new ProviderSettings(
            TryReadString(root, "bitcoinWithdrawalKey"),
            TryReadString(root, "etherWithdrawalKey"),
            TryReadInt32(root, "bitcoinRequiredConfirmations"),
            TryReadInt32(root, "etherRequiredConfirmations"));
    }

    private static string? TryReadString(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? TryReadInt32(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(element.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private sealed record ProviderSettings(
        string? BitcoinWithdrawalKey,
        string? EtherWithdrawalKey,
        int? BitcoinRequiredConfirmations,
        int? EtherRequiredConfirmations);
}
