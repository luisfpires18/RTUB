using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddRepertoireDateToEventRepertoire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the RepertoireDate column with a temporary default value
            migrationBuilder.AddColumn<DateTime>(
                name: "RepertoireDate",
                table: "EventRepertoires",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Update existing records to use the event's start date
            migrationBuilder.Sql(@"
                UPDATE EventRepertoires 
                SET RepertoireDate = (
                    SELECT Date 
                    FROM Events 
                    WHERE Events.Id = EventRepertoires.EventId
                )
                WHERE RepertoireDate = '0001-01-01 00:00:00'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepertoireDate",
                table: "EventRepertoires");
        }
    }
}
