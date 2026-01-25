using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddBetComments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BetComments",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                BetId = table.Column<int>(type: "INTEGER", nullable: false),
                AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                Text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                MediaUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                MediaType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BetComments", x => x.Id);
                table.ForeignKey(
                    name: "FK_BetComments_AspNetUsers_AuthorId",
                    column: x => x.AuthorId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_BetComments_Bets_BetId",
                    column: x => x.BetId,
                    principalTable: "Bets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BetComments_AuthorId",
            table: "BetComments",
            column: "AuthorId");

        migrationBuilder.CreateIndex(
            name: "IX_BetComments_BetId",
            table: "BetComments",
            column: "BetId");

        migrationBuilder.CreateIndex(
            name: "IX_BetComments_CreatedAt",
            table: "BetComments",
            column: "CreatedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BetComments");
    }
}
