using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterLastBattleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastBattleId",
                table: "Characters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForgedWeapons_UserId_IsEquipped",
                table: "ForgedWeapons",
                columns: new[] { "UserId", "IsEquipped" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ForgedWeapons_UserId_IsEquipped",
                table: "ForgedWeapons");

            migrationBuilder.DropColumn(
                name: "LastBattleId",
                table: "Characters");
        }
    }
}
