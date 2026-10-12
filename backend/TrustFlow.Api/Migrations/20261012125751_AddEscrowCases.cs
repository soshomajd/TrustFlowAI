using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEscrowCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Milestones",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArbiterWalletAddress",
                table: "Escrows",
                type: "character varying(42)",
                maxLength: 42,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EscrowCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EscrowId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaidToFreelancer = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ReturnedToClient = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    EscrowEnded = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewNoteAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AdviceJson = table.Column<string>(type: "text", nullable: true),
                    AdviceFreelancerSharePercent = table.Column<int>(type: "integer", nullable: true),
                    AdviceConfidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true),
                    AdviceAiRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdviceAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AdviceStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscrowCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscrowCases_Escrows_EscrowId",
                        column: x => x.EscrowId,
                        principalTable: "Escrows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EscrowCases_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EscrowCases_MilestoneId",
                table: "EscrowCases",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowCases_Open_EscrowId",
                table: "EscrowCases",
                column: "EscrowId",
                unique: true,
                filter: "\"Status\" = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_EscrowCases_Status_OpenedAt",
                table: "EscrowCases",
                columns: new[] { "Status", "OpenedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EscrowCases");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "ArbiterWalletAddress",
                table: "Escrows");
        }
    }
}
