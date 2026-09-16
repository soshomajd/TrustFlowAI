using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClientProfilesAndVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientIdentityVerifications",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LegalNameProtected = table.Column<byte[]>(type: "bytea", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    RegistrationNumberProtected = table.Column<byte[]>(type: "bytea", nullable: true),
                    DocumentContentProtected = table.Column<byte[]>(type: "bytea", nullable: true),
                    DocumentFileSize = table.Column<int>(type: "integer", nullable: true),
                    DocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientIdentityVerifications", x => x.ClientId);
                    table.CheckConstraint("CK_ClientIdentityVerifications_AccountType", "\"AccountType\" IN ('Individual', 'Company')");
                    table.CheckConstraint("CK_ClientIdentityVerifications_PendingDocument", "\"Status\" <> 'Pending' OR (\"DocumentContentProtected\" IS NOT NULL AND \"DocumentFileSize\" BETWEEN 1 AND 5242880)");
                    table.CheckConstraint("CK_ClientIdentityVerifications_Status", "\"Status\" IN ('Pending', 'Verified', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_ClientIdentityVerifications_AspNetUsers_ClientId",
                        column: x => x.ClientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientIdentityVerifications_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClientProfiles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Overview = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Industry = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CompanySize = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProfiles", x => x.UserId);
                    table.CheckConstraint("CK_ClientProfiles_AccountType", "\"AccountType\" IN ('Individual', 'Company')");
                    table.CheckConstraint("CK_ClientProfiles_CompanySize", "\"CompanySize\" IN ('NotSpecified', 'JustMe', '2-10', '11-50', '51-200', '201Plus')");
                    table.ForeignKey(
                        name: "FK_ClientProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientIdentityVerifications_ReviewedByUserId",
                table: "ClientIdentityVerifications",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientIdentityVerifications_Status_SubmittedAt",
                table: "ClientIdentityVerifications",
                columns: new[] { "Status", "SubmittedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientIdentityVerifications");

            migrationBuilder.DropTable(
                name: "ClientProfiles");
        }
    }
}
