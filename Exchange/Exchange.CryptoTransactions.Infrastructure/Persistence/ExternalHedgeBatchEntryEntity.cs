namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class ExternalHedgeBatchEntryEntity
{
    public Guid Id { get; set; }
    public string CustomerAccountId { get; set; } = string.Empty;
    public string ClientOrderId { get; set; } = string.Empty;
    public string AssetSymbol { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? ExecutedAtUtc { get; set; }
    public string? ExecutedExternalOrderId { get; set; }
    public string? LeaseOwnerId { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
}
