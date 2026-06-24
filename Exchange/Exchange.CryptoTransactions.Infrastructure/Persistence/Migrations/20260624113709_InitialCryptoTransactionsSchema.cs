using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCryptoTransactionsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "background_worker_heartbeats",
                columns: table => new
                {
                    worker_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    last_seen_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_worker_heartbeats", x => x.worker_name);
                });

            migrationBuilder.CreateTable(
                name: "brokered_crypto_buy_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    customer_account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quote_currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    internal_fill_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    external_hedge_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    executed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    external_hedge_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brokered_crypto_buy_executions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cron_job_execution_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    runner_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    scheduled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    result_message = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cron_job_execution_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cron_job_states",
                columns: table => new
                {
                    job_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    cron_expression = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    next_run_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_run_status = table.Column<int>(type: "integer", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    lease_owner_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cron_job_states", x => x.job_name);
                });

            migrationBuilder.CreateTable(
                name: "crypto_gateway_resilience_settings_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    operation_timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    retry_delay_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    failure_ratio = table.Column<double>(type: "double precision", nullable: false),
                    minimum_throughput = table.Column<int>(type: "integer", nullable: false),
                    sampling_duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    break_duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    max_parallelization = table.Column<int>(type: "integer", nullable: false),
                    max_queueing_actions = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crypto_gateway_resilience_settings_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "crypto_gateway_settings_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: false),
                    http_timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    api_key = table.Column<string>(type: "text", nullable: true),
                    api_secret = table.Column<string>(type: "text", nullable: true),
                    provider_settings_json = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crypto_gateway_settings_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "crypto_ledger_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    executed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crypto_ledger_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "crypto_ownership_positions",
                columns: table => new
                {
                    customer_account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crypto_ownership_positions", x => new { x.customer_account_id, x.asset_symbol });
                });

            migrationBuilder.CreateTable(
                name: "crypto_settings_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quote_ttl_seconds = table.Column<int>(type: "integer", nullable: false),
                    internal_only_spread_basis_points = table.Column<decimal>(type: "numeric", nullable: false),
                    external_hedge_spread_basis_points = table.Column<decimal>(type: "numeric", nullable: false),
                    max_allowed_slippage_basis_points = table.Column<decimal>(type: "numeric", nullable: false),
                    bitcoin_reference_price_nok = table.Column<decimal>(type: "numeric", nullable: false),
                    ether_reference_price_nok = table.Column<decimal>(type: "numeric", nullable: false),
                    initial_bitcoin_inventory = table.Column<decimal>(type: "numeric", nullable: false),
                    initial_ether_inventory = table.Column<decimal>(type: "numeric", nullable: false),
                    max_buffered_hedge_customer_buys = table.Column<int>(type: "integer", nullable: false),
                    max_buffered_hedge_delay_seconds = table.Column<int>(type: "integer", nullable: false),
                    timeout_reconciliation_scan_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    timeout_reconciliation_stale_after_seconds = table.Column<int>(type: "integer", nullable: false),
                    simulation_min_latency_ms = table.Column<int>(type: "integer", nullable: false),
                    simulation_max_latency_ms = table.Column<int>(type: "integer", nullable: false),
                    simulation_reject_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    simulation_timeout_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    simulation_default_bitcoin_available_balance = table.Column<decimal>(type: "numeric", nullable: false),
                    simulation_default_ether_available_balance = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crypto_settings_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "crypto_transfer_idempotency_receipts",
                columns: table => new
                {
                    source_account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    total_debit = table.Column<decimal>(type: "numeric", nullable: false),
                    destination_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    network_fee = table.Column<decimal>(type: "numeric", nullable: false),
                    receipt_json = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crypto_transfer_idempotency_receipts", x => new { x.source_account_id, x.asset_symbol, x.idempotency_key });
                });

            migrationBuilder.CreateTable(
                name: "external_hedge_batch_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quote_currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    executed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    executed_external_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lease_owner_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_hedge_batch_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_inventory_positions",
                columns: table => new
                {
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    available_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_inventory_positions", x => x.asset_symbol);
                });

            migrationBuilder.Sql("""
                INSERT INTO platform_inventory_positions (asset_symbol, available_quantity, updated_at_utc)
                VALUES ('BTC', 2, CURRENT_TIMESTAMP),
                       ('ETH', 25, CURRENT_TIMESTAMP)
                ON CONFLICT (asset_symbol) DO NOTHING;
                """);

            migrationBuilder.CreateTable(
                name: "settings_change_outbox_archive_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publish_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings_change_outbox_archive_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings_change_outbox_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    publish_attempt_count = table.Column<int>(type: "integer", nullable: false),
                    lease_owner_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings_change_outbox_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "crypto_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crypto_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_crypto_ledger_entries_crypto_ledger_transactions_transactio~",
                        column: x => x.transaction_id,
                        principalTable: "crypto_ledger_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_brokered_crypto_buy_executions_customer_asset_order",
                table: "brokered_crypto_buy_executions",
                columns: new[] { "customer_account_id", "asset_symbol", "client_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cron_job_execution_records_job_started",
                table: "cron_job_execution_records",
                columns: new[] { "job_name", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_cron_job_states_due_lease",
                table: "cron_job_states",
                columns: new[] { "next_run_at_utc", "lease_expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_crypto_gateway_resilience_settings_profiles_name",
                table: "crypto_gateway_resilience_settings_profiles",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_crypto_gateway_settings_profiles_provider_name",
                table: "crypto_gateway_settings_profiles",
                columns: new[] { "provider", "name" });

            migrationBuilder.CreateIndex(
                name: "ux_crypto_ledger_entries_transaction_sequence",
                table: "crypto_ledger_entries",
                columns: new[] { "transaction_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_crypto_ledger_transactions_operation",
                table: "crypto_ledger_transactions",
                columns: new[] { "operation_type", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_crypto_settings_profiles_name",
                table: "crypto_settings_profiles",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_external_hedge_batch_entries_due_lookup",
                table: "external_hedge_batch_entries",
                columns: new[] { "executed_at_utc", "lease_expires_at_utc", "asset_symbol", "quote_currency", "requested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_external_hedge_batch_entries_lease_token",
                table: "external_hedge_batch_entries",
                column: "lease_token");

            migrationBuilder.CreateIndex(
                name: "ux_external_hedge_batch_entries_customer_order",
                table: "external_hedge_batch_entries",
                columns: new[] { "customer_account_id", "client_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_settings_change_outbox_archive_entries_published_archived",
                table: "settings_change_outbox_archive_entries",
                columns: new[] { "published_at_utc", "archived_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_settings_change_outbox_entries_lease_state",
                table: "settings_change_outbox_entries",
                columns: new[] { "published_at_utc", "lease_expires_at_utc", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_settings_change_outbox_entries_publish_state",
                table: "settings_change_outbox_entries",
                columns: new[] { "published_at_utc", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_worker_heartbeats");

            migrationBuilder.DropTable(
                name: "brokered_crypto_buy_executions");

            migrationBuilder.DropTable(
                name: "cron_job_execution_records");

            migrationBuilder.DropTable(
                name: "cron_job_states");

            migrationBuilder.DropTable(
                name: "crypto_gateway_resilience_settings_profiles");

            migrationBuilder.DropTable(
                name: "crypto_gateway_settings_profiles");

            migrationBuilder.DropTable(
                name: "crypto_ledger_entries");

            migrationBuilder.DropTable(
                name: "crypto_ownership_positions");

            migrationBuilder.DropTable(
                name: "crypto_settings_profiles");

            migrationBuilder.DropTable(
                name: "crypto_transfer_idempotency_receipts");

            migrationBuilder.DropTable(
                name: "external_hedge_batch_entries");

            migrationBuilder.DropTable(
                name: "platform_inventory_positions");

            migrationBuilder.DropTable(
                name: "settings_change_outbox_archive_entries");

            migrationBuilder.DropTable(
                name: "settings_change_outbox_entries");

            migrationBuilder.DropTable(
                name: "crypto_ledger_transactions");
        }
    }
}
