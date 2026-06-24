using System.Text.Json;
using Exchange.CryptoTransactions.Application.Messaging;
using Exchange.CryptoTransactions.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;

namespace Tests;

[TestClass]
public sealed class SettingsChangeOutboxMessagingTests
{
    [TestMethod]
    public void ResolveMessageType_ForKnownPayload_ReturnsConfiguredMessageType()
    {
        Assert.AreEqual(
            SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged,
            SettingsChangeOutboxMessageRegistry.ResolveMessageType<CryptoSettingsProfileChangedIntegrationEvent>());
        Assert.AreEqual(
            SettingsChangeOutboxMessageTypes.CryptoGatewaySettingsProfileChanged,
            SettingsChangeOutboxMessageRegistry.ResolveMessageType<CryptoGatewaySettingsProfileChangedIntegrationEvent>());
        Assert.AreEqual(
            SettingsChangeOutboxMessageTypes.CryptoGatewayResilienceSettingsProfileChanged,
            SettingsChangeOutboxMessageRegistry.ResolveMessageType<CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent>());
    }

    [TestMethod]
    public void DeserializeMessage_ForKnownMessageType_RoundTripsPayload()
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        var payload = new CryptoSettingsProfileChangedIntegrationEvent(
            Guid.CreateVersion7(),
            SettingsProfileChangeType.Updated,
            createdAtUtc);
        var payloadJson = JsonSerializer.Serialize(payload);

        var deserialized = SettingsChangeOutboxMessageRegistry.DeserializeMessage(
            SettingsChangeOutboxMessageTypes.CryptoSettingsProfileChanged,
            payloadJson);

        Assert.IsInstanceOfType<CryptoSettingsProfileChangedIntegrationEvent>(deserialized);
        var typed = (CryptoSettingsProfileChangedIntegrationEvent)deserialized;
        Assert.AreEqual(payload.ProfileId, typed.ProfileId);
        Assert.AreEqual(payload.ChangeType, typed.ChangeType);
        Assert.AreEqual(payload.OccurredAtUtc, typed.OccurredAtUtc);
    }

    [TestMethod]
    public void ResolveMessageType_ForUnknownPayload_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SettingsChangeOutboxMessageRegistry.ResolveMessageType<UnknownSettingsPayload>());
    }

    [TestMethod]
    public void OutboxOptions_FromConfiguration_ThrowsWhenMaxPublishAttemptsIsInvalid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CryptoTransactions:SettingsChangeOutbox:MaxPublishAttempts"] = "0"
            })
            .Build();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SettingsChangeOutboxPublisherOptions.FromConfiguration(configuration));
    }

    private sealed record UnknownSettingsPayload(Guid ProfileId);
}
