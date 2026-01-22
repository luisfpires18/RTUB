using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddLeaderboardComments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LeaderboardComments",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                TargetUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                AuthorId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaderboardComments", x => x.Id);
                table.ForeignKey(
                    name: "FK_LeaderboardComments_AspNetUsers_AuthorId",
                    column: x => x.AuthorId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaderboardComments_AspNetUsers_TargetUserId",
                    column: x => x.TargetUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "LeaderboardCommentLikes",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CommentId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeaderboardCommentLikes", x => x.Id);
                table.ForeignKey(
                    name: "FK_LeaderboardCommentLikes_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_LeaderboardCommentLikes_LeaderboardComments_CommentId",
                    column: x => x.CommentId,
                    principalTable: "LeaderboardComments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LeaderboardCommentLike_CommentId_UserId_Unique",
            table: "LeaderboardCommentLikes",
            columns: new[] { "CommentId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_LeaderboardCommentLike_UserId",
            table: "LeaderboardCommentLikes",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaderboardComment_AuthorId",
            table: "LeaderboardComments",
            column: "AuthorId");

        migrationBuilder.CreateIndex(
            name: "IX_LeaderboardComment_TargetUserId_DeletedAt_CreatedAt",
            table: "LeaderboardComments",
            columns: new[] { "TargetUserId", "DeletedAt", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LeaderboardCommentLikes");

        migrationBuilder.DropTable(
            name: "LeaderboardComments");
    }
}
