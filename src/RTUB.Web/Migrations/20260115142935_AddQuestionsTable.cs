using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AssignedPosition = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AssignedMemberId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Unanswered"),
                    LastNotificationSent = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsAwaitingUserReply = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_AspNetUsers_AssignedMemberId",
                        column: x => x.AssignedMemberId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questions_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionReplies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    IsFromAssignedMember = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionReplies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionReplies_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionReplies_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReplies_CreatedAt",
                table: "QuestionReplies",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReplies_IsDeleted",
                table: "QuestionReplies",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReply_AuthorId",
                table: "QuestionReplies",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReply_QuestionId",
                table: "QuestionReplies",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Question_AssignedMemberId",
                table: "Questions",
                column: "AssignedMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Question_AuthorId",
                table: "Questions",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Question_Status_AwaitingReply",
                table: "Questions",
                columns: new[] { "Status", "IsAwaitingUserReply" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CreatedAt",
                table: "Questions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_IsDeleted",
                table: "Questions",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionReplies");

            migrationBuilder.DropTable(
                name: "Questions");
        }
    }
}
