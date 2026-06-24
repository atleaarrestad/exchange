using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exchange.CryptoTransactions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeBrokeredBuySagaCurrentStateNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE brokered_fiat_crypto_buy_saga_states
                SET current_state = NULL
                WHERE btrim(current_state) = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "current_state",
                table: "brokered_fiat_crypto_buy_saga_states",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE brokered_fiat_crypto_buy_saga_states
                SET current_state = ''
                WHERE current_state IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "current_state",
                table: "brokered_fiat_crypto_buy_saga_states",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
