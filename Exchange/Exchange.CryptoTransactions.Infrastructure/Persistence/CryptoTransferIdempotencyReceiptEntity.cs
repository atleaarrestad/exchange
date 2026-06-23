namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoTransferIdempotencyReceiptEntity
{
    public required string SourceAccountId { get; set; }
    public required string AssetSymbol { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string ReceiptJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public IdempotencyStatus Status { get; set; }
}
