using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear any stale dev data from the old schema before altering the table
            migrationBuilder.Sql("DELETE FROM TransportationPassengers;");
            migrationBuilder.Sql("DELETE FROM Transportations;");

            migrationBuilder.DropForeignKey(
                name: "FK_Transportations_AspNetUsers_DriverId",
                table: "Transportations");

            migrationBuilder.DropForeignKey(
                name: "FK_Transportations_Events_EventId",
                table: "Transportations");

            migrationBuilder.DropIndex(
                name: "IX_Transportations_DriverId",
                table: "Transportations");

            migrationBuilder.DropIndex(
                name: "IX_Transportations_EventId",
                table: "Transportations");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "Transportations");

            migrationBuilder.RenameColumn(
                name: "EventId",
                table: "Transportations",
                newName: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Transportations_PostId",
                table: "Transportations",
                column: "PostId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transportations_Posts_PostId",
                table: "Transportations",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transportations_Posts_PostId",
                table: "Transportations");

            migrationBuilder.DropIndex(
                name: "IX_Transportations_PostId",
                table: "Transportations");

            migrationBuilder.RenameColumn(
                name: "PostId",
                table: "Transportations",
                newName: "EventId");

            migrationBuilder.AddColumn<string>(
                name: "DriverId",
                table: "Transportations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Transportations_DriverId",
                table: "Transportations",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Transportations_EventId",
                table: "Transportations",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transportations_AspNetUsers_DriverId",
                table: "Transportations",
                column: "DriverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transportations_Events_EventId",
                table: "Transportations",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
