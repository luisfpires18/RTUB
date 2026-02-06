using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddCharacterEnergy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Energy",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 10);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastEnergyRegenAt",
            table: "Characters",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MaxEnergy",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 10);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Energy",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "LastEnergyRegenAt",
            table: "Characters");

        migrationBuilder.DropColumn(
            name: "MaxEnergy",
            table: "Characters");
    }
}
