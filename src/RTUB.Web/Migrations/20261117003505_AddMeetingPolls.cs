using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingPolls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Audience = table.Column<int>(type: "INTEGER", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ClosesAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingPolls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MeetingPollOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingPollId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingPollOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingPollOptions_MeetingPolls_MeetingPollId",
                        column: x => x.MeetingPollId,
                        principalTable: "MeetingPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingPollVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingPollId = table.Column<int>(type: "INTEGER", nullable: false),
                    MeetingPollOptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    VotedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingPollVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingPollVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingPollVotes_MeetingPollOptions_MeetingPollOptionId",
                        column: x => x.MeetingPollOptionId,
                        principalTable: "MeetingPollOptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingPollVotes_MeetingPolls_MeetingPollId",
                        column: x => x.MeetingPollId,
                        principalTable: "MeetingPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPollOptions_MeetingPollId",
                table: "MeetingPollOptions",
                column: "MeetingPollId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPolls_Audience_CreatedAt",
                table: "MeetingPolls",
                columns: new[] { "Audience", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPollVotes_MeetingPollId_UserId",
                table: "MeetingPollVotes",
                columns: new[] { "MeetingPollId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPollVotes_MeetingPollOptionId",
                table: "MeetingPollVotes",
                column: "MeetingPollOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPollVotes_UserId",
                table: "MeetingPollVotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingPollVotes");

            migrationBuilder.DropTable(
                name: "MeetingPollOptions");

            migrationBuilder.DropTable(
                name: "MeetingPolls");
        }
    }
}
