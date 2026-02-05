using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Web.Migrations;

/// <inheritdoc />
public partial class AddBossStageNumberToStageEnemy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "BossStageNumber",
            table: "StageEnemies",
            type: "INTEGER",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BossStageNumber",
            table: "StageEnemies");
    }
}
