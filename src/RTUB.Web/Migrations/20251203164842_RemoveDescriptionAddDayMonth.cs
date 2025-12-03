using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDescriptionAddDayMonth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "GalleryMedia");

            migrationBuilder.AddColumn<byte>(
                name: "Day",
                table: "GalleryMedia",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Month",
                table: "GalleryMedia",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Day",
                table: "GalleryMedia");

            migrationBuilder.DropColumn(
                name: "Month",
                table: "GalleryMedia");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "GalleryMedia",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }
    }
}
