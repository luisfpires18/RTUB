using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Web.Migrations;

/// <inheritdoc />
public partial class AddPlacementToStageEnemy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Placement",
            table: "StageEnemies",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Placement",
            table: "StageEnemies");
    }
}
