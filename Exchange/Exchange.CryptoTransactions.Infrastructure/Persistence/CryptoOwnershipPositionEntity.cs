namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoOwnershipPositionEntity
{
    public string CustomerAccountId { get; set; } = string.Empty;
    public string AssetSymbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
