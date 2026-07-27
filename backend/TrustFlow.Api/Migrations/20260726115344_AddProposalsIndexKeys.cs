using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalsIndexKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Proposals_ProjectId",
                table: "Proposals");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_ProjectId_FreelancerId",
                table: "Proposals",
                columns: new[] { "ProjectId", "FreelancerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Proposals_ProjectId_FreelancerId",
                table: "Proposals");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_ProjectId",
                table: "Proposals",
                column: "ProjectId");
        }
    }
}
