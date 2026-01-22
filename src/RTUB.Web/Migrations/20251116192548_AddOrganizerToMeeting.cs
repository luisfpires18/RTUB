using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddOrganizerToMeeting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OrganizerUserId",
            table: "Meetings",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Meetings_OrganizerUserId",
            table: "Meetings",
            column: "OrganizerUserId");

        migrationBuilder.AddForeignKey(
            name: "FK_Meetings_AspNetUsers_OrganizerUserId",
            table: "Meetings",
            column: "OrganizerUserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Meetings_AspNetUsers_OrganizerUserId",
            table: "Meetings");

        migrationBuilder.DropIndex(
            name: "IX_Meetings_OrganizerUserId",
            table: "Meetings");

        migrationBuilder.DropColumn(
            name: "OrganizerUserId",
            table: "Meetings");
    }
}
