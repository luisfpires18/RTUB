using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RenameMbwayPersonNameToTransferFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PersonName",
                table: "MbwayTransfers",
                newName: "TransferFrom");

            migrationBuilder.AddColumn<string>(
                name: "TransferTo",
                table: "MbwayTransfers",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransferTo",
                table: "MbwayTransfers");

            migrationBuilder.RenameColumn(
                name: "TransferFrom",
                table: "MbwayTransfers",
                newName: "PersonName");
        }
    }
}
