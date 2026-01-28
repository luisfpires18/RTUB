using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations;

/// <inheritdoc />
public partial class AddMyTunoTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "AspNetUsers",
            type: "BLOB",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Characters",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                Level = table.Column<int>(type: "INTEGER", nullable: false),
                Xp = table.Column<int>(type: "INTEGER", nullable: false),
                Hp = table.Column<int>(type: "INTEGER", nullable: false),
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
            name: "MyTunoBattles",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Mode = table.Column<int>(type: "INTEGER", nullable: false),
                Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                Seed = table.Column<long>(type: "INTEGER", nullable: false),
                ReplayJson = table.Column<string>(type: "TEXT", nullable: false),
                AttackerCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                DefenderCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                AttackerXpDelta = table.Column<int>(type: "INTEGER", nullable: false),
                DefenderXpDelta = table.Column<int>(type: "INTEGER", nullable: false),
                AttackerFidelisDelta = table.Column<decimal>(type: "TEXT", nullable: false),
                DefenderFidelisDelta = table.Column<decimal>(type: "TEXT", nullable: false),
                StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MyTunoBattles", x => x.Id);
                table.ForeignKey(
                    name: "FK_MyTunoBattles_Characters_AttackerCharacterId",
                    column: x => x.AttackerCharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MyTunoBattles_Characters_DefenderCharacterId",
                    column: x => x.DefenderCharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "MyTunoChallengeRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                RequesterUserId = table.Column<string>(type: "TEXT", nullable: false),
                TargetUserId = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MyTunoChallengeRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_MyTunoChallengeRequests_AspNetUsers_RequesterUserId",
                    column: x => x.RequesterUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MyTunoChallengeRequests_AspNetUsers_TargetUserId",
                    column: x => x.TargetUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Characters_UserId",
            table: "Characters",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MyTunoBattles_AttackerCharacterId_StartedAt",
            table: "MyTunoBattles",
            columns: new[] { "AttackerCharacterId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_MyTunoBattles_DefenderCharacterId_StartedAt",
            table: "MyTunoBattles",
            columns: new[] { "DefenderCharacterId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_MyTunoChallengeRequests_RequesterUserId",
            table: "MyTunoChallengeRequests",
            column: "RequesterUserId");

        migrationBuilder.CreateIndex(
            name: "IX_MyTunoChallengeRequests_TargetUserId_Status",
            table: "MyTunoChallengeRequests",
            columns: new[] { "TargetUserId", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MyTunoBattles");

        migrationBuilder.DropTable(
            name: "MyTunoChallengeRequests");

        migrationBuilder.DropTable(
            name: "Characters");

        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "AspNetUsers");
    }
}
