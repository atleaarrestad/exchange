using Microsoft.EntityFrameworkCore;

namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoTransactionsDbContext(DbContextOptions<CryptoTransactionsDbContext> options) : DbContext(options)
{
    public DbSet<CryptoTransferIdempotencyReceiptEntity> CryptoTransferIdempotencyReceipts => Set<CryptoTransferIdempotencyReceiptEntity>();
    public DbSet<CryptoSettingsProfileEntity> CryptoSettingsProfiles => Set<CryptoSettingsProfileEntity>();
    public DbSet<CryptoGatewaySettingsProfileEntity> CryptoGatewaySettingsProfiles => Set<CryptoGatewaySettingsProfileEntity>();
    public DbSet<SettingsChangeOutboxEntryEntity> SettingsChangeOutboxEntries => Set<SettingsChangeOutboxEntryEntity>();
    public DbSet<ExternalHedgeBatchEntryEntity> ExternalHedgeBatchEntries => Set<ExternalHedgeBatchEntryEntity>();
    public DbSet<BackgroundWorkerHeartbeatEntity> BackgroundWorkerHeartbeats => Set<BackgroundWorkerHeartbeatEntity>();

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
        receipt.Property(entity => entity.DestinationAddress)
            .HasColumnName("destination_address")
            .HasMaxLength(256);
        receipt.Property(entity => entity.Amount)
            .HasColumnName("amount");
        receipt.Property(entity => entity.NetworkFee)
            .HasColumnName("network_fee");

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

        var settings = modelBuilder.Entity<CryptoSettingsProfileEntity>();
        settings.ToTable("crypto_settings_profiles");
        settings.HasKey(entity => entity.Id);

        settings.Property(entity => entity.Id)
            .HasColumnName("id");

        settings.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(100);

        settings.Property(entity => entity.QuoteTtlSeconds).HasColumnName("quote_ttl_seconds");
        settings.Property(entity => entity.InternalOnlySpreadBasisPoints).HasColumnName("internal_only_spread_basis_points");
        settings.Property(entity => entity.ExternalHedgeSpreadBasisPoints).HasColumnName("external_hedge_spread_basis_points");
        settings.Property(entity => entity.MaxAllowedSlippageBasisPoints).HasColumnName("max_allowed_slippage_basis_points");
        settings.Property(entity => entity.BitcoinReferencePriceNok).HasColumnName("bitcoin_reference_price_nok");
        settings.Property(entity => entity.EtherReferencePriceNok).HasColumnName("ether_reference_price_nok");
        settings.Property(entity => entity.InitialBitcoinInventory).HasColumnName("initial_bitcoin_inventory");
        settings.Property(entity => entity.InitialEtherInventory).HasColumnName("initial_ether_inventory");
        settings.Property(entity => entity.MaxBufferedHedgeCustomerBuys).HasColumnName("max_buffered_hedge_customer_buys");
        settings.Property(entity => entity.MaxBufferedHedgeDelaySeconds).HasColumnName("max_buffered_hedge_delay_seconds");
        settings.Property(entity => entity.TimeoutReconciliationScanIntervalSeconds).HasColumnName("timeout_reconciliation_scan_interval_seconds");
        settings.Property(entity => entity.TimeoutReconciliationStaleAfterSeconds).HasColumnName("timeout_reconciliation_stale_after_seconds");
        settings.Property(entity => entity.SimulationMinLatencyMs).HasColumnName("simulation_min_latency_ms");
        settings.Property(entity => entity.SimulationMaxLatencyMs).HasColumnName("simulation_max_latency_ms");
        settings.Property(entity => entity.SimulationRejectRate).HasColumnName("simulation_reject_rate");
        settings.Property(entity => entity.SimulationTimeoutRate).HasColumnName("simulation_timeout_rate");
        settings.Property(entity => entity.SimulationDefaultBitcoinAvailableBalance).HasColumnName("simulation_default_bitcoin_available_balance");
        settings.Property(entity => entity.SimulationDefaultEtherAvailableBalance).HasColumnName("simulation_default_ether_available_balance");
        settings.Property(entity => entity.CreatedAtUtc).HasColumnName("created_at_utc");
        settings.Property(entity => entity.UpdatedAtUtc).HasColumnName("updated_at_utc");

        settings.HasIndex(entity => entity.Name)
            .HasDatabaseName("ix_crypto_settings_profiles_name");

        var gatewaySettings = modelBuilder.Entity<CryptoGatewaySettingsProfileEntity>();
        gatewaySettings.ToTable("crypto_gateway_settings_profiles");
        gatewaySettings.HasKey(entity => entity.Id);

        gatewaySettings.Property(entity => entity.Id).HasColumnName("id");
        gatewaySettings.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(100);
        gatewaySettings.Property(entity => entity.Provider).HasColumnName("provider").HasMaxLength(32);
        gatewaySettings.Property(entity => entity.Enabled).HasColumnName("enabled");
        gatewaySettings.Property(entity => entity.BaseUrl).HasColumnName("base_url");
        gatewaySettings.Property(entity => entity.HttpTimeoutSeconds).HasColumnName("http_timeout_seconds");
        gatewaySettings.Property(entity => entity.ApiKey).HasColumnName("api_key");
        gatewaySettings.Property(entity => entity.ApiSecret).HasColumnName("api_secret");
        gatewaySettings.Property(entity => entity.ProviderSettingsJson).HasColumnName("provider_settings_json");
        gatewaySettings.Property(entity => entity.CreatedAtUtc).HasColumnName("created_at_utc");
        gatewaySettings.Property(entity => entity.UpdatedAtUtc).HasColumnName("updated_at_utc");

        gatewaySettings.HasIndex(entity => new { entity.Provider, entity.Name })
            .HasDatabaseName("ix_crypto_gateway_settings_profiles_provider_name");

        var outboxEntries = modelBuilder.Entity<SettingsChangeOutboxEntryEntity>();
        outboxEntries.ToTable("settings_change_outbox_entries");
        outboxEntries.HasKey(entity => entity.Id);
        outboxEntries.Property(entity => entity.Id).HasColumnName("id");
        outboxEntries.Property(entity => entity.MessageType).HasColumnName("message_type").HasMaxLength(200);
        outboxEntries.Property(entity => entity.PayloadJson).HasColumnName("payload_json");
        outboxEntries.Property(entity => entity.CreatedAtUtc).HasColumnName("created_at_utc");
        outboxEntries.Property(entity => entity.PublishedAtUtc).HasColumnName("published_at_utc");
        outboxEntries.Property(entity => entity.PublishAttemptCount).HasColumnName("publish_attempt_count");
        outboxEntries.Property(entity => entity.LeaseOwnerId).HasColumnName("lease_owner_id").HasMaxLength(128);
        outboxEntries.Property(entity => entity.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
        outboxEntries.Property(entity => entity.LeaseToken).HasColumnName("lease_token");

        outboxEntries.HasIndex(entity => new { entity.PublishedAtUtc, entity.CreatedAtUtc })
            .HasDatabaseName("ix_settings_change_outbox_entries_publish_state");
        outboxEntries.HasIndex(entity => new { entity.PublishedAtUtc, entity.LeaseExpiresAtUtc, entity.CreatedAtUtc })
            .HasDatabaseName("ix_settings_change_outbox_entries_lease_state");

        var externalHedgeBatchEntries = modelBuilder.Entity<ExternalHedgeBatchEntryEntity>();
        externalHedgeBatchEntries.ToTable("external_hedge_batch_entries");
        externalHedgeBatchEntries.HasKey(entity => entity.Id);
        externalHedgeBatchEntries.Property(entity => entity.Id).HasColumnName("id");
        externalHedgeBatchEntries.Property(entity => entity.CustomerAccountId).HasColumnName("customer_account_id").HasMaxLength(64);
        externalHedgeBatchEntries.Property(entity => entity.ClientOrderId).HasColumnName("client_order_id").HasMaxLength(128);
        externalHedgeBatchEntries.Property(entity => entity.AssetSymbol).HasColumnName("asset_symbol").HasMaxLength(16);
        externalHedgeBatchEntries.Property(entity => entity.QuoteCurrency).HasColumnName("quote_currency").HasMaxLength(16);
        externalHedgeBatchEntries.Property(entity => entity.Quantity).HasColumnName("quantity");
        externalHedgeBatchEntries.Property(entity => entity.RequestedAtUtc).HasColumnName("requested_at_utc");
        externalHedgeBatchEntries.Property(entity => entity.ExecutedAtUtc).HasColumnName("executed_at_utc");
        externalHedgeBatchEntries.Property(entity => entity.ExecutedExternalOrderId).HasColumnName("executed_external_order_id").HasMaxLength(128);
        externalHedgeBatchEntries.Property(entity => entity.LeaseOwnerId).HasColumnName("lease_owner_id").HasMaxLength(128);
        externalHedgeBatchEntries.Property(entity => entity.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
        externalHedgeBatchEntries.Property(entity => entity.LeaseToken).HasColumnName("lease_token");

        externalHedgeBatchEntries.HasIndex(entity => new { entity.CustomerAccountId, entity.ClientOrderId })
            .IsUnique()
            .HasDatabaseName("ux_external_hedge_batch_entries_customer_order");
        externalHedgeBatchEntries.HasIndex(entity => new { entity.ExecutedAtUtc, entity.LeaseExpiresAtUtc, entity.AssetSymbol, entity.QuoteCurrency, entity.RequestedAtUtc })
            .HasDatabaseName("ix_external_hedge_batch_entries_due_lookup");
        externalHedgeBatchEntries.HasIndex(entity => entity.LeaseToken)
            .HasDatabaseName("ix_external_hedge_batch_entries_lease_token");

        var workerHeartbeats = modelBuilder.Entity<BackgroundWorkerHeartbeatEntity>();
        workerHeartbeats.ToTable("background_worker_heartbeats");
        workerHeartbeats.HasKey(entity => entity.WorkerName);
        workerHeartbeats.Property(entity => entity.WorkerName).HasColumnName("worker_name").HasMaxLength(128);
        workerHeartbeats.Property(entity => entity.LastSeenAtUtc).HasColumnName("last_seen_at_utc");
    }
}
