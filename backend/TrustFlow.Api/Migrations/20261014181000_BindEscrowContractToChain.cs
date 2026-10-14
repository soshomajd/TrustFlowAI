using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class BindEscrowContractToChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Escrows_ContractAddress",
                table: "Escrows");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ChainId_ContractAddress",
                table: "Escrows",
                columns: new[] { "ChainId", "ContractAddress" },
                unique: true,
                filter: "\"ContractAddress\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Escrows_ChainId_ContractAddress",
                table: "Escrows");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ContractAddress",
                table: "Escrows",
                column: "ContractAddress",
                unique: true,
                filter: "\"ContractAddress\" IS NOT NULL");
        }
    }
}
