namespace Exchange.FiatTransactions.Infrastructure.Persistence;

public sealed class BrokeredCryptoBuySettlementEntity
{
    public Guid Id { get; set; }
    public string ClientOrderId { get; set; } = string.Empty;
    public string CustomerAccountId { get; set; } = string.Empty;
    public string FiatCurrency { get; set; } = string.Empty;
    public decimal CustomerDebitAmount { get; set; }
    public DateTimeOffset ExecutedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
