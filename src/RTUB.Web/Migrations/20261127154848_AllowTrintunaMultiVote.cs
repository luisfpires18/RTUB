using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AllowTrintunaMultiVote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GatheringPollVotes_GatheringId_UserId",
                table: "GatheringPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GatheringPollVotes_GatheringPollOptionId",
                table: "GatheringPollVotes");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPollVotes_GatheringId_UserId",
                table: "GatheringPollVotes",
                columns: new[] { "GatheringId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPollVotes_GatheringPollOptionId_UserId",
                table: "GatheringPollVotes",
                columns: new[] { "GatheringPollOptionId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GatheringPollVotes_GatheringId_UserId",
                table: "GatheringPollVotes");

            migrationBuilder.DropIndex(
                name: "IX_GatheringPollVotes_GatheringPollOptionId_UserId",
                table: "GatheringPollVotes");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPollVotes_GatheringId_UserId",
                table: "GatheringPollVotes",
                columns: new[] { "GatheringId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPollVotes_GatheringPollOptionId",
                table: "GatheringPollVotes",
                column: "GatheringPollOptionId");
        }
    }
}
