using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferPayloadAndWorkerHeartbeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "destination_address",
                table: "crypto_transfer_idempotency_receipts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                table: "crypto_transfer_idempotency_receipts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "network_fee",
                table: "crypto_transfer_idempotency_receipts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_worker_heartbeats");

            migrationBuilder.DropColumn(
                name: "destination_address",
                table: "crypto_transfer_idempotency_receipts");

            migrationBuilder.DropColumn(
                name: "amount",
                table: "crypto_transfer_idempotency_receipts");

            migrationBuilder.DropColumn(
                name: "network_fee",
                table: "crypto_transfer_idempotency_receipts");
        }
    }
}
