using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddEventMediaTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_EventRepertoires_EventId",
            table: "EventRepertoires");

        migrationBuilder.CreateTable(
            name: "CommentImages",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CommentId = table.Column<int>(type: "INTEGER", nullable: false),
                Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommentImages", x => x.Id);
                table.ForeignKey(
                    name: "FK_CommentImages_Comments_CommentId",
                    column: x => x.CommentId,
                    principalTable: "Comments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PostMedia",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                PostId = table.Column<int>(type: "INTEGER", nullable: false),
                Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                MediaType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PostMedia", x => x.Id);
                table.ForeignKey(
                    name: "FK_PostMedia_Posts_PostId",
                    column: x => x.PostId,
                    principalTable: "Posts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Songs_AlbumId_TrackNumber",
            table: "Songs",
            columns: new[] { "AlbumId", "TrackNumber" });

        migrationBuilder.CreateIndex(
            name: "IX_RoleAssignments_Position",
            table: "RoleAssignments",
            column: "Position");

        migrationBuilder.CreateIndex(
            name: "IX_RoleAssignments_StartYear_EndYear",
            table: "RoleAssignments",
            columns: new[] { "StartYear", "EndYear" });

        migrationBuilder.CreateIndex(
            name: "IX_RoleAssignments_UserId_Position_Years_Unique",
            table: "RoleAssignments",
            columns: new[] { "UserId", "Position", "StartYear", "EndYear" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EventRepertoires_EventId_DisplayOrder",
            table: "EventRepertoires",
            columns: new[] { "EventId", "DisplayOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_EventRepertoires_EventId_SongId_Unique",
            table: "EventRepertoires",
            columns: new[] { "EventId", "SongId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CommentImages_CommentId",
            table: "CommentImages",
            column: "CommentId");

        migrationBuilder.CreateIndex(
            name: "IX_CommentImages_CommentId_SortOrder",
            table: "CommentImages",
            columns: new[] { "CommentId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_PostMedia_PostId",
            table: "PostMedia",
            column: "PostId");

        migrationBuilder.CreateIndex(
            name: "IX_PostMedia_PostId_SortOrder",
            table: "PostMedia",
            columns: new[] { "PostId", "SortOrder" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CommentImages");

        migrationBuilder.DropTable(
            name: "PostMedia");

        migrationBuilder.DropIndex(
            name: "IX_Songs_AlbumId_TrackNumber",
            table: "Songs");

        migrationBuilder.DropIndex(
            name: "IX_RoleAssignments_Position",
            table: "RoleAssignments");

        migrationBuilder.DropIndex(
            name: "IX_RoleAssignments_StartYear_EndYear",
            table: "RoleAssignments");

        migrationBuilder.DropIndex(
            name: "IX_RoleAssignments_UserId_Position_Years_Unique",
            table: "RoleAssignments");

        migrationBuilder.DropIndex(
            name: "IX_EventRepertoires_EventId_DisplayOrder",
            table: "EventRepertoires");

        migrationBuilder.DropIndex(
            name: "IX_EventRepertoires_EventId_SongId_Unique",
            table: "EventRepertoires");

        migrationBuilder.CreateIndex(
            name: "IX_EventRepertoires_EventId",
            table: "EventRepertoires",
            column: "EventId");
    }
}
