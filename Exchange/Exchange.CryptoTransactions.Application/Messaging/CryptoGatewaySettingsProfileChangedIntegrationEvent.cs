namespace Exchange.CryptoTransactions.Application.Messaging;

public sealed record CryptoGatewaySettingsProfileChangedIntegrationEvent(
    Guid ProfileId,
    SettingsProfileChangeType ChangeType,
    DateTimeOffset OccurredAtUtc);
