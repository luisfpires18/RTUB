using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddTrintunaGatherings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Gatherings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Place = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OrganizerUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsHistorical = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gatherings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gatherings_AspNetUsers_OrganizerUserId",
                        column: x => x.OrganizerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrintunaAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrintunaAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrintunaAccesses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatheringParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GatheringId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    WillAttend = table.Column<bool>(type: "INTEGER", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatheringParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatheringParticipants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatheringParticipants_Gatherings_GatheringId",
                        column: x => x.GatheringId,
                        principalTable: "Gatherings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatheringPollOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GatheringId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatheringPollOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatheringPollOptions_Gatherings_GatheringId",
                        column: x => x.GatheringId,
                        principalTable: "Gatherings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GatheringPollVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GatheringId = table.Column<int>(type: "INTEGER", nullable: false),
                    GatheringPollOptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    VotedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GatheringPollVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GatheringPollVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatheringPollVotes_GatheringPollOptions_GatheringPollOptionId",
                        column: x => x.GatheringPollOptionId,
                        principalTable: "GatheringPollOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GatheringPollVotes_Gatherings_GatheringId",
                        column: x => x.GatheringId,
                        principalTable: "Gatherings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GatheringParticipants_GatheringId_UserId",
                table: "GatheringParticipants",
                columns: new[] { "GatheringId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatheringParticipants_UserId",
                table: "GatheringParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPollOptions_GatheringId_Date",
                table: "GatheringPollOptions",
                columns: new[] { "GatheringId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPollVotes_GatheringId_UserId",
                table: "GatheringPollVotes",
                columns: new[] { "GatheringId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPollVotes_GatheringPollOptionId",
                table: "GatheringPollVotes",
                column: "GatheringPollOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_GatheringPollVotes_UserId",
                table: "GatheringPollVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Gatherings_Number",
                table: "Gatherings",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gatherings_OrganizerUserId",
                table: "Gatherings",
                column: "OrganizerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TrintunaAccesses_UserId",
                table: "TrintunaAccesses",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GatheringParticipants");

            migrationBuilder.DropTable(
                name: "GatheringPollVotes");

            migrationBuilder.DropTable(
                name: "TrintunaAccesses");

            migrationBuilder.DropTable(
                name: "GatheringPollOptions");

            migrationBuilder.DropTable(
                name: "Gatherings");
        }
    }
}
