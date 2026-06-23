using System;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Exchange.CryptoTransactions.Infrastructure.Migrations;

[DbContext(typeof(CryptoTransactionsDbContext))]
public partial class CryptoTransactionsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("Exchange.CryptoTransactions.Infrastructure.Persistence.CryptoTransferIdempotencyReceiptEntity", entity =>
        {
            entity.Property<string>("SourceAccountId")
                .HasColumnType("TEXT")
                .HasMaxLength(64)
                .HasColumnName("source_account_id");

            entity.Property<string>("AssetSymbol")
                .HasColumnType("TEXT")
                .HasMaxLength(16)
                .HasColumnName("asset_symbol");

            entity.Property<string>("IdempotencyKey")
                .HasColumnType("TEXT")
                .HasMaxLength(128)
                .HasColumnName("idempotency_key");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .IsConcurrencyToken()
                .HasColumnType("TEXT")
                .HasColumnName("created_at_utc");

            entity.Property<string>("ReceiptJson")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("receipt_json");

            entity.Property<string>("RequestFingerprint")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasMaxLength(64)
                .HasColumnName("request_fingerprint");

            entity.Property<int>("Status")
                .IsConcurrencyToken()
                .HasColumnType("INTEGER")
                .HasColumnName("status");

            entity.Property<decimal>("TotalDebit")
                .HasColumnType("TEXT")
                .HasColumnName("total_debit");

            entity.HasKey("SourceAccountId", "AssetSymbol", "IdempotencyKey");

            entity.ToTable("crypto_transfer_idempotency_receipts");
        });
#pragma warning restore 612, 618
    }
}
