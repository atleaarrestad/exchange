using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalHedgeBatchQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_hedge_batch_entries");
        }
    }
}
