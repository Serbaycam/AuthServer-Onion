using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthServer.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionSecurityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AbsoluteExpiresAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<DateTime>(
                name: "FamilyCreatedAt",
                table: "RefreshTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "RefreshTokens",
                type: "text",
                nullable: false,
                defaultValue: "");
            // A historical row has no trustworthy issuance stamp/family deadline.
            // Erase old hashes so a pre-upgrade credential cannot replay against a new login.
            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" SET
                    "FamilyId" = "Id", "FamilyCreatedAt" = "CreatedDate", "AbsoluteExpiresAt" = "Expires",
                    "Token" = 'session-upgrade-' || "Id"::text, "ReplacedByToken" = NULL,
                    "RevokedDate" = COALESCE("RevokedDate", NOW()),
                    "ReasonRevoked" = 'Session policy upgrade: sign in again';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback must not silently restore unbounded sessions under older code.
            migrationBuilder.Sql("""
                UPDATE "RefreshTokens" SET "Token" = 'session-downgrade-' || "Id"::text,
                    "ReplacedByToken" = NULL, "RevokedDate" = COALESCE("RevokedDate", NOW()),
                    "ReasonRevoked" = 'Session policy downgrade: sign in again';
                """);
            migrationBuilder.DropColumn(
                name: "AbsoluteExpiresAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "FamilyCreatedAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "RefreshTokens");
        }
    }
}
