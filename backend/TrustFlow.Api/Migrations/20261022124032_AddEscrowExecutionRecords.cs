using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEscrowExecutionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EscrowExecutionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EscrowId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    MilestoneIndex = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                    BlockHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: false),
                    BlockNumber = table.Column<long>(type: "bigint", nullable: false),
                    LogIndex = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TransactionSender = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscrowExecutionRecords", x => x.Id);
                    table.CheckConstraint("CK_EscrowExecutionRecords_Evidence", "length(\"TransactionHash\") = 66 AND length(\"BlockHash\") = 66 AND length(\"TransactionSender\") = 42 AND \"BlockNumber\" >= 0 AND \"LogIndex\" >= 0 AND \"OccurredAt\" > '1970-01-01'::timestamptz AND ((\"Kind\" = 'WorkStarted' AND \"MilestoneIndex\" = -1 AND \"MilestoneId\" IS NULL) OR (\"Kind\" IN ('ClientRelease','CaseSettlement') AND \"MilestoneIndex\" >= 0 AND \"MilestoneId\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_EscrowExecutionRecords_Escrows_EscrowId",
                        column: x => x.EscrowId,
                        principalTable: "Escrows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EscrowExecutionRecords_EscrowId_MilestoneIndex",
                table: "EscrowExecutionRecords",
                columns: new[] { "EscrowId", "MilestoneIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscrowExecutionRecords_EscrowId_OccurredAt",
                table: "EscrowExecutionRecords",
                columns: new[] { "EscrowId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EscrowExecutionRecords");
        }
    }
}
