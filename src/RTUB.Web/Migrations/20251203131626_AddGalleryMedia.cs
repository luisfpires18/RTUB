using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddGalleryMedia : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "GalleryMedia",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UploaderId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                MediaUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                Year = table.Column<int>(type: "INTEGER", nullable: false),
                TakenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GalleryMedia", x => x.Id);
                table.ForeignKey(
                    name: "FK_GalleryMedia_AspNetUsers_UploaderId",
                    column: x => x.UploaderId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "GalleryMediaPersonTags",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                GalleryMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GalleryMediaPersonTags", x => x.Id);
                table.ForeignKey(
                    name: "FK_GalleryMediaPersonTags_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_GalleryMediaPersonTags_GalleryMedia_GalleryMediaId",
                    column: x => x.GalleryMediaId,
                    principalTable: "GalleryMedia",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GalleryMedia_UploaderId",
            table: "GalleryMedia",
            column: "UploaderId");

        migrationBuilder.CreateIndex(
            name: "IX_GalleryMedia_Year",
            table: "GalleryMedia",
            column: "Year");

        migrationBuilder.CreateIndex(
            name: "IX_GalleryMedia_Year_CreatedAt",
            table: "GalleryMedia",
            columns: new[] { "Year", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_GalleryMediaPersonTag_GalleryMediaId",
            table: "GalleryMediaPersonTags",
            column: "GalleryMediaId");

        migrationBuilder.CreateIndex(
            name: "IX_GalleryMediaPersonTag_GalleryMediaId_UserId_Unique",
            table: "GalleryMediaPersonTags",
            columns: new[] { "GalleryMediaId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GalleryMediaPersonTag_UserId",
            table: "GalleryMediaPersonTags",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "GalleryMediaPersonTags");

        migrationBuilder.DropTable(
            name: "GalleryMedia");
    }
}
