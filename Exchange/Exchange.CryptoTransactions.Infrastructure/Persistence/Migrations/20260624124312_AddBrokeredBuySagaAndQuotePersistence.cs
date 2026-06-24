using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrokeredBuySagaAndQuotePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brokered_crypto_buy_quotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quote_currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    internal_fill_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    external_hedge_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    market_price_observed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    quoted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    requires_external_hedge = table.Column<bool>(type: "boolean", nullable: false),
                    price_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brokered_crypto_buy_quotes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "brokered_fiat_crypto_buy_saga_states",
                columns: table => new
                {
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    customer_account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    quote_currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    max_unit_price = table.Column<decimal>(type: "numeric", nullable: true),
                    max_total_cost = table.Column<decimal>(type: "numeric", nullable: true),
                    reserved_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    captured_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    failure_reason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brokered_fiat_crypto_buy_saga_states", x => x.correlation_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_brokered_crypto_buy_quotes_expires_at",
                table: "brokered_crypto_buy_quotes",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_brokered_fiat_crypto_buy_saga_states_customer_order",
                table: "brokered_fiat_crypto_buy_saga_states",
                columns: new[] { "customer_account_id", "client_order_id" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brokered_crypto_buy_quotes");

            migrationBuilder.DropTable(
                name: "brokered_fiat_crypto_buy_saga_states");

        }
    }
}
