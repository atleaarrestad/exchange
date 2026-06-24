namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class ExternalHedgeExecutionRecordEntity
{
    public Guid Id { get; set; }
    public string ExternalOrderId { get; set; } = string.Empty;
    public string AssetSymbol { get; set; } = string.Empty;
    public string QuoteCurrency { get; set; } = string.Empty;
    public decimal ExecutedQuantity { get; set; }
    public decimal ExecutedUnitPrice { get; set; }
    public DateTimeOffset ExecutedAtUtc { get; set; }
    public DateTimeOffset? SettledAtUtc { get; set; }
    public Guid? SettlementLedgerTransactionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
