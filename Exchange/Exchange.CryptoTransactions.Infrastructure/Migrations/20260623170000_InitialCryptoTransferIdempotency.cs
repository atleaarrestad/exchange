using System;
using Exchange.CryptoTransactions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Exchange.CryptoTransactions.Infrastructure.Migrations;

[DbContext(typeof(CryptoTransactionsDbContext))]
[Migration("20260623170000_InitialCryptoTransferIdempotency")]
public partial class InitialCryptoTransferIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "crypto_transfer_idempotency_receipts",
            columns: table => new
            {
                source_account_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                asset_symbol = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                idempotency_key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                request_fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                total_debit = table.Column<decimal>(type: "TEXT", nullable: false),
                receipt_json = table.Column<string>(type: "TEXT", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                last_updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                status = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_crypto_transfer_idempotency_receipts",
                    entity => new { entity.source_account_id, entity.asset_symbol, entity.idempotency_key });
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "crypto_transfer_idempotency_receipts");
    }
}
