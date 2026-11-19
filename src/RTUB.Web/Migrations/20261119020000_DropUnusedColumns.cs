using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedColumns : Migration
    {
        /// <summary>
        /// Drops the five columns the app stopped mapping in StopMappingUnusedColumns (task 131). Written by hand: the
        /// model snapshot no longer lists them, so EF generates nothing. Ships only after the release with
        /// StopMappingUnusedColumns is live on PROD and its rollback window is over (N-1 rule,
        /// docs/db-cleanup-inventory.md). It also fixes a database created from the Baseline, where
        /// MemberStatuses.OverrideRetired is NOT NULL with no default and every insert failed.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExperiencePoints", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "Level", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "OverrideRetired", table: "MemberStatuses");
            migrationBuilder.DropColumn(name: "FileUrl", table: "MeetingAtaAttachments");
            migrationBuilder.DropColumn(name: "PdfData", table: "Reports");
        }

        /// <summary>The columns come back empty, at the defaults the previous release expects.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExperiencePoints", table: "AspNetUsers", type: "INTEGER", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "Level", table: "AspNetUsers", type: "INTEGER", nullable: false, defaultValue: 1);
            migrationBuilder.AddColumn<bool>(
                name: "OverrideRetired", table: "MemberStatuses", type: "INTEGER", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(
                name: "FileUrl", table: "MeetingAtaAttachments", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<byte[]>(
                name: "PdfData", table: "Reports", type: "BLOB", nullable: true);
        }
    }
}
