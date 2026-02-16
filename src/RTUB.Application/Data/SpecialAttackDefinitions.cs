using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Definitions for the two combat abilities:
///   1) Ataque Pesado — universal, always available
///   2) Ataque Especial — unique per instrument, based on equipped weapon
/// Instruments removed from game (Baixo, Flauta, Fagote, Saxofone) return null.
/// </summary>
public static class SpecialAttackDefinitions
{
    /// <summary>
    /// Returns the heavy attack (always available to all players).
    /// </summary>
    public static SpecialAttack GetHeavyAttack() => new()
    {
        Id = 1,
        AttackId = "heavy_attack",
        Name = "Ataque Pesado",
        Description = "Um golpe poderoso com 2x de dano. Pode causar crítico.",
        VfxType = SpellVfxType.MeleeStrike,
        Target = SpellTargetType.SingleEnemy,
        DamageMultiplier = 2.0,
        CooldownSeconds = 6.0,
        Icon = "⚔️",
        VfxColor = "ff8800",
        ScreenShake = true,
        CanCrit = true,
        SortOrder = 0,
        EffectType = SpellEffectType.None
    };

    /// <summary>
    /// Returns the unique special attack for the given instrument.
    /// Returns null if no instrument is equipped or if the instrument was removed from game.
    /// </summary>
    public static SpecialAttack? GetInstrumentSpecial(InstrumentType? instrument)
    {
        if (instrument == null) return null;

        return instrument.Value switch
        {
            InstrumentType.Guitarra => new SpecialAttack
            {
                Id = 101,
                AttackId = "guitarra_barrage",
                Name = "Ataque Especial",
                Description = "Carrega 2 segundos e dispara uma rajada de acordes contra todos os inimigos.",
                VfxType = SpellVfxType.Projectile,
                Target = SpellTargetType.AllEnemies,
                DamageMultiplier = 2.0,
                CooldownSeconds = 14.0,
                Icon = "🎸",
                VfxColor = "ff4444",
                ScreenShake = true,
                CanCrit = false,
                SortOrder = 1,
                EffectType = SpellEffectType.None
            },

            InstrumentType.Bandolim => new SpecialAttack
            {
                Id = 102,
                AttackId = "bandolim_swiftchord",
                Name = "Ataque Especial",
                Description = "Um acorde rápido e certeiro que causa 3x de dano a um único alvo. Pode causar crítico.",
                VfxType = SpellVfxType.Projectile,
                Target = SpellTargetType.SingleEnemy,
                DamageMultiplier = 3.0,
                CooldownSeconds = 10.0,
                Icon = "🎵",
                VfxColor = "44ff88",
                ScreenShake = false,
                CanCrit = true,
                SortOrder = 1,
                EffectType = SpellEffectType.None
            },

            InstrumentType.Cavaquinho => new SpecialAttack
            {
                Id = 103,
                AttackId = "cavaquinho_paralysis",
                Name = "Ataque Especial",
                Description = "Ritmo paralisante que causa 1.5x de dano a todos os inimigos e paralisa-os por 3 turnos.",
                VfxType = SpellVfxType.SoundWave,
                Target = SpellTargetType.AllEnemies,
                DamageMultiplier = 1.5,
                CooldownSeconds = 18.0,
                Icon = "⚡",
                VfxColor = "ffdd00",
                ScreenShake = true,
                CanCrit = false,
                SortOrder = 1,
                EffectType = SpellEffectType.Sleep, // Paralysis = frozen speed bar
                EffectDuration = 3
            },

            InstrumentType.Acordeao => new SpecialAttack
            {
                Id = 104,
                AttackId = "acordeao_fear",
                Name = "Ataque Especial",
                Description = "Som aterrador que causa 1.5x de dano a todos os inimigos e amedronta-os por 3 turnos.",
                VfxType = SpellVfxType.SoundWave,
                Target = SpellTargetType.AllEnemies,
                DamageMultiplier = 1.5,
                CooldownSeconds = 18.0,
                Icon = "😱",
                VfxColor = "9933ff",
                ScreenShake = true,
                CanCrit = false,
                SortOrder = 1,
                EffectType = SpellEffectType.Sleep, // Fear = frozen speed bar
                EffectDuration = 3
            },

            // Baixo — removed from game
            InstrumentType.Baixo => null,

            InstrumentType.Contrabaixo => new SpecialAttack
            {
                Id = 107,
                AttackId = "contrabaixo_sonicboom",
                Name = "Ataque Especial",
                Description = "Projétil sonoro gigante que causa 2x de dano a todos os inimigos.",
                VfxType = SpellVfxType.Projectile,
                Target = SpellTargetType.AllEnemies,
                DamageMultiplier = 2.0,
                CooldownSeconds = 14.0,
                Icon = "💥",
                VfxColor = "4466ff",
                ScreenShake = true,
                CanCrit = false,
                SortOrder = 1,
                EffectType = SpellEffectType.None
            },

            InstrumentType.Percussao => new SpecialAttack
            {
                Id = 108,
                AttackId = "percussao_combo",
                Name = "Ataque Especial",
                Description = "Sequência de 3 golpes (1-2-3) — o 3º golpe causa o dobro do dano. Total: 4x dano.",
                VfxType = SpellVfxType.MeleeStrike,
                Target = SpellTargetType.SingleEnemy,
                DamageMultiplier = 4.0,
                CooldownSeconds = 14.0,
                Icon = "🥁",
                VfxColor = "ff9933",
                ScreenShake = true,
                CanCrit = true,
                SortOrder = 1,
                EffectType = SpellEffectType.None
            },

            InstrumentType.Pandeireta => new SpecialAttack
            {
                Id = 109,
                AttackId = "pandeireta_boomerang",
                Name = "Ataque Especial",
                Description = "Atira a pandeireta como um boomerang que vai e volta, causando 2x de dano a todos os inimigos no caminho.",
                VfxType = SpellVfxType.Projectile,
                Target = SpellTargetType.AllEnemies,
                DamageMultiplier = 2.0,
                CooldownSeconds = 12.0,
                Icon = "🪃",
                VfxColor = "ffff44",
                ScreenShake = false,
                CanCrit = false,
                SortOrder = 1,
                EffectType = SpellEffectType.None
            },

            InstrumentType.Estandarte => new SpecialAttack
            {
                Id = 110,
                AttackId = "estandarte_rally",
                Name = "Ataque Especial",
                Description = "Ergue o estandarte, aumentando todas as stats em 25% durante 3 rondas.",
                VfxType = SpellVfxType.Buff,
                Target = SpellTargetType.Self,
                DamageMultiplier = 0.0,
                CooldownSeconds = 20.0,
                Icon = "🏴",
                VfxColor = "ffcc00",
                ScreenShake = false,
                CanCrit = false,
                SortOrder = 1,
                EffectType = SpellEffectType.PowerBoost,
                EffectDuration = 3,
                EffectMagnitude = 0.25
            },

            InstrumentType.Violino => new SpecialAttack
            {
                Id = 111,
                AttackId = "violino_sleep",
                Name = "Ataque Especial",
                Description = "Uma melodia sombria que causa 1.5x de dano a todos os inimigos e adormece-os por 3 turnos.",
                VfxType = SpellVfxType.MusicNotes,
                Target = SpellTargetType.AllEnemies,
                DamageMultiplier = 1.5,
                CooldownSeconds = 18.0,
                Icon = "🎻",
                VfxColor = "aa44ff",
                ScreenShake = false,
                CanCrit = false,
                SortOrder = 1,
                EffectType = SpellEffectType.Sleep,
                EffectDuration = 3
            },

            // Flauta — removed from game
            InstrumentType.Flauta => null,

            // Fagote — removed from game
            InstrumentType.Fagote => null,

            // Saxofone — removed from game
            InstrumentType.Saxofone => null,

            _ => null
        };
    }

    /// <summary>
    /// Returns the combat abilities for a player based on their equipped weapon's instrument.
    /// Always returns Ataque Pesado; adds Ataque Especial if applicable.
    /// </summary>
    public static List<SpecialAttack> GetCombatAbilities(InstrumentType? instrument)
    {
        var abilities = new List<SpecialAttack> { GetHeavyAttack() };

        var special = GetInstrumentSpecial(instrument);
        if (special != null)
        {
            abilities.Add(special);
        }

        return abilities;
    }
}
