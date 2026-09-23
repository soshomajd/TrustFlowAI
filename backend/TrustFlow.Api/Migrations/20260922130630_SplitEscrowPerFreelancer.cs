using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class SplitEscrowPerFreelancer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Escrows_ProjectId",
                table: "Escrows");

            migrationBuilder.AddColumn<Guid>(
                name: "FreelancerId",
                table: "Milestones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FreelancerId",
                table: "Escrows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ProposalMilestones",
                columns: table => new
                {
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalMilestones", x => new { x.ProposalId, x.MilestoneId });
                    table.ForeignKey(
                        name: "FK_ProposalMilestones_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProposalMilestones_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_FreelancerId",
                table: "Milestones",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_FreelancerId",
                table: "Escrows",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ProjectId_FreelancerId",
                table: "Escrows",
                columns: new[] { "ProjectId", "FreelancerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProposalMilestones_MilestoneId",
                table: "ProposalMilestones",
                column: "MilestoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_Escrows_AspNetUsers_FreelancerId",
                table: "Escrows",
                column: "FreelancerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Milestones_AspNetUsers_FreelancerId",
                table: "Milestones",
                column: "FreelancerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Escrows_AspNetUsers_FreelancerId",
                table: "Escrows");

            migrationBuilder.DropForeignKey(
                name: "FK_Milestones_AspNetUsers_FreelancerId",
                table: "Milestones");

            migrationBuilder.DropTable(
                name: "ProposalMilestones");

            migrationBuilder.DropIndex(
                name: "IX_Milestones_FreelancerId",
                table: "Milestones");

            migrationBuilder.DropIndex(
                name: "IX_Escrows_FreelancerId",
                table: "Escrows");

            migrationBuilder.DropIndex(
                name: "IX_Escrows_ProjectId_FreelancerId",
                table: "Escrows");

            migrationBuilder.DropColumn(
                name: "FreelancerId",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "FreelancerId",
                table: "Escrows");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ProjectId",
                table: "Escrows",
                column: "ProjectId",
                unique: true);
        }
    }
}
