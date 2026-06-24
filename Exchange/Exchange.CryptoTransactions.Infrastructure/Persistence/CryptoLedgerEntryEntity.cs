namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoLedgerEntryEntity
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public int Sequence { get; set; }
    public string AssetSymbol { get; set; } = string.Empty;
    public string AccountKind { get; set; } = string.Empty;
    public string? AccountId { get; set; }
    public CryptoLedgerEntryDirection Direction { get; set; }
    public decimal Quantity { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
