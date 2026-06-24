using MassTransit;

namespace Exchange.Infrastructure.Messaging;

public sealed record SettingsFanoutSubscription(
    string MessageTopic,
    Action<IReceiveEndpointConfigurator> ConfigureEndpoint);

public static class SettingsFanoutEndpointRegistration
{
    public static void ConfigureFanoutEndpoints(
        Action<string, Action<IReceiveEndpointConfigurator>> receiveEndpoint,
        IEnumerable<SettingsFanoutSubscription> subscriptions,
        string instanceId)
    {
        ArgumentNullException.ThrowIfNull(receiveEndpoint);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        foreach (var subscription in subscriptions)
        {
            ArgumentNullException.ThrowIfNull(subscription);
            var endpointName = SettingsFanoutEndpointNameFactory.BuildSubscriberEndpointName(
                subscription.MessageTopic,
                instanceId);

            receiveEndpoint(endpointName, subscription.ConfigureEndpoint);
        }
    }
}
