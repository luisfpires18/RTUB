using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class MakeGameScoreUserGameKeyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameScores_UserId_GameKey",
                table: "GameScores");

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
}
