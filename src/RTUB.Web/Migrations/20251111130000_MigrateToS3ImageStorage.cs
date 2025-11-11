using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToS3ImageStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove ImageData and ImageContentType columns from Slideshows
            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Slideshows");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Slideshows");

            // Remove ImageData and ImageContentType columns from Events
            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Events");

            // Remove CoverImageData and CoverImageContentType columns from Albums
            // and rename CoverImageUrl to ImageUrl
            migrationBuilder.DropColumn(
                name: "CoverImageData",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "CoverImageContentType",
                table: "Albums");

            migrationBuilder.RenameColumn(
                name: "CoverImageUrl",
                table: "Albums",
                newName: "ImageUrl");

            // Remove ImageData and ImageContentType columns from Products
            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Products");

            // Remove ImageData and ImageContentType columns from Instruments
            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Instruments");

            // Remove ProfilePictureData and ProfilePictureContentType from AspNetUsers
            // Add ImageUrl column
            migrationBuilder.DropColumn(
                name: "ProfilePictureData",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilePictureContentType",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore blob columns for Slideshows
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Slideshows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Slideshows",
                type: "BLOB",
                nullable: true);

            // Restore blob columns for Events
            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Events",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Events",
                type: "BLOB",
                nullable: true);

            // Restore blob columns for Albums and rename ImageUrl back to CoverImageUrl
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Albums",
                newName: "CoverImageUrl");

            migrationBuilder.AddColumn<string>(
                name: "CoverImageContentType",
                table: "Albums",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CoverImageData",
                table: "Albums",
                type: "BLOB",
                nullable: true);

            // Restore blob columns for Products
            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Products",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Products",
                type: "TEXT",
                nullable: true);

            // Restore blob columns for Instruments
            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Instruments",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Instruments",
                type: "TEXT",
                nullable: true);

            // Restore ProfilePictureData blob columns for AspNetUsers
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<byte[]>(
                name: "ProfilePictureData",
                table: "AspNetUsers",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureContentType",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }
    }
}
