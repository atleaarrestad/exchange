namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public static class SettingsChangeOutboxMessageTypes
{
    public const string CryptoSettingsProfileChanged = "crypto-settings-profile-changed";
    public const string CryptoGatewaySettingsProfileChanged = "crypto-gateway-settings-profile-changed";
    public const string CryptoGatewayResilienceSettingsProfileChanged = "crypto-gateway-resilience-settings-profile-changed";
}
