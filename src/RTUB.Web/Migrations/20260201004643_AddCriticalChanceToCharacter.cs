using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddCriticalChanceToCharacter : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<double>(
            name: "CriticalChance",
            table: "Characters",
            type: "REAL",
            nullable: false,
            defaultValue: 0.01);

        migrationBuilder.AddColumn<int>(
            name: "CriticalUpgrades",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CriticalChance",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "CriticalUpgrades",
            table: "Characters");
    }
}
