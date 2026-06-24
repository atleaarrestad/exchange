using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCryptoGatewayResilienceSettingsProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "ix_crypto_gateway_resilience_settings_profiles_name",
                table: "crypto_gateway_resilience_settings_profiles",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crypto_gateway_resilience_settings_profiles");
        }
    }
}
