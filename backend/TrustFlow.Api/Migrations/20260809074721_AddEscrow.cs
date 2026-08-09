using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEscrow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Escrows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChainId = table.Column<long>(type: "bigint", nullable: false),
                    TokenAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: false),
                    ContractAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: true),
                    ClientWalletAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: true),
                    FreelancerWalletAddress = table.Column<string>(type: "character varying(42)", maxLength: 42, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReleasedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "PendingDeployment"),
                    DeploymentTransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: true),
                    FundingTransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: true),
                    CancellationTransactionHash = table.Column<string>(type: "character varying(66)", maxLength: 66, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeployedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FundedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Escrows", x => x.Id);
                    table.CheckConstraint("CK_Escrows_ChainId_Positive", "\"ChainId\" > 0");
                    table.CheckConstraint("CK_Escrows_ReleasedAmount_NonNegative", "\"ReleasedAmount\" >= 0");
                    table.CheckConstraint("CK_Escrows_ReleasedAmount_WithinTotal", "\"ReleasedAmount\" <= \"TotalAmount\"");
                    table.CheckConstraint("CK_Escrows_TotalAmount_Positive", "\"TotalAmount\" > 0");
                    table.ForeignKey(
                        name: "FK_Escrows_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ContractAddress",
                table: "Escrows",
                column: "ContractAddress",
                unique: true,
                filter: "\"ContractAddress\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_ProjectId",
                table: "Escrows",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Escrows_Status_UpdatedAt",
                table: "Escrows",
                columns: new[] { "Status", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Escrows");
        }
    }
}
