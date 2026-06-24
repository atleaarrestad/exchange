namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoGatewaySettingsProfileEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public int HttpTimeoutSeconds { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string ProviderSettingsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
