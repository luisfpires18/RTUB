using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentTuner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstrumentTunings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InstrumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TuningNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentTunings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "InstrumentTunings",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "InstrumentType", "IsDefault", "Name", "TuningNotes", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", 0, true, "Padrão", "[\"E\",\"A\",\"D\",\"G\",\"B\",\"E\"]", null, null },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", 1, true, "Padrão", "[\"G\",\"D\",\"A\",\"E\"]", null, null },
                    { 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", 2, true, "Padrão", "[\"D\",\"G\",\"B\",\"D\"]", null, null },
                    { 4, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", 6, true, "Padrão", "[\"E\",\"A\",\"D\",\"G\"]", null, null },
                    { 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", 3, true, "Referência", "[\"C\"]", null, null },
                    { 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", 8, true, "Sem Afinação", "[]", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentTuning_InstrumentType_IsDefault",
                table: "InstrumentTunings",
                columns: new[] { "InstrumentType", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstrumentTunings");
        }
    }
}
