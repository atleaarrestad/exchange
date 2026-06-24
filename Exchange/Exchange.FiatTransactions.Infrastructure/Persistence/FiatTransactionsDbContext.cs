using Microsoft.EntityFrameworkCore;

namespace Exchange.FiatTransactions.Infrastructure.Persistence;

public sealed class FiatTransactionsDbContext(DbContextOptions<FiatTransactionsDbContext> options) : DbContext(options)
{
    public DbSet<FiatBalancePositionEntity> FiatBalancePositions => Set<FiatBalancePositionEntity>();
    public DbSet<BrokeredCryptoBuySettlementEntity> BrokeredCryptoBuySettlements => Set<BrokeredCryptoBuySettlementEntity>();
    public DbSet<FiatLedgerTransactionEntity> FiatLedgerTransactions => Set<FiatLedgerTransactionEntity>();
    public DbSet<FiatLedgerEntryEntity> FiatLedgerEntries => Set<FiatLedgerEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var balancePositions = modelBuilder.Entity<FiatBalancePositionEntity>();
        balancePositions.ToTable("fiat_balance_positions");
        balancePositions.HasKey(entity => new { entity.FiatCurrency, entity.AccountKind, entity.AccountId });
        balancePositions.Property(entity => entity.FiatCurrency).HasColumnName("fiat_currency").HasMaxLength(16);
        balancePositions.Property(entity => entity.AccountKind).HasColumnName("account_kind").HasMaxLength(64);
        balancePositions.Property(entity => entity.AccountId).HasColumnName("account_id").HasMaxLength(128);
        balancePositions.Property(entity => entity.AvailableAmount).HasColumnName("available_amount");
        balancePositions.Property(entity => entity.UpdatedAtUtc).HasColumnName("updated_at_utc");

        var settlements = modelBuilder.Entity<BrokeredCryptoBuySettlementEntity>();
        settlements.ToTable("brokered_crypto_buy_settlements");
        settlements.HasKey(entity => entity.Id);
        settlements.Property(entity => entity.Id).HasColumnName("id");
        settlements.Property(entity => entity.ClientOrderId).HasColumnName("client_order_id").HasMaxLength(128);
        settlements.Property(entity => entity.CustomerAccountId).HasColumnName("customer_account_id").HasMaxLength(64);
        settlements.Property(entity => entity.FiatCurrency).HasColumnName("fiat_currency").HasMaxLength(16);
        settlements.Property(entity => entity.CustomerDebitAmount).HasColumnName("customer_debit_amount");
        settlements.Property(entity => entity.ExecutedAtUtc).HasColumnName("executed_at_utc");
        settlements.Property(entity => entity.CreatedAtUtc).HasColumnName("created_at_utc");
        settlements.HasIndex(entity => new { entity.CustomerAccountId, entity.ClientOrderId })
            .IsUnique()
            .HasDatabaseName("ux_brokered_crypto_buy_settlements_customer_order");

        var ledgerTransactions = modelBuilder.Entity<FiatLedgerTransactionEntity>();
        ledgerTransactions.ToTable("fiat_ledger_transactions");
        ledgerTransactions.HasKey(entity => entity.Id);
        ledgerTransactions.Property(entity => entity.Id).HasColumnName("id");
        ledgerTransactions.Property(entity => entity.OperationType).HasColumnName("operation_type").HasMaxLength(64);
        ledgerTransactions.Property(entity => entity.OperationId).HasColumnName("operation_id").HasMaxLength(256);
        ledgerTransactions.Property(entity => entity.ExecutedAtUtc).HasColumnName("executed_at_utc");
        ledgerTransactions.Property(entity => entity.CreatedAtUtc).HasColumnName("created_at_utc");
        ledgerTransactions.HasIndex(entity => new { entity.OperationType, entity.OperationId })
            .IsUnique()
            .HasDatabaseName("ux_fiat_ledger_transactions_operation");

        var ledgerEntries = modelBuilder.Entity<FiatLedgerEntryEntity>();
        ledgerEntries.ToTable("fiat_ledger_entries");
        ledgerEntries.HasKey(entity => entity.Id);
        ledgerEntries.Property(entity => entity.Id).HasColumnName("id");
        ledgerEntries.Property(entity => entity.TransactionId).HasColumnName("transaction_id");
        ledgerEntries.Property(entity => entity.Sequence).HasColumnName("sequence");
        ledgerEntries.Property(entity => entity.FiatCurrency).HasColumnName("fiat_currency").HasMaxLength(16);
        ledgerEntries.Property(entity => entity.AccountKind).HasColumnName("account_kind").HasMaxLength(64);
        ledgerEntries.Property(entity => entity.AccountId).HasColumnName("account_id").HasMaxLength(128);
        ledgerEntries.Property(entity => entity.Direction).HasColumnName("direction").HasConversion<int>();
        ledgerEntries.Property(entity => entity.Amount).HasColumnName("amount");
        ledgerEntries.Property(entity => entity.CreatedAtUtc).HasColumnName("created_at_utc");
        ledgerEntries.HasIndex(entity => new { entity.TransactionId, entity.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_fiat_ledger_entries_transaction_sequence");
        ledgerEntries.HasOne<FiatLedgerTransactionEntity>()
            .WithMany()
            .HasForeignKey(entity => entity.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
