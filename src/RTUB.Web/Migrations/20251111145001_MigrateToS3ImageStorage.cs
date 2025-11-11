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
            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Slideshows");

            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Slideshows");

            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ProfilePictureData",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CoverImageContentType",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "CoverImageData",
                table: "Albums");

            migrationBuilder.RenameColumn(
                name: "ImageContentType",
                table: "Products",
                newName: "ImageUrl");

            migrationBuilder.RenameColumn(
                name: "ImageContentType",
                table: "Instruments",
                newName: "ImageUrl");

            migrationBuilder.RenameColumn(
                name: "ProfilePictureContentType",
                table: "AspNetUsers",
                newName: "ImageUrl");

            migrationBuilder.RenameColumn(
                name: "CoverImageUrl",
                table: "Albums",
                newName: "ImageUrl");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Events",
                type: "TEXT",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 500);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Products",
                newName: "ImageContentType");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Instruments",
                newName: "ImageContentType");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "AspNetUsers",
                newName: "ProfilePictureContentType");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Albums",
                newName: "CoverImageUrl");

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

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Products",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Instruments",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Events",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 500,
                oldNullable: true);

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

            migrationBuilder.AddColumn<byte[]>(
                name: "ProfilePictureData",
                table: "AspNetUsers",
                type: "BLOB",
                nullable: true);

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
        }
    }
}
