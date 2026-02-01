using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddAndroidTesterLoginTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AndroidTesterLogins",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                LoginDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AndroidTesterLogins", x => x.Id);
                table.ForeignKey(
                    name: "FK_AndroidTesterLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AndroidTesterLogins_UserId",
            table: "AndroidTesterLogins",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AndroidTesterLogins");
    }
}

