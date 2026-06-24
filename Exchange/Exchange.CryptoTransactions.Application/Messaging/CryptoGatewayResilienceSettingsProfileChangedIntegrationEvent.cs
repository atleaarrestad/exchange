namespace Exchange.CryptoTransactions.Application.Messaging;

public sealed record CryptoGatewayResilienceSettingsProfileChangedIntegrationEvent(
    Guid ProfileId,
    SettingsProfileChangeType ChangeType,
    DateTimeOffset OccurredAtUtc);
