using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite requires table rebuild for adding foreign keys with specific behaviors
            // Execute PRAGMA outside transaction
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
            
            migrationBuilder.AddColumn<string>(
                name: "MentorId",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_MentorId",
                table: "AspNetUsers",
                column: "MentorId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_MentorId",
                table: "AspNetUsers",
                column: "MentorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
            
            // Re-enable foreign keys outside transaction
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite requires table rebuild for dropping foreign keys
            // Execute PRAGMA outside transaction
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
            
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_MentorId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_MentorId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MentorId",
                table: "AspNetUsers");
            
            // Re-enable foreign keys outside transaction
            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }
    }
}
