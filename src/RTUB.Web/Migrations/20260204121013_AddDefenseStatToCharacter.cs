using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Web.Migrations;

/// <inheritdoc />
public partial class AddDefenseStatToCharacter : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "BaseDefense",
            table: "StageEnemies",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "Defense",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "DefenseUpgrades",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BaseDefense",
            table: "StageEnemies");

        migrationBuilder.DropColumn(
            name: "Defense",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "DefenseUpgrades",
            table: "Characters");
    }
}
