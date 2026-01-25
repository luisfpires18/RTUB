using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class FixGameScoreUniqueIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Remove duplicate records before creating unique index
        // Keep only the record with the highest Points for each user/game combination
        migrationBuilder.Sql(@"
                DELETE FROM GameScores 
                WHERE Id NOT IN (
                    SELECT MIN(gs.Id) 
                    FROM GameScores gs
                    INNER JOIN (
                        SELECT UserId, GameKey, MAX(Points) as MaxPoints
                        FROM GameScores
                        GROUP BY UserId, GameKey
                    ) best ON gs.UserId = best.UserId 
                        AND gs.GameKey = best.GameKey 
                        AND gs.Points = best.MaxPoints
                    GROUP BY gs.UserId, gs.GameKey
                )
            ");

        // Drop the existing index if it exists (it may be non-unique from previous migration)
        migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_GameScores_UserId_GameKey""
            ");

        // Create the unique index
        migrationBuilder.CreateIndex(
            name: "IX_GameScores_UserId_GameKey",
            table: "GameScores",
            columns: new[] { "UserId", "GameKey" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GameScores_UserId_GameKey",
            table: "GameScores");

        migrationBuilder.CreateIndex(
            name: "IX_GameScores_UserId_GameKey",
            table: "GameScores",
            columns: new[] { "UserId", "GameKey" });
    }
}
