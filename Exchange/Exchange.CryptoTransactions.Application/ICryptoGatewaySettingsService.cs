namespace Exchange.CryptoTransactions.Application;

public interface ICryptoGatewaySettingsService
{
    Task<IReadOnlyList<CryptoGatewaySettingsProfile>> GetAllAsync(CancellationToken cancellationToken);

    Task<CryptoGatewaySettingsProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<CryptoGatewaySettingsProfile> CreateAsync(CreateCryptoGatewaySettingsProfileCommand command, CancellationToken cancellationToken);

    Task<CryptoGatewaySettingsProfile?> UpdateAsync(Guid id, UpdateCryptoGatewaySettingsProfileCommand command, CancellationToken cancellationToken);

    Task<bool> SaveCredentialsAsync(Guid id, SaveCryptoGatewayCredentialsCommand command, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record CreateCryptoGatewaySettingsProfileCommand(
    string Name,
    string Provider,
    bool Enabled,
    string BaseUrl,
    int HttpTimeoutSeconds,
    string ProviderSettingsJson);

public sealed record UpdateCryptoGatewaySettingsProfileCommand(
    string Name,
    string Provider,
    bool Enabled,
    string BaseUrl,
    int HttpTimeoutSeconds,
    string ProviderSettingsJson);

public sealed record SaveCryptoGatewayCredentialsCommand(
    string ApiKey,
    string ApiSecret);
