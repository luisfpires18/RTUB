using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStageEnemyBaseStatsAndAddInventoryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseCriticalChance",
                table: "StageEnemies");

            migrationBuilder.DropColumn(
                name: "BaseDefense",
                table: "StageEnemies");

            migrationBuilder.DropColumn(
                name: "BaseFidelisDrop",
                table: "StageEnemies");

            migrationBuilder.DropColumn(
                name: "BaseHP",
                table: "StageEnemies");

            migrationBuilder.DropColumn(
                name: "BasePower",
                table: "StageEnemies");

            migrationBuilder.DropColumn(
                name: "BaseSpeed",
                table: "StageEnemies");

            migrationBuilder.DropColumn(
                name: "FinoDropChance",
                table: "StageEnemies");

            migrationBuilder.DropColumn(
                name: "ShotDropChance",
                table: "StageEnemies");

            migrationBuilder.AlterColumn<long>(
                name: "DailyBossMaxHP",
                table: "BossModeProgresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 0);

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""InventoryItems"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_InventoryItems"" PRIMARY KEY AUTOINCREMENT,
                    ""UserId"" TEXT NOT NULL COLLATE NOCASE,
                    ""Type"" INTEGER NOT NULL,
                    ""Quantity"" INTEGER NOT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""CreatedBy"" TEXT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    ""UpdatedBy"" TEXT NULL,
                    CONSTRAINT ""FK_InventoryItems_AspNetUsers_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""AspNetUsers"" (""Id"") ON DELETE CASCADE
                );
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_InventoryItems_UserId"" ON ""InventoryItems"" (""UserId"");
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_InventoryItems_UserId_Type"" ON ""InventoryItems"" (""UserId"", ""Type"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.AddColumn<double>(
                name: "BaseCriticalChance",
                table: "StageEnemies",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "BaseDefense",
                table: "StageEnemies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseFidelisDrop",
                table: "StageEnemies",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BaseHP",
                table: "StageEnemies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BasePower",
                table: "StageEnemies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BaseSpeed",
                table: "StageEnemies",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "FinoDropChance",
                table: "StageEnemies",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ShotDropChance",
                table: "StageEnemies",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AlterColumn<int>(
                name: "DailyBossMaxHP",
                table: "BossModeProgresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldDefaultValue: 0L);
        }
    }
}
