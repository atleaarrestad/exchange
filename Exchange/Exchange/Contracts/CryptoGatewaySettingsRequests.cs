namespace Exchange.Contracts;

public sealed record UpsertCryptoGatewaySettingsRequest(
    string Name,
    string Provider,
    bool Enabled,
    string BaseUrl,
    int HttpTimeoutSeconds,
    string ProviderSettingsJson);

public sealed record SaveCryptoGatewayCredentialsRequest(
    string ApiKey,
    string ApiSecret);
