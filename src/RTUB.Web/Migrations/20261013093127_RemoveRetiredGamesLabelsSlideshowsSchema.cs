using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRetiredGamesLabelsSlideshowsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BetComments");

            migrationBuilder.DropTable(
                name: "BossModeProgresses");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "ForgeComboConfigs");

            migrationBuilder.DropTable(
                name: "ForgedWeapons");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "GameScores");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "ItemTypeConfigs");

            migrationBuilder.DropTable(
                name: "Labels");

            migrationBuilder.DropTable(
                name: "Slideshows");

            migrationBuilder.DropTable(
                name: "StageEnemies");

            migrationBuilder.DropTable(
                name: "StageProgresses");

            migrationBuilder.DropTable(
                name: "SurviveModeProgresses");

            migrationBuilder.DropTable(
                name: "UserBets");

            migrationBuilder.DropTable(
                name: "BetOptions");

            migrationBuilder.DropTable(
                name: "Bets");
        }

        /// <summary>
        /// Schema-only, for local / DEV testing: recreates the tables as the EF model defines them, with no rows. It is
        /// NOT a production rollback path: it does not recreate the pre-043 drift the migration chain left
        /// (<c>Bets.DiscussionId</c> + its foreign key and index, nullable <c>Labels</c> columns, some column defaults)
        /// and restores no data. Rolling production back across task 043 means restoring the pre-migration database
        /// backup (docs/db-cleanup-inventory.md, Task 043).
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BetCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    DateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ImageSrc = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCancelled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    WinningOptionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BossModeProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentBossStage = table.Column<int>(type: "INTEGER", nullable: false),
                    DailyBossMaxHP = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    DailyBossRemainingHP = table.Column<long>(type: "INTEGER", nullable: true),
                    DailyBossStage = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    HighestBossStage = table.Column<int>(type: "INTEGER", nullable: false),
                    LastDailyResetDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalBossStagesCleared = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalFitabSpent = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRunsAttempted = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BossModeProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BossModeProgresses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ArenaLosses = table.Column<int>(type: "INTEGER", nullable: false),
                    ArenaRating = table.Column<int>(type: "INTEGER", nullable: false),
                    ArenaWins = table.Column<int>(type: "INTEGER", nullable: false),
                    CanhaoBuffExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CanhaoBuffRemainingMs = table.Column<long>(type: "INTEGER", nullable: false),
                    CanhaoTimerUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    CastSpeedUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    CigarroDodgeUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    CigarroShieldHitsRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CriticalChance = table.Column<double>(type: "REAL", nullable: false),
                    CriticalUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentHP = table.Column<long>(type: "INTEGER", nullable: true),
                    CustomSpritePath = table.Column<string>(type: "TEXT", nullable: true),
                    Defense = table.Column<int>(type: "INTEGER", nullable: false),
                    DefenseUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    DoubleGatheringUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    Energy = table.Column<int>(type: "INTEGER", nullable: false),
                    EnergyAmountUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    EnergyRegenUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    EquipmentCriticalBonus = table.Column<double>(type: "REAL", nullable: false),
                    EquipmentDefenseBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    EquipmentHPBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    EquipmentPowerBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    EquipmentSpeedBonus = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedBoots = table.Column<int>(type: "INTEGER", nullable: true),
                    EquippedBootsBonusLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedBootsQuality = table.Column<double>(type: "REAL", nullable: false),
                    EquippedChest = table.Column<int>(type: "INTEGER", nullable: true),
                    EquippedChestBonusLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedChestQuality = table.Column<double>(type: "REAL", nullable: false),
                    EquippedGloves = table.Column<int>(type: "INTEGER", nullable: true),
                    EquippedGlovesBonusLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedGlovesQuality = table.Column<double>(type: "REAL", nullable: false),
                    EquippedHead = table.Column<int>(type: "INTEGER", nullable: true),
                    EquippedHeadBonusLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedHeadQuality = table.Column<double>(type: "REAL", nullable: false),
                    EquippedLegs = table.Column<int>(type: "INTEGER", nullable: true),
                    EquippedLegsBonusLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedLegsQuality = table.Column<double>(type: "REAL", nullable: false),
                    EquippedShoulders = table.Column<int>(type: "INTEGER", nullable: true),
                    EquippedShouldersBonusLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedShouldersQuality = table.Column<double>(type: "REAL", nullable: false),
                    EquippedWeapon1 = table.Column<int>(type: "INTEGER", nullable: true),
                    EquippedWeapon2 = table.Column<int>(type: "INTEGER", nullable: true),
                    HP = table.Column<int>(type: "INTEGER", nullable: false),
                    HpUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    LastBattleAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastBattleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastDailyRewardClaim = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastEnergyRegenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastOpponentId = table.Column<int>(type: "INTEGER", nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxEnergy = table.Column<int>(type: "INTEGER", nullable: false),
                    PenaltyBuffExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PenaltyBuffRemainingMs = table.Column<long>(type: "INTEGER", nullable: false),
                    PenaltyTimerUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    Power = table.Column<int>(type: "INTEGER", nullable: false),
                    PowerUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    RareBootsApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    RareChestApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    RareGlovesApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    RareHeadApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    RareLegsApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    RareShouldersApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShotBuffBattlesRemaining = table.Column<int>(type: "INTEGER", nullable: false),
                    ShotBuffUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    ShotStatBuffUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    Speed = table.Column<int>(type: "INTEGER", nullable: false),
                    SpeedUpgrades = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    XP = table.Column<int>(type: "INTEGER", nullable: false)
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
                name: "ForgeComboConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComboKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    PictureUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForgeComboConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ForgedWeapons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    BonusCriticalChance = table.Column<double>(type: "REAL", nullable: false),
                    BonusDefense = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusHP = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusPower = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusSpeed = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsEquipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsTwoHanded = table.Column<bool>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceDrink = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceInstrument = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    WeaponType = table.Column<int>(type: "INTEGER", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsComingSoon = table.Column<bool>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MembersOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    PlayRoute = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    GameKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    MaxLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeSurvived = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameScores_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTypeConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IconClass = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PictureUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    TypeKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTypeConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Labels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Content = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Slideshows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: false),
                    IntervalMs = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsExclusive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slideshows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StageEnemies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BossStageNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Placement = table.Column<int>(type: "INTEGER", nullable: false),
                    Region = table.Column<int>(type: "INTEGER", nullable: false),
                    SpritePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageEnemies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StageProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentRegion = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentStage = table.Column<int>(type: "INTEGER", nullable: false),
                    EndlessModeUnlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnemiesDefeatedInCurrentStage = table.Column<int>(type: "INTEGER", nullable: false),
                    HighestStage = table.Column<int>(type: "INTEGER", nullable: false),
                    LastCheckpoint = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalBossesDefeated = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalStagesCleared = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageProgresses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SurviveModeProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    CurrentRegion = table.Column<int>(type: "INTEGER", nullable: false),
                    HighestLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    IsRunActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LongestSurvivalTime = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    RunStartLevel = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    RunStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalEnemiesKilled = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalLevelsCompleted = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalRunsAttempted = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurviveModeProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurviveModeProgresses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BetComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    BetId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    MediaUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BetComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BetComments_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BetComments_Bets_BetId",
                        column: x => x.BetId,
                        principalTable: "Bets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BetOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BetId = table.Column<int>(type: "INTEGER", nullable: false),
                    MemberAId = table.Column<string>(type: "TEXT", nullable: true),
                    MemberBId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Odds = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BetOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BetOptions_AspNetUsers_MemberAId",
                        column: x => x.MemberAId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BetOptions_AspNetUsers_MemberBId",
                        column: x => x.MemberBId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BetOptions_Bets_BetId",
                        column: x => x.BetId,
                        principalTable: "Bets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BetId = table.Column<int>(type: "INTEGER", nullable: false),
                    BetOptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    FidelisAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FidelisWinnings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IsWon = table.Column<bool>(type: "INTEGER", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBets_BetOptions_BetOptionId",
                        column: x => x.BetOptionId,
                        principalTable: "BetOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBets_Bets_BetId",
                        column: x => x.BetId,
                        principalTable: "Bets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BetComments_AuthorId",
                table: "BetComments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_BetComments_BetId",
                table: "BetComments",
                column: "BetId");

            migrationBuilder.CreateIndex(
                name: "IX_BetComments_CreatedAt",
                table: "BetComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BetOptions_BetId",
                table: "BetOptions",
                column: "BetId");

            migrationBuilder.CreateIndex(
                name: "IX_BetOptions_MemberAId",
                table: "BetOptions",
                column: "MemberAId");

            migrationBuilder.CreateIndex(
                name: "IX_BetOptions_MemberBId",
                table: "BetOptions",
                column: "MemberBId");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_BetCategory",
                table: "Bets",
                column: "BetCategory");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_DateTime",
                table: "Bets",
                column: "DateTime");

            migrationBuilder.CreateIndex(
                name: "IX_Bets_IsCancelled",
                table: "Bets",
                column: "IsCancelled");

            migrationBuilder.CreateIndex(
                name: "IX_BossModeProgresses_HighestBossStage",
                table: "BossModeProgresses",
                column: "HighestBossStage");

            migrationBuilder.CreateIndex(
                name: "IX_BossModeProgresses_UserId",
                table: "BossModeProgresses",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_Level",
                table: "Characters",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_UserId",
                table: "Characters",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForgedWeapons_UserId",
                table: "ForgedWeapons",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ForgedWeapons_UserId_IsEquipped",
                table: "ForgedWeapons",
                columns: new[] { "UserId", "IsEquipped" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_Key",
                table: "Games",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameScores_GameKey_Points_MaxLevel",
                table: "GameScores",
                columns: new[] { "GameKey", "Points", "MaxLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_GameScores_UserId_GameKey",
                table: "GameScores",
                columns: new[] { "UserId", "GameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_UserId",
                table: "InventoryItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_UserId_Type",
                table: "InventoryItems",
                columns: new[] { "UserId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageEnemies_Region",
                table: "StageEnemies",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_StageEnemies_Type",
                table: "StageEnemies",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_StageEnemies_Type_Region",
                table: "StageEnemies",
                columns: new[] { "Type", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_StageProgresses_HighestStage",
                table: "StageProgresses",
                column: "HighestStage");

            migrationBuilder.CreateIndex(
                name: "IX_StageProgresses_UserId",
                table: "StageProgresses",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SurviveModeProgresses_HighestLevel",
                table: "SurviveModeProgresses",
                column: "HighestLevel");

            migrationBuilder.CreateIndex(
                name: "IX_SurviveModeProgresses_UserId",
                table: "SurviveModeProgresses",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBets_BetId",
                table: "UserBets",
                column: "BetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBets_BetOptionId",
                table: "UserBets",
                column: "BetOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBets_UserId",
                table: "UserBets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBets_UserId_BetId",
                table: "UserBets",
                columns: new[] { "UserId", "BetId" });
        }
    }
}
