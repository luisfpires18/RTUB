using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Web.Migrations;

/// <inheritdoc />
public partial class RemoveBattleAndStageBattleTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Step 1: Add new columns to Characters table for arena statistics
        migrationBuilder.AddColumn<int>(
            name: "ArenaWins",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "ArenaLosses",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "ArenaDraws",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "LastOpponentId",
            table: "Characters",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastBattleAt",
            table: "Characters",
            type: "TEXT",
            nullable: true);

        // Step 2: Migrate win/loss/draw counts from Battles table to Characters
        // Count wins (AttackerWon = 0)
        migrationBuilder.Sql(@"
            UPDATE Characters
            SET ArenaWins = (
                SELECT COUNT(*)
                FROM Battles
                WHERE Battles.AttackerCharacterId = Characters.Id
                AND Battles.Outcome = 0
            )
        ");

        // Count losses (DefenderWon = 1)
        migrationBuilder.Sql(@"
            UPDATE Characters
            SET ArenaLosses = (
                SELECT COUNT(*)
                FROM Battles
                WHERE Battles.AttackerCharacterId = Characters.Id
                AND Battles.Outcome = 1
            )
        ");

        // Count draws (Draw = 2)
        migrationBuilder.Sql(@"
            UPDATE Characters
            SET ArenaDraws = (
                SELECT COUNT(*)
                FROM Battles
                WHERE Battles.AttackerCharacterId = Characters.Id
                AND Battles.Outcome = 2
            )
        ");

        // Set LastOpponentId and LastBattleAt from most recent battle
        migrationBuilder.Sql(@"
            UPDATE Characters
            SET LastOpponentId = (
                SELECT DefenderCharacterId
                FROM Battles
                WHERE Battles.AttackerCharacterId = Characters.Id
                ORDER BY CreatedAt DESC
                LIMIT 1
            ),
            LastBattleAt = (
                SELECT CreatedAt
                FROM Battles
                WHERE Battles.AttackerCharacterId = Characters.Id
                ORDER BY CreatedAt DESC
                LIMIT 1
            )
        ");

        // Step 3: Drop the Battles table
        migrationBuilder.DropTable(
            name: "Battles");

        // Step 4: Drop the StageBattles table
        migrationBuilder.DropTable(
            name: "StageBattles");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Recreate Battles table
        migrationBuilder.CreateTable(
            name: "Battles",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AttackerCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                DefenderCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                Seed = table.Column<int>(type: "INTEGER", nullable: false),
                Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                AttackerXP = table.Column<int>(type: "INTEGER", nullable: false),
                DefenderXP = table.Column<int>(type: "INTEGER", nullable: false),
                AttackerFidelis = table.Column<decimal>(type: "TEXT", nullable: false),
                DefenderFidelis = table.Column<decimal>(type: "TEXT", nullable: false),
                ReplayJson = table.Column<string>(type: "TEXT", nullable: false),
                RewardsApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                AttackerFinalHP = table.Column<int>(type: "INTEGER", nullable: true),
                ShotBuffUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                ShotBuffExpired = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Battles", x => x.Id);
                table.ForeignKey(
                    name: "FK_Battles_Characters_AttackerCharacterId",
                    column: x => x.AttackerCharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Battles_Characters_DefenderCharacterId",
                    column: x => x.DefenderCharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        // Recreate StageBattles table
        migrationBuilder.CreateTable(
            name: "StageBattles",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                StageNumber = table.Column<int>(type: "INTEGER", nullable: false),
                StageEnemyId = table.Column<int>(type: "INTEGER", nullable: true),
                EnemyType = table.Column<int>(type: "INTEGER", nullable: false),
                Region = table.Column<int>(type: "INTEGER", nullable: false),
                EnemyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Seed = table.Column<int>(type: "INTEGER", nullable: false),
                Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                XPReward = table.Column<int>(type: "INTEGER", nullable: false),
                FidelisReward = table.Column<decimal>(type: "TEXT", nullable: false),
                BeersDropped = table.Column<int>(type: "INTEGER", nullable: false),
                ShotsDropped = table.Column<int>(type: "INTEGER", nullable: false),
                ReplayJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StageBattles", x => x.Id);
                table.ForeignKey(
                    name: "FK_StageBattles_Characters_CharacterId",
                    column: x => x.CharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Battles_AttackerCharacterId",
            table: "Battles",
            column: "AttackerCharacterId");

        migrationBuilder.CreateIndex(
            name: "IX_Battles_DefenderCharacterId",
            table: "Battles",
            column: "DefenderCharacterId");

        migrationBuilder.CreateIndex(
            name: "IX_StageBattles_CharacterId",
            table: "StageBattles",
            column: "CharacterId");

        // Remove new columns from Characters
        migrationBuilder.DropColumn(
            name: "ArenaWins",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "ArenaLosses",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "ArenaDraws",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "LastOpponentId",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "LastBattleAt",
            table: "Characters");
    }
}
