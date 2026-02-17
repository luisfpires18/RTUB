using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterAppearance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "DailyBossMaxHP",
                table: "BossModeProgresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CharacterAppearances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                    HairStyle = table.Column<int>(type: "INTEGER", nullable: false),
                    HairColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    EyeColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    SkinColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    ClothesStyle = table.Column<int>(type: "INTEGER", nullable: false),
                    WeaponVisual = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAppearances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterAppearances_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAppearances_CharacterId",
                table: "CharacterAppearances",
                column: "CharacterId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterAppearances");

            migrationBuilder.AlterColumn<int>(
                name: "DailyBossMaxHP",
                table: "BossModeProgresses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldDefaultValue: 0L);
        }
    }
}
