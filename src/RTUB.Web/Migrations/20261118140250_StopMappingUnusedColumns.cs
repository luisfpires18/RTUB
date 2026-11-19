using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class StopMappingUnusedColumns : Migration
    {
        /// <summary>
        /// Model only. The app stops reading and writing AspNetUsers.ExperiencePoints / Level (database defaults since
        /// UserXpLevelDefaults), MemberStatuses.OverrideRetired, MeetingAtaAttachments.FileUrl and Reports.PdfData, but the
        /// columns stay: the release before this one still maps them (N-1 rule). A later release drops them, by hand,
        /// because this snapshot no longer lists them (docs/db-cleanup-inventory.md).
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
