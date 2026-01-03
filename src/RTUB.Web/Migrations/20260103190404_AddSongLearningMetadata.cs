using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddSongLearningMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "Songs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedPracticeHours",
                table: "Songs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrimaryInstrument",
                table: "Songs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryInstruments",
                table: "Songs",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkillTags",
                table: "Songs",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Song_Difficulty",
                table: "Songs",
                column: "Difficulty");

            migrationBuilder.CreateIndex(
                name: "IX_Song_PrimaryInstrument",
                table: "Songs",
                column: "PrimaryInstrument");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Song_Difficulty",
                table: "Songs");

            migrationBuilder.DropIndex(
                name: "IX_Song_PrimaryInstrument",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "EstimatedPracticeHours",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "PrimaryInstrument",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "SecondaryInstruments",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "SkillTags",
                table: "Songs");
        }
    }
}
