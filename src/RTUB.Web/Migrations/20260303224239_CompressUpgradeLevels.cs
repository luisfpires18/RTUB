using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RTUB.Migrations
{
    /// <summary>
    /// Compresses all upgrade levels by 2× as part of the stat rebalance.
    /// Stat upgrades: flatBonus 100→200 HP, 15→30 Power, 12→24 Defense.
    /// Equipment/weapon per-level: 100→200 HP, 15→30 Power, 12→24 Defense.
    /// Uses ceil(old/2) so players never lose stats (tiny free bump ≤0.3%).
    /// </summary>
    public partial class CompressUpgradeLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Compress stat upgrade levels: ceil(old / 2)
            // SQLite integer division truncates, so (x + 1) / 2 = ceil(x / 2)
            migrationBuilder.Sql("""
                UPDATE Characters SET
                    HpUpgrades      = (HpUpgrades + 1) / 2,
                    PowerUpgrades   = (PowerUpgrades + 1) / 2,
                    DefenseUpgrades = (DefenseUpgrades + 1) / 2
                WHERE HpUpgrades > 0 OR PowerUpgrades > 0 OR DefenseUpgrades > 0
                """);

            // Compress equipment enhancement levels per slot: ceil(old / 2)
            migrationBuilder.Sql("""
                UPDATE Characters SET
                    EquippedHeadBonusLevel      = (EquippedHeadBonusLevel + 1) / 2,
                    EquippedShouldersBonusLevel  = (EquippedShouldersBonusLevel + 1) / 2,
                    EquippedChestBonusLevel      = (EquippedChestBonusLevel + 1) / 2,
                    EquippedGlovesBonusLevel     = (EquippedGlovesBonusLevel + 1) / 2,
                    EquippedLegsBonusLevel       = (EquippedLegsBonusLevel + 1) / 2,
                    EquippedBootsBonusLevel      = (EquippedBootsBonusLevel + 1) / 2
                WHERE EquippedHeadBonusLevel > 0
                   OR EquippedShouldersBonusLevel > 0
                   OR EquippedChestBonusLevel > 0
                   OR EquippedGlovesBonusLevel > 0
                   OR EquippedLegsBonusLevel > 0
                   OR EquippedBootsBonusLevel > 0
                """);

            // Compress forged weapon levels: ceil(old / 2)
            migrationBuilder.Sql("""
                UPDATE ForgedWeapons SET
                    Level = (Level + 1) / 2
                WHERE Level > 0
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: multiply compressed levels back by 2.
            // Note: this is lossy — the remainder from ceiling is lost.
            migrationBuilder.Sql("""
                UPDATE Characters SET
                    HpUpgrades      = HpUpgrades * 2,
                    PowerUpgrades   = PowerUpgrades * 2,
                    DefenseUpgrades = DefenseUpgrades * 2
                WHERE HpUpgrades > 0 OR PowerUpgrades > 0 OR DefenseUpgrades > 0
                """);

            migrationBuilder.Sql("""
                UPDATE Characters SET
                    EquippedHeadBonusLevel      = EquippedHeadBonusLevel * 2,
                    EquippedShouldersBonusLevel  = EquippedShouldersBonusLevel * 2,
                    EquippedChestBonusLevel      = EquippedChestBonusLevel * 2,
                    EquippedGlovesBonusLevel     = EquippedGlovesBonusLevel * 2,
                    EquippedLegsBonusLevel       = EquippedLegsBonusLevel * 2,
                    EquippedBootsBonusLevel      = EquippedBootsBonusLevel * 2
                WHERE EquippedHeadBonusLevel > 0
                   OR EquippedShouldersBonusLevel > 0
                   OR EquippedChestBonusLevel > 0
                   OR EquippedGlovesBonusLevel > 0
                   OR EquippedLegsBonusLevel > 0
                   OR EquippedBootsBonusLevel > 0
                """);

            migrationBuilder.Sql("""
                UPDATE ForgedWeapons SET
                    Level = Level * 2
                WHERE Level > 0
                """);
        }
    }
}
