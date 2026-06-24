namespace Exchange.FiatTransactions.Infrastructure.Persistence;

public sealed class FiatLedgerEntryEntity
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public int Sequence { get; set; }
    public string FiatCurrency { get; set; } = string.Empty;
    public string AccountKind { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public FiatLedgerEntryDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
