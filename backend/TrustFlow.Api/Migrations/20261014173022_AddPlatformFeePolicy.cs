using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformFeePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PaidAmount",
                table: "Milestones",
                type: "numeric(38,18)",
                precision: 38,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeePaid",
                table: "Milestones",
                type: "numeric(38,18)",
                precision: 38,
                scale: 18,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReleasedAmount",
                table: "Escrows",
                type: "numeric(38,18)",
                precision: 38,
                scale: 18,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "FeeRecipientAddress",
                table: "Escrows",
                type: "character varying(42)",
                maxLength: 42,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlatformFeeBasisPoints",
                table: "Escrows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeesPaid",
                table: "Escrows",
                type: "numeric(38,18)",
                precision: 38,
                scale: 18,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TokenDecimals",
                table: "Escrows",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReturnedToClient",
                table: "EscrowCases",
                type: "numeric(38,18)",
                precision: 38,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PaidToFreelancer",
                table: "EscrowCases",
                type: "numeric(38,18)",
                precision: 38,
                scale: 18,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeePaid",
                table: "EscrowCases",
                type: "numeric(38,18)",
                precision: 38,
                scale: 18,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlatformFeePaid",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "FeeRecipientAddress",
                table: "Escrows");

            migrationBuilder.DropColumn(
                name: "PlatformFeeBasisPoints",
                table: "Escrows");

            migrationBuilder.DropColumn(
                name: "PlatformFeesPaid",
                table: "Escrows");

            migrationBuilder.DropColumn(
                name: "TokenDecimals",
                table: "Escrows");

            migrationBuilder.DropColumn(
                name: "PlatformFeePaid",
                table: "EscrowCases");

            migrationBuilder.AlterColumn<decimal>(
                name: "PaidAmount",
                table: "Milestones",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,18)",
                oldPrecision: 38,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReleasedAmount",
                table: "Escrows",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,18)",
                oldPrecision: 38,
                oldScale: 18);

            migrationBuilder.AlterColumn<decimal>(
                name: "ReturnedToClient",
                table: "EscrowCases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,18)",
                oldPrecision: 38,
                oldScale: 18,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PaidToFreelancer",
                table: "EscrowCases",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,18)",
                oldPrecision: 38,
                oldScale: 18,
                oldNullable: true);
        }
    }
}
