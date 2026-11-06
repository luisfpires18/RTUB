using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddLogisticsCardWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Task 066. Existing cards are Normal (1). The model has no default on purpose: an explicit Low (0) on a new
            // card must be written, not replaced by the column default. Additive only (N-1 safe: 2.3.x ignores the new
            // columns and tables, and its inserts get Priority 1 / Version 0 from these defaults).
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "LogisticsCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "LogisticsCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LogisticsCardActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsCardActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticsCardActivities_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LogisticsCardActivities_LogisticsCards_CardId",
                        column: x => x.CardId,
                        principalTable: "LogisticsCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogisticsCardAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedById = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsCardAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticsCardAttachments_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LogisticsCardAttachments_LogisticsCards_CardId",
                        column: x => x.CardId,
                        principalTable: "LogisticsCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LogisticsCardComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CardId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Body = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    MentionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogisticsCardComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogisticsCardComments_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LogisticsCardComments_LogisticsCards_CardId",
                        column: x => x.CardId,
                        principalTable: "LogisticsCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCardActivities_ActorId",
                table: "LogisticsCardActivities",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCardActivities_CardId_CreatedAt",
                table: "LogisticsCardActivities",
                columns: new[] { "CardId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCardAttachments_CardId",
                table: "LogisticsCardAttachments",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCardAttachments_UploadedById",
                table: "LogisticsCardAttachments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCardComments_AuthorId",
                table: "LogisticsCardComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_LogisticsCardComments_CardId_CreatedAt",
                table: "LogisticsCardComments",
                columns: new[] { "CardId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogisticsCardActivities");

            migrationBuilder.DropTable(
                name: "LogisticsCardAttachments");

            migrationBuilder.DropTable(
                name: "LogisticsCardComments");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "LogisticsCards");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "LogisticsCards");
        }
    }
}
