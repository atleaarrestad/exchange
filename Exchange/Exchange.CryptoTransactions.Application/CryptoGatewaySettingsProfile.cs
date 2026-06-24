namespace Exchange.CryptoTransactions.Application;

public sealed record CryptoGatewaySettingsProfile(
    Guid Id,
    string Name,
    string Provider,
    bool Enabled,
    string BaseUrl,
    int HttpTimeoutSeconds,
    string ProviderSettingsJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
