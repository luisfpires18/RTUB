using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingActionDismissals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingActionDismissals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ActionKind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Period = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingActionDismissals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingActionDismissals_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingActionDismissals_UserId_ActionKind_SourceKey_Period",
                table: "PendingActionDismissals",
                columns: new[] { "UserId", "ActionKind", "SourceKey", "Period" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingActionDismissals");
        }
    }
}
