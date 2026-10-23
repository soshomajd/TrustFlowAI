using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementNoticeAcknowledgements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClientSettlementNoticeReadAt",
                table: "EscrowCases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FreelancerSettlementNoticeReadAt",
                table: "EscrowCases",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientSettlementNoticeReadAt",
                table: "EscrowCases");

            migrationBuilder.DropColumn(
                name: "FreelancerSettlementNoticeReadAt",
                table: "EscrowCases");
        }
    }
}
