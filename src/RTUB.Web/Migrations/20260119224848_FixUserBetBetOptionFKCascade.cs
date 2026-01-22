using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class FixUserBetBetOptionFKCascade : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_UserBets_BetOptions_BetOptionId",
            table: "UserBets");

        migrationBuilder.AddForeignKey(
            name: "FK_UserBets_BetOptions_BetOptionId",
            table: "UserBets",
            column: "BetOptionId",
            principalTable: "BetOptions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_UserBets_BetOptions_BetOptionId",
            table: "UserBets");

        migrationBuilder.AddForeignKey(
            name: "FK_UserBets_BetOptions_BetOptionId",
            table: "UserBets",
            column: "BetOptionId",
            principalTable: "BetOptions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
