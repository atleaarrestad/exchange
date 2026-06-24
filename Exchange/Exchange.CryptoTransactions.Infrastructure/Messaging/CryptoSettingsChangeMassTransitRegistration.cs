using Exchange.Infrastructure.Messaging;
using MassTransit;

namespace Exchange.CryptoTransactions.Infrastructure.Messaging;

public static class CryptoSettingsChangeMassTransitRegistration
{
    public static void AddSettingsChangeConsumers(this IBusRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<CryptoSettingsProfileChangedConsumer>();
        configurator.AddConsumer<CryptoGatewaySettingsProfileChangedConsumer>();
        configurator.AddConsumer<CryptoGatewayResilienceSettingsProfileChangedConsumer>();
    }

    public static IReadOnlyList<SettingsFanoutSubscription> BuildFanoutSubscriptions(IBusRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            new SettingsFanoutSubscription(
                SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged,
                endpoint => endpoint.ConfigureConsumer<CryptoSettingsProfileChangedConsumer>(context)),
            new SettingsFanoutSubscription(
                SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged,
                endpoint => endpoint.ConfigureConsumer<CryptoGatewaySettingsProfileChangedConsumer>(context)),
            new SettingsFanoutSubscription(
                SettingsChangeOutboxMessageTypes.CryptoGatewayResilienceSettingsProfileChanged,
                endpoint => endpoint.ConfigureConsumer<CryptoGatewayResilienceSettingsProfileChangedConsumer>(context))
        ];
    }
}
