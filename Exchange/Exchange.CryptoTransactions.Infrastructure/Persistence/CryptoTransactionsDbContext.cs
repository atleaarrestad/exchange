using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoTransactionsDbContext(DbContextOptions<CryptoTransactionsDbContext> options) : DbContext(options)
{
    public DbSet<CryptoTransferIdempotencyReceiptEntity> CryptoTransferIdempotencyReceipts => Set<CryptoTransferIdempotencyReceiptEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var receipt = modelBuilder.Entity<CryptoTransferIdempotencyReceiptEntity>();
        receipt.ToTable("crypto_transfer_idempotency_receipts");

        receipt.HasKey(entity => new { entity.SourceAccountId, entity.AssetSymbol, entity.IdempotencyKey });

        receipt.Property(entity => entity.SourceAccountId)
            .HasColumnName("source_account_id")
            .HasMaxLength(64);

        receipt.Property(entity => entity.AssetSymbol)
            .HasColumnName("asset_symbol")
            .HasMaxLength(16);

        receipt.Property(entity => entity.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(128);

        receipt.Property(entity => entity.RequestFingerprint)
            .HasColumnName("request_fingerprint")
            .HasMaxLength(64);

        receipt.Property(entity => entity.TotalDebit)
            .HasColumnName("total_debit");

        receipt.Property(entity => entity.ReceiptJson)
            .HasColumnName("receipt_json");

        receipt.Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsConcurrencyToken();

        receipt.Property(entity => entity.LastUpdatedAtUtc)
            .HasColumnName("last_updated_at_utc")
            .IsConcurrencyToken();

        receipt.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsConcurrencyToken();
    }
}
