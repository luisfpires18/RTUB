using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddLogisticsBoardAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "LogisticsLists",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "LogisticsCards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "LogisticsBoards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyFolder",
                table: "LogisticsBoards",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "LogisticsBoards",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Visibility",
                table: "LogisticsBoards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LogisticsBoardMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsBoardMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticsBoardMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LogisticsBoardMembers_LogisticsBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "LogisticsBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsBoards_OwnerUserId",
                table: "LogisticsBoards",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsBoardMembers_BoardId_UserId",
                table: "LogisticsBoardMembers",
                columns: new[] { "BoardId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsBoardMembers_UserId",
                table: "LogisticsBoardMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogisticsBoardMembers");

            migrationBuilder.DropIndex(
                name: "IX_LogisticsBoards_OwnerUserId",
                table: "LogisticsBoards");

            migrationBuilder.DropColumn(
                name: "State",
                table: "LogisticsLists");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "LogisticsCards");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "LogisticsBoards");

            migrationBuilder.DropColumn(
                name: "LegacyFolder",
                table: "LogisticsBoards");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "LogisticsBoards");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "LogisticsBoards");
        }
    }
}
