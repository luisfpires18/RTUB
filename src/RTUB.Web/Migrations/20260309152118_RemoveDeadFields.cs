using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeadFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "TargetMemberName",
                table: "AuditLogs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Posts",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Comments",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetMemberName",
                table: "AuditLogs",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }
    }
}
