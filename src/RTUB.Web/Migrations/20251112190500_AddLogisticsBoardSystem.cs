using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddLogisticsBoardSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create LogisticsBoards table
            migrationBuilder.CreateTable(
                name: "LogisticsBoards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EventId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticsBoards_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                });

            // Create LogisticsLists table
            migrationBuilder.CreateTable(
                name: "LogisticsLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticsLists_LogisticsBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "LogisticsBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create LogisticsCards table
            migrationBuilder.CreateTable(
                name: "LogisticsCards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ListId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    EventId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedToUserId = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Labels = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReminderDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChecklistJson = table.Column<string>(type: "TEXT", nullable: true),
                    AttachmentsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticsCards_AspNetUsers_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LogisticsCards_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LogisticsCards_LogisticsLists_ListId",
                        column: x => x.ListId,
                        principalTable: "LogisticsLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_LogisticsBoards_EventId",
                table: "LogisticsBoards",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsLists_BoardId",
                table: "LogisticsLists",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCards_AssignedToUserId",
                table: "LogisticsCards",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCards_EventId",
                table: "LogisticsCards",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCards_ListId",
                table: "LogisticsCards",
                column: "ListId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogisticsCards");

            migrationBuilder.DropTable(
                name: "LogisticsLists");

            migrationBuilder.DropTable(
                name: "LogisticsBoards");
        }
    }
}
