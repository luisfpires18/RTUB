using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDiscussionForBets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bets_Discussions_DiscussionId",
                table: "Bets");

            migrationBuilder.DropIndex(
                name: "IX_Bets_DiscussionId",
                table: "Bets");

            migrationBuilder.DropColumn(
                name: "DiscussionId",
                table: "Bets");

            // Make EventId nullable to support both Event and Bet discussions
            migrationBuilder.AlterColumn<int>(
                name: "EventId",
                table: "Discussions",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "BetId",
                table: "Discussions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Discussions_BetId",
                table: "Discussions",
                column: "BetId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Discussions_Bets_BetId",
                table: "Discussions",
                column: "BetId",
                principalTable: "Bets",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Discussions_Bets_BetId",
                table: "Discussions");

            migrationBuilder.DropIndex(
                name: "IX_Discussions_BetId",
                table: "Discussions");

            migrationBuilder.DropColumn(
                name: "BetId",
                table: "Discussions");

            // Revert EventId to non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "EventId",
                table: "Discussions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscussionId",
                table: "Bets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bets_DiscussionId",
                table: "Bets",
                column: "DiscussionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bets_Discussions_DiscussionId",
                table: "Bets",
                column: "DiscussionId",
                principalTable: "Discussions",
                principalColumn: "Id");
        }
    }
}
