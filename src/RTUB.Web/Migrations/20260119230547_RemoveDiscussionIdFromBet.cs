using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDiscussionIdFromBet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the FK constraint first
            migrationBuilder.DropForeignKey(
                name: "FK_Bets_Discussions_DiscussionId",
                table: "Bets");

            // Drop the index
            migrationBuilder.DropIndex(
                name: "IX_Bets_DiscussionId",
                table: "Bets");

            // Drop the column
            migrationBuilder.DropColumn(
                name: "DiscussionId",
                table: "Bets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add the column
            migrationBuilder.AddColumn<int>(
                name: "DiscussionId",
                table: "Bets",
                type: "INTEGER",
                nullable: true);

            // Re-add the index
            migrationBuilder.CreateIndex(
                name: "IX_Bets_DiscussionId",
                table: "Bets",
                column: "DiscussionId");

            // Re-add the FK constraint
            migrationBuilder.AddForeignKey(
                name: "FK_Bets_Discussions_DiscussionId",
                table: "Bets",
                column: "DiscussionId",
                principalTable: "Discussions",
                principalColumn: "Id");
        }
    }
}
