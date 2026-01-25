using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddActivityLockAndMeetingAtaConfirmation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsLocked",
            table: "Activities",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "MeetingAtaConfirmations",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                MeetingAtaId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                IsConfirmed = table.Column<bool>(type: "INTEGER", nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                ConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MeetingAtaConfirmations", x => x.Id);
                table.ForeignKey(
                    name: "FK_MeetingAtaConfirmations_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MeetingAtaConfirmations_MeetingAtas_MeetingAtaId",
                    column: x => x.MeetingAtaId,
                    principalTable: "MeetingAtas",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MeetingAtaConfirmations_AtaId_UserId",
            table: "MeetingAtaConfirmations",
            columns: new[] { "MeetingAtaId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MeetingAtaConfirmations_UserId",
            table: "MeetingAtaConfirmations",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MeetingAtaConfirmations");

        migrationBuilder.DropColumn(
            name: "IsLocked",
            table: "Activities");
    }
}
