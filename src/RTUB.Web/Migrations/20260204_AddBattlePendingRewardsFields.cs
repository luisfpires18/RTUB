using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Web.Migrations;

/// <inheritdoc />
public partial class AddBattlePendingRewardsFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AttackerFinalHP",
            table: "Battles",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "RewardsApplied",
            table: "Battles",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "ShotBuffExpired",
            table: "Battles",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "ShotBuffUsed",
            table: "Battles",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AttackerFinalHP",
            table: "Battles");

        migrationBuilder.DropColumn(
            name: "RewardsApplied",
            table: "Battles");

        migrationBuilder.DropColumn(
            name: "ShotBuffExpired",
            table: "Battles");

        migrationBuilder.DropColumn(
            name: "ShotBuffUsed",
            table: "Battles");
    }
}
