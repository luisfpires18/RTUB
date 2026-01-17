using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddGameScoreIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameScores_UserId",
                table: "GameScores");

            migrationBuilder.CreateIndex(
                name: "IX_GameScores_GameKey_Points_MaxLevel",
                table: "GameScores",
                columns: new[] { "GameKey", "Points", "MaxLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_GameScores_UserId_GameKey",
                table: "GameScores",
                columns: new[] { "UserId", "GameKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameScores_GameKey_Points_MaxLevel",
                table: "GameScores");

            migrationBuilder.DropIndex(
                name: "IX_GameScores_UserId_GameKey",
                table: "GameScores");

            migrationBuilder.CreateIndex(
                name: "IX_GameScores_UserId",
                table: "GameScores",
                column: "UserId");
        }
    }
}
