namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class BrokeredCryptoBuyQuoteEntity
{
    public Guid Id { get; set; }
    public string CustomerAccountId { get; set; } = string.Empty;
    public string AssetSymbol { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal InternalFillQuantity { get; set; }
    public decimal ExternalHedgeQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalCost { get; set; }
    public DateTimeOffset MarketPriceObservedAtUtc { get; set; }
    public DateTimeOffset QuotedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool RequiresExternalHedge { get; set; }
    public string PriceSource { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
