using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.FiatTransactions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFiatTransactionsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brokered_crypto_buy_settlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    customer_account_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fiat_currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    customer_debit_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    executed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brokered_crypto_buy_settlements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fiat_balance_positions",
                columns: table => new
                {
                    fiat_currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    available_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiat_balance_positions", x => new { x.fiat_currency, x.account_kind, x.account_id });
                });

            migrationBuilder.CreateTable(
                name: "fiat_ledger_transactions",
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
                    table.PrimaryKey("PK_fiat_ledger_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fiat_ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    fiat_currency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    account_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiat_ledger_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_fiat_ledger_entries_fiat_ledger_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "fiat_ledger_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_brokered_crypto_buy_settlements_customer_order",
                table: "brokered_crypto_buy_settlements",
                columns: new[] { "customer_account_id", "client_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fiat_ledger_entries_transaction_sequence",
                table: "fiat_ledger_entries",
                columns: new[] { "transaction_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fiat_ledger_transactions_operation",
                table: "fiat_ledger_transactions",
                columns: new[] { "operation_type", "operation_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brokered_crypto_buy_settlements");

            migrationBuilder.DropTable(
                name: "fiat_balance_positions");

            migrationBuilder.DropTable(
                name: "fiat_ledger_entries");

            migrationBuilder.DropTable(
                name: "fiat_ledger_transactions");
        }
    }
}
