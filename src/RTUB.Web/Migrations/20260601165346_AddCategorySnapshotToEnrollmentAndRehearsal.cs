using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddCategorySnapshotToEnrollmentAndRehearsal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryAtRehearsal",
                table: "RehearsalAttendances",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryAtEvent",
                table: "Enrollments",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryAtRehearsal",
                table: "RehearsalAttendances");

            migrationBuilder.DropColumn(
                name: "CategoryAtEvent",
                table: "Enrollments");
        }
    }
}
