using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxLeasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at_utc",
                table: "settings_change_outbox_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lease_owner_id",
                table: "settings_change_outbox_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_token",
                table: "settings_change_outbox_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_settings_change_outbox_entries_lease_state",
                table: "settings_change_outbox_entries",
                columns: new[] { "published_at_utc", "lease_expires_at_utc", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_settings_change_outbox_entries_lease_state",
                table: "settings_change_outbox_entries");

            migrationBuilder.DropColumn(
                name: "lease_expires_at_utc",
                table: "settings_change_outbox_entries");

            migrationBuilder.DropColumn(
                name: "lease_owner_id",
                table: "settings_change_outbox_entries");

            migrationBuilder.DropColumn(
                name: "lease_token",
                table: "settings_change_outbox_entries");
        }
    }
}
