using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddTunoRepresentativeToMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TunoRepresentativeUserId",
                table: "Meetings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_TunoRepresentativeUserId",
                table: "Meetings",
                column: "TunoRepresentativeUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_AspNetUsers_TunoRepresentativeUserId",
                table: "Meetings",
                column: "TunoRepresentativeUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_AspNetUsers_TunoRepresentativeUserId",
                table: "Meetings");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_TunoRepresentativeUserId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "TunoRepresentativeUserId",
                table: "Meetings");
        }
    }
}
