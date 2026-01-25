using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddNaipesFeature : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NaipeContents",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                InstrumentType = table.Column<int>(type: "INTEGER", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                MimeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                IsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                SortOrder = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NaipeContents", x => x.Id);
                table.ForeignKey(
                    name: "FK_NaipeContents_AspNetUsers_CreatedByUserId",
                    column: x => x.CreatedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "NaipeComments",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                NaipeContentId = table.Column<int>(type: "INTEGER", nullable: false),
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
                table.PrimaryKey("PK_NaipeComments", x => x.Id);
                table.ForeignKey(
                    name: "FK_NaipeComments_AspNetUsers_AuthorId",
                    column: x => x.AuthorId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_NaipeComments_NaipeContents_NaipeContentId",
                    column: x => x.NaipeContentId,
                    principalTable: "NaipeContents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "NaipePlayCounts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                NaipeContentId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                PlayedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NaipePlayCounts", x => x.Id);
                table.ForeignKey(
                    name: "FK_NaipePlayCounts_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_NaipePlayCounts_NaipeContents_NaipeContentId",
                    column: x => x.NaipeContentId,
                    principalTable: "NaipeContents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_NaipeComment_AuthorId",
            table: "NaipeComments",
            column: "AuthorId");

        migrationBuilder.CreateIndex(
            name: "IX_NaipeComment_NaipeContentId_DeletedAt_CreatedAt",
            table: "NaipeComments",
            columns: new[] { "NaipeContentId", "DeletedAt", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_NaipeContent_CreatedByUserId",
            table: "NaipeContents",
            column: "CreatedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_NaipeContent_InstrumentType",
            table: "NaipeContents",
            column: "InstrumentType");

        migrationBuilder.CreateIndex(
            name: "IX_NaipeContent_InstrumentType_SortOrder",
            table: "NaipeContents",
            columns: new[] { "InstrumentType", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_NaipePlayCount_NaipeContentId",
            table: "NaipePlayCounts",
            column: "NaipeContentId");

        migrationBuilder.CreateIndex(
            name: "IX_NaipePlayCount_UserId",
            table: "NaipePlayCounts",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "NaipeComments");

        migrationBuilder.DropTable(
            name: "NaipePlayCounts");

        migrationBuilder.DropTable(
            name: "NaipeContents");
    }
}
