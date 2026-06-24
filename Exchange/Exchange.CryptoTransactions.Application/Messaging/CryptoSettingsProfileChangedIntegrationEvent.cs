namespace Exchange.CryptoTransactions.Application.Messaging;

public sealed record CryptoSettingsProfileChangedIntegrationEvent(
    Guid ProfileId,
    SettingsProfileChangeType ChangeType,
    DateTimeOffset OccurredAtUtc);
