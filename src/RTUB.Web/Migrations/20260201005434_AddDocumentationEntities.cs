using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddDocumentationEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Folders",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                NormalizedKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                IsSpecial = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                SpecialVisibility = table.Column<int>(type: "INTEGER", nullable: true),
                CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CreatedByUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Folders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Documents",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                FolderId = table.Column<int>(type: "INTEGER", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                CloudflareUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                ObjectKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CreatedByUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Documents", x => x.Id);
                table.ForeignKey(
                    name: "FK_Documents_Folders_FolderId",
                    column: x => x.FolderId,
                    principalTable: "Folders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FolderViewers",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                FolderId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CreatedByUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FolderViewers", x => x.Id);
                table.ForeignKey(
                    name: "FK_FolderViewers_Folders_FolderId",
                    column: x => x.FolderId,
                    principalTable: "Folders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Documents_CreatedAt",
            table: "Documents",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_Documents_FolderId",
            table: "Documents",
            column: "FolderId");

        migrationBuilder.CreateIndex(
            name: "IX_Documents_ObjectKey",
            table: "Documents",
            column: "ObjectKey");

        migrationBuilder.CreateIndex(
            name: "IX_Folders_IsSpecial",
            table: "Folders",
            column: "IsSpecial");

        migrationBuilder.CreateIndex(
            name: "IX_Folders_NormalizedKey",
            table: "Folders",
            column: "NormalizedKey");

        migrationBuilder.CreateIndex(
            name: "IX_FolderViewers_FolderId",
            table: "FolderViewers",
            column: "FolderId");

        migrationBuilder.CreateIndex(
            name: "IX_FolderViewers_FolderId_UserId",
            table: "FolderViewers",
            columns: new[] { "FolderId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FolderViewers_UserId",
            table: "FolderViewers",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Documents");

        migrationBuilder.DropTable(
            name: "FolderViewers");

        migrationBuilder.DropTable(
            name: "Folders");
    }
}
