using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingWalletAddress",
                table: "AspNetUsers",
                type: "character varying(42)",
                maxLength: 42,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletAddress",
                table: "AspNetUsers",
                type: "character varying(42)",
                maxLength: 42,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletAddressNormalized",
                table: "AspNetUsers",
                type: "character varying(42)",
                maxLength: 42,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WalletVerificationExpiresAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletVerificationNonce",
                table: "AspNetUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WalletVerifiedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_WalletAddressNormalized",
                table: "AspNetUsers",
                column: "WalletAddressNormalized",
                unique: true,
                filter: "\"WalletAddressNormalized\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_WalletAddressNormalized",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PendingWalletAddress",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WalletAddress",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WalletAddressNormalized",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WalletVerificationExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WalletVerificationNonce",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WalletVerifiedAt",
                table: "AspNetUsers");
        }
    }
}
