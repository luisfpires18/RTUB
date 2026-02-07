using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddForgedWeapons : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "EquippedInstrument",
            table: "Characters",
            newName: "EquippedWeapon2");

        migrationBuilder.AddColumn<int>(
            name: "EquippedWeapon1",
            table: "Characters",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ForgedWeapons",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                WeaponType = table.Column<int>(type: "INTEGER", nullable: false),
                IsTwoHanded = table.Column<bool>(type: "INTEGER", nullable: false),
                SourceInstrument = table.Column<int>(type: "INTEGER", nullable: false),
                SourceDrink = table.Column<int>(type: "INTEGER", nullable: false),
                BonusHP = table.Column<int>(type: "INTEGER", nullable: false),
                BonusPower = table.Column<int>(type: "INTEGER", nullable: false),
                BonusSpeed = table.Column<int>(type: "INTEGER", nullable: false),
                BonusDefense = table.Column<int>(type: "INTEGER", nullable: false),
                BonusCriticalChance = table.Column<double>(type: "REAL", nullable: false),
                IsEquipped = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ForgedWeapons", x => x.Id);
                table.ForeignKey(
                    name: "FK_ForgedWeapons_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ForgedWeapons_UserId",
            table: "ForgedWeapons",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ForgedWeapons");

        migrationBuilder.DropColumn(
            name: "EquippedWeapon1",
            table: "Characters");

        migrationBuilder.RenameColumn(
            name: "EquippedWeapon2",
            table: "Characters",
            newName: "EquippedInstrument");
    }
}
