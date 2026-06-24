using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "settings_change_outbox_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    publish_attempt_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings_change_outbox_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_crypto_gateway_settings_profiles_provider_name",
                table: "crypto_gateway_settings_profiles",
                columns: new[] { "provider", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_crypto_settings_profiles_name",
                table: "crypto_settings_profiles",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_settings_change_outbox_entries_publish_state",
                table: "settings_change_outbox_entries",
                columns: new[] { "published_at_utc", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crypto_gateway_settings_profiles");

            migrationBuilder.DropTable(
                name: "crypto_settings_profiles");

            migrationBuilder.DropTable(
                name: "crypto_transfer_idempotency_receipts");

            migrationBuilder.DropTable(
                name: "settings_change_outbox_entries");
        }
    }
}
