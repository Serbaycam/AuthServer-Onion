using AuthServer.Identity.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AuthServer.Identity.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260910090000_HardenRefreshTokens")]
public class HardenRefreshTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "RefreshTokens" SET "Token" = 'legacy-' || "Id"::text,
                "ReplacedByToken" = NULL,
                "RevokedDate" = COALESCE("RevokedDate", NOW()),
                "ReasonRevoked" = 'Security upgrade: sign in again';
            """);
        migrationBuilder.CreateIndex(name: "IX_RefreshTokens_Token", table: "RefreshTokens", column: "Token", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex(name: "IX_RefreshTokens_Token", table: "RefreshTokens");
}
