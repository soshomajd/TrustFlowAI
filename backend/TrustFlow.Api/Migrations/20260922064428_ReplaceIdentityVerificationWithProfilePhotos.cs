using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIdentityVerificationWithProfilePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientIdentityVerifications");

            migrationBuilder.CreateTable(
                name: "ProfilePhotos",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilePhotos", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ProfilePhotos_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfilePhotos");

            migrationBuilder.CreateTable(
                name: "ClientIdentityVerifications",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    DocumentContentProtected = table.Column<byte[]>(type: "bytea", nullable: true),
                    DocumentFileSize = table.Column<int>(type: "integer", nullable: true),
                    DocumentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LegalNameProtected = table.Column<byte[]>(type: "bytea", nullable: true),
                    RegistrationNumberProtected = table.Column<byte[]>(type: "bytea", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_ClientIdentityVerifications_ReviewedByUserId",
                table: "ClientIdentityVerifications",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientIdentityVerifications_Status_SubmittedAt",
                table: "ClientIdentityVerifications",
                columns: new[] { "Status", "SubmittedAt" });
        }
    }
}
