namespace Exchange.FiatTransactions.Infrastructure.Persistence;

public sealed class FiatBalancePositionEntity
{
    public string FiatCurrency { get; set; } = string.Empty;
    public string AccountKind { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public decimal AvailableAmount { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
