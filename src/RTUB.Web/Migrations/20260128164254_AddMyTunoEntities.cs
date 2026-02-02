using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddMyTunoEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Characters",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                Level = table.Column<int>(type: "INTEGER", nullable: false),
                XP = table.Column<int>(type: "INTEGER", nullable: false),
                HP = table.Column<int>(type: "INTEGER", nullable: false),
                Power = table.Column<int>(type: "INTEGER", nullable: false),
                Speed = table.Column<int>(type: "INTEGER", nullable: false),
                HpUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                PowerUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                SpeedUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Characters", x => x.Id);
                table.ForeignKey(
                    name: "FK_Characters_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Battles",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                AttackerCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                DefenderCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                Seed = table.Column<int>(type: "INTEGER", nullable: false),
                Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                AttackerXP = table.Column<int>(type: "INTEGER", nullable: false),
                DefenderXP = table.Column<int>(type: "INTEGER", nullable: false),
                AttackerFidelis = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                DefenderFidelis = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                ReplayJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Battles", x => x.Id);
                table.ForeignKey(
                    name: "FK_Battles_Characters_AttackerCharacterId",
                    column: x => x.AttackerCharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Battles_Characters_DefenderCharacterId",
                    column: x => x.DefenderCharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Battles_AttackerCharacterId",
            table: "Battles",
            column: "AttackerCharacterId");

        migrationBuilder.CreateIndex(
            name: "IX_Battles_CreatedAt",
            table: "Battles",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_Battles_DefenderCharacterId",
            table: "Battles",
            column: "DefenderCharacterId");

        migrationBuilder.CreateIndex(
            name: "IX_Characters_Level",
            table: "Characters",
            column: "Level");

        migrationBuilder.CreateIndex(
            name: "IX_Characters_UserId",
            table: "Characters",
            column: "UserId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Battles");

        migrationBuilder.DropTable(
            name: "Characters");
    }
}
