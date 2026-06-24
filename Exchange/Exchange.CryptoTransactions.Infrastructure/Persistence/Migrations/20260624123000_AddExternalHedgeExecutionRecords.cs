using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CryptoTransactionsDbContext))]
    [Migration("20260624123000_AddExternalHedgeExecutionRecords")]
    public partial class AddExternalHedgeExecutionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_hedge_execution_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    asset_symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quote_currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    executed_quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    executed_unit_price = table.Column<decimal>(type: "numeric", nullable: false),
                    executed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    settled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    settlement_ledger_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_hedge_execution_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_hedge_execution_records_settlement_state",
                table: "external_hedge_execution_records",
                columns: new[] { "settled_at_utc", "executed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_external_hedge_execution_records_external_order",
                table: "external_hedge_execution_records",
                column: "external_order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_hedge_execution_records");
        }
    }
}
