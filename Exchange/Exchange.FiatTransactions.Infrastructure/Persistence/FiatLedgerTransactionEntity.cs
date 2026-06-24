namespace Exchange.FiatTransactions.Infrastructure.Persistence;

public sealed class FiatLedgerTransactionEntity
{
    public Guid Id { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public DateTimeOffset ExecutedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
