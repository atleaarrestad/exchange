using System.Text.Json;

namespace Exchange.CryptoTransactions.Application.Validation;

public sealed class CryptoGatewaySettingsCommandValidator : ICryptoGatewaySettingsCommandValidator
{
    public void Validate(CreateCryptoGatewaySettingsProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCore(
            command.Name,
            command.Provider,
            command.BaseUrl,
            command.HttpTimeoutSeconds,
            command.ProviderSettingsJson);
    }

    public void Validate(UpdateCryptoGatewaySettingsProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCore(
            command.Name,
            command.Provider,
            command.BaseUrl,
            command.HttpTimeoutSeconds,
            command.ProviderSettingsJson);
    }

    private static void ValidateCore(
        string name,
        string provider,
        string baseUrl,
        int httpTimeoutSeconds,
        string providerSettingsJson)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var normalizedProvider = provider.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameof(name)] = ["Name is required."];
        }

        if (normalizedProvider != GatewayProviders.Kraken && normalizedProvider != GatewayProviders.Coinbase)
        {
            errors[nameof(provider)] = ["Provider must be one of: kraken, coinbase."];
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            errors[nameof(baseUrl)] = ["BaseUrl is required."];
        }
        else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
        {
            errors[nameof(baseUrl)] = ["BaseUrl must be an absolute URI."];
        }

        if (httpTimeoutSeconds <= 0)
        {
            errors[nameof(httpTimeoutSeconds)] = ["HttpTimeoutSeconds must be greater than zero."];
        }

        if (string.IsNullOrWhiteSpace(providerSettingsJson))
        {
            errors[nameof(providerSettingsJson)] = ["ProviderSettingsJson is required."];
        }
        else if (!TryParseObject(providerSettingsJson))
        {
            errors[nameof(providerSettingsJson)] = ["ProviderSettingsJson must be a valid JSON object."];
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }

    private static bool TryParseObject(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
