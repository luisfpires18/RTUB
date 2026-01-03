using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddChordLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChordDiagrams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InstrumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    ChordName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    FingeringData = table.Column<string>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AudioSampleUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChordDiagrams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChordDiagram_InstrumentType_ChordName",
                table: "ChordDiagrams",
                columns: new[] { "InstrumentType", "ChordName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChordDiagram_InstrumentType_Difficulty",
                table: "ChordDiagrams",
                columns: new[] { "InstrumentType", "Difficulty" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChordDiagrams");
        }
    }
}
