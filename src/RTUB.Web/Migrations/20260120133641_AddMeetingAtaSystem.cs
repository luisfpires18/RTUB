using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingAtaSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DelegatedAtaWriterMemberId",
                table: "Meetings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MeetingAtas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingId = table.Column<int>(type: "INTEGER", nullable: false),
                    AtaNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ActualStartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PresidentUserId = table.Column<string>(type: "TEXT", nullable: false),
                    FirstSecretaryUserId = table.Column<string>(type: "TEXT", nullable: false),
                    SecondSecretaryUserId = table.Column<string>(type: "TEXT", nullable: true),
                    QuorumBasis = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AttendeesPresent = table.Column<string>(type: "TEXT", nullable: false),
                    AttendeesAbsent = table.Column<string>(type: "TEXT", nullable: false),
                    ClosingText = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PdfStorageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAtas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAtas_AspNetUsers_FirstSecretaryUserId",
                        column: x => x.FirstSecretaryUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingAtas_AspNetUsers_PresidentUserId",
                        column: x => x.PresidentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingAtas_AspNetUsers_SecondSecretaryUserId",
                        column: x => x.SecondSecretaryUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MeetingAtas_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAtaAgendaPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingAtaId = table.Column<int>(type: "INTEGER", nullable: false),
                    PointNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DiscussionSummary = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    DecisionText = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    VotesFor = table.Column<int>(type: "INTEGER", nullable: true),
                    VotesAgainst = table.Column<int>(type: "INTEGER", nullable: true),
                    VotesAbstain = table.Column<int>(type: "INTEGER", nullable: true),
                    VoteResult = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAtaAgendaPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAtaAgendaPoints_MeetingAtas_MeetingAtaId",
                        column: x => x.MeetingAtaId,
                        principalTable: "MeetingAtas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAtaAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MeetingAtaId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttachmentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FileUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IncludeInPdf = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAtaAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAtaAttachments_MeetingAtas_MeetingAtaId",
                        column: x => x.MeetingAtaId,
                        principalTable: "MeetingAtas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_DelegatedAtaWriterMemberId",
                table: "Meetings",
                column: "DelegatedAtaWriterMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAtaAgendaPoints_MeetingAtaId",
                table: "MeetingAtaAgendaPoints",
                column: "MeetingAtaId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAtaAttachments_MeetingAtaId",
                table: "MeetingAtaAttachments",
                column: "MeetingAtaId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAtas_FirstSecretaryUserId",
                table: "MeetingAtas",
                column: "FirstSecretaryUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAtas_MeetingId",
                table: "MeetingAtas",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAtas_PresidentUserId",
                table: "MeetingAtas",
                column: "PresidentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAtas_SecondSecretaryUserId",
                table: "MeetingAtas",
                column: "SecondSecretaryUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Meetings_AspNetUsers_DelegatedAtaWriterMemberId",
                table: "Meetings",
                column: "DelegatedAtaWriterMemberId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Meetings_AspNetUsers_DelegatedAtaWriterMemberId",
                table: "Meetings");

            migrationBuilder.DropTable(
                name: "MeetingAtaAgendaPoints");

            migrationBuilder.DropTable(
                name: "MeetingAtaAttachments");

            migrationBuilder.DropTable(
                name: "MeetingAtas");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_DelegatedAtaWriterMemberId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "DelegatedAtaWriterMemberId",
                table: "Meetings");
        }
    }
}
