namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class PlatformInventoryPositionEntity
{
    public string AssetSymbol { get; set; } = string.Empty;
    public decimal AvailableQuantity { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
