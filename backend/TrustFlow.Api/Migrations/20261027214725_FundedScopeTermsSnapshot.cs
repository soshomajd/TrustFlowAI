using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class FundedScopeTermsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TermsJson",
                table: "EscrowFundingStages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "EscrowFundingStages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TermsJson",
                table: "EscrowFundingStages");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "EscrowFundingStages");
        }
    }
}
