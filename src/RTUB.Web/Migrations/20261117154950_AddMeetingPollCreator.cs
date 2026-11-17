using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingPollCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "MeetingPolls",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            // Polls created before this column: the audit field CreatedBy holds the creator's username. A poll whose
            // username no longer matches stays NULL (Owner can still delete it).
            migrationBuilder.Sql(
                """
                UPDATE "MeetingPolls"
                SET "CreatedByUserId" = (SELECT "Id" FROM "AspNetUsers" WHERE "UserName" = "MeetingPolls"."CreatedBy")
                WHERE "CreatedByUserId" IS NULL AND "CreatedBy" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "MeetingPolls");
        }
    }
}
