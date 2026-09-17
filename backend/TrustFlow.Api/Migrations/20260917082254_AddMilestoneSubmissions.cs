using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MilestoneSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PreviewLink = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceFilesLink = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientFeedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilestoneSubmissions", x => x.Id);
                    table.CheckConstraint("CK_MilestoneSubmissions_Version_Positive", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_MilestoneSubmissions_AspNetUsers_FreelancerId",
                        column: x => x.FreelancerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MilestoneSubmissions_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MilestoneSubmissionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileSize = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilestoneSubmissionAttachments", x => x.Id);
                    table.CheckConstraint("CK_MilestoneSubmissionAttachments_FileSize", "\"FileSize\" BETWEEN 1 AND 26214400");
                    table.ForeignKey(
                        name: "FK_MilestoneSubmissionAttachments_MilestoneSubmissions_Submiss~",
                        column: x => x.SubmissionId,
                        principalTable: "MilestoneSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneSubmissionAttachments_SubmissionId",
                table: "MilestoneSubmissionAttachments",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneSubmissions_FreelancerId",
                table: "MilestoneSubmissions",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneSubmissions_MilestoneId_Version",
                table: "MilestoneSubmissions",
                columns: new[] { "MilestoneId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilestoneSubmissionAttachments");

            migrationBuilder.DropTable(
                name: "MilestoneSubmissions");
        }
    }
}
