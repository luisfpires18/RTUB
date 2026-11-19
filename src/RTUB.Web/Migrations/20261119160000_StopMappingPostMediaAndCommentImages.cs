using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class StopMappingPostMediaAndCommentImages : Migration
    {
        /// <summary>
        /// Model only. The app stops mapping PostMedia and CommentImages (0 rows on production, nothing wrote or returned
        /// them), but the tables stay: the release before this one still maps them (N-1 rule). A later release drops
        /// them, by hand, because this snapshot no longer lists them (docs/db-cleanup-inventory.md).
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
