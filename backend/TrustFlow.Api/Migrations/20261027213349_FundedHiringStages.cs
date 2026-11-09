using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class FundedHiringStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Proposals_ProjectId_FreelancerId",
                table: "Proposals");

            migrationBuilder.AddColumn<int>(
                name: "HiringFlowVersion",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "OnChainIndex",
                table: "Milestones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProtocolVersion",
                table: "Escrows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EscrowFundingStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EscrowId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanOrder = table.Column<int>(type: "integer", nullable: false),
                    ChainIndex = table.Column<int>(type: "integer", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(38,18)", precision: 38, scale: 18, nullable: false),
                    FundingReference = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                    TermsHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                    ReservationActive = table.Column<bool>(type: "boolean", nullable: false),
                    FundingTransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: true),
                    FundedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartTransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscrowFundingStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscrowFundingStages_Escrows_EscrowId",
                        column: x => x.EscrowId,
                        principalTable: "Escrows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EscrowFundingStages_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_ProjectId_FreelancerId",
                table: "Proposals",
                columns: new[] { "ProjectId", "FreelancerId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Accepted', 'AwaitingFunding', 'Funded')");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowFundingStages_EscrowId_ChainIndex",
                table: "EscrowFundingStages",
                columns: new[] { "EscrowId", "ChainIndex" },
                unique: true,
                filter: "\"ChainIndex\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowFundingStages_EscrowId_PlanOrder",
                table: "EscrowFundingStages",
                columns: new[] { "EscrowId", "PlanOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscrowFundingStages_FundingReference",
                table: "EscrowFundingStages",
                column: "FundingReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscrowFundingStages_MilestoneId",
                table: "EscrowFundingStages",
                column: "MilestoneId",
                unique: true,
                filter: "\"ReservationActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EscrowFundingStages");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_ProjectId_FreelancerId",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "HiringFlowVersion",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OnChainIndex",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "ProtocolVersion",
                table: "Escrows");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_ProjectId_FreelancerId",
                table: "Proposals",
                columns: new[] { "ProjectId", "FreelancerId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Accepted')");
        }
    }
}
