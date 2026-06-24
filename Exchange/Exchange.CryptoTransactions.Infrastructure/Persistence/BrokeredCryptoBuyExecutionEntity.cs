namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class BrokeredCryptoBuyExecutionEntity
{
    public Guid Id { get; set; }
    public string ClientOrderId { get; set; } = string.Empty;
    public string CustomerAccountId { get; set; } = string.Empty;
    public string AssetSymbol { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal InternalFillQuantity { get; set; }
    public decimal ExternalHedgeQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalCost { get; set; }
    public DateTimeOffset ExecutedAtUtc { get; set; }
    public string? ExternalHedgeOrderId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
