using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Proposals_FreelancerId",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_Projects_FreelancerId",
                table: "Projects");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "text",
                nullable: false,
                defaultValue: "Open",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_FreelancerId_CreatedAt",
                table: "Proposals",
                columns: new[] { "FreelancerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_ProjectId_CreatedAt",
                table: "Proposals",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Description_Trgm",
                table: "Projects",
                column: "Description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_FreelancerId_CreatedAt",
                table: "Projects",
                columns: new[] { "FreelancerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_Budget",
                table: "Projects",
                columns: new[] { "Status", "Budget" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_CreatedAt",
                table: "Projects",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_Deadline",
                table: "Projects",
                columns: new[] { "Status", "Deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Title_Trgm",
                table: "Projects",
                column: "Title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Proposals_FreelancerId_CreatedAt",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_ProjectId_CreatedAt",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Description_Trgm",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_FreelancerId_CreatedAt",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Status_Budget",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Status_CreatedAt",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Status_Deadline",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Title_Trgm",
                table: "Projects");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Open");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_FreelancerId",
                table: "Proposals",
                column: "FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_FreelancerId",
                table: "Projects",
                column: "FreelancerId");
        }
    }
}
