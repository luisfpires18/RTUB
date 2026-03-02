/**
 * Survive Mode — Weapon Definitions.
 *
 * 8 base weapons (5 levels each) + 4 evolved weapons (single-level).
 * Inspired by survivor.io weapon variety + evolution system.
 */
import { AttackPattern } from '../types';
import type { WeaponDef, WeaponLevelStats, EvolutionDef } from '../types';

// ─── Helper to build level arrays ─────────────────────────────

function levels(base: WeaponLevelStats, scale: Partial<Record<keyof WeaponLevelStats, number[]>>): WeaponLevelStats[] {
  const result: WeaponLevelStats[] = [{ ...base }];
  for (let i = 1; i < 5; i++) {
    const prev = result[i - 1];
    const next: WeaponLevelStats = { ...prev };
    for (const [key, vals] of Object.entries(scale) as [keyof WeaponLevelStats, number[]][]) {
      if (vals && vals[i - 1] !== undefined) {
        (next as Record<string, number>)[key] = (prev as Record<string, number>)[key] + vals[i - 1];
      }
    }
    result.push(next);
  }
  return result;
}

// ─── 1. Tiro Certeiro (Sharpshot) ─────────────────────────────

export const WEAPON_SHARPSHOT: WeaponDef = {
  id: 'sharpshot',
  name: 'Sharpshot',
  icon: '🎯',
  description: 'Dispara projéteis no inimigo mais próximo.',
  pattern: AttackPattern.Projectile,
  color: 0x4fc3f7,
  projectileRadius: 4,
  levels: levels(
    { damage: 1, cooldown: 0.45, range: 180, count: 1, pierce: 0, aoeRadius: 0, speedMult: 1.0 },
    { damage: [1, 1, 2, 3], count: [1, 0, 1, 0], pierce: [0, 1, 0, 1], range: [0, 20, 0, 20] },
  ),
};

// ─── 2. Lâmina Giratória (Spinning Blade) ─────────────────────

export const WEAPON_SPINBLADE: WeaponDef = {
  id: 'spinblade',
  name: 'Spinning Blade',
  icon: '🔪',
  description: 'Lâminas orbitam à volta do jogador.',
  pattern: AttackPattern.Orbital,
  color: 0xb0bec5,
  levels: levels(
    { damage: 2, cooldown: 0, range: 60, count: 2, pierce: -1, aoeRadius: 0, speedMult: 1.0 },
    { damage: [1, 1, 2, 2], count: [1, 0, 1, 1], range: [10, 10, 15, 15] },
  ),
};

// ─── 3. Onda de Choque (Shockwave) ────────────────────────────

export const WEAPON_SHOCKWAVE: WeaponDef = {
  id: 'shockwave',
  name: 'Shockwave',
  icon: '💥',
  description: 'Explosão periódica à volta do jogador.',
  pattern: AttackPattern.AoE,
  color: 0xff8a65,
  levels: levels(
    { damage: 3, cooldown: 3.0, range: 0, count: 1, pierce: 0, aoeRadius: 100, speedMult: 1.0 },
    { damage: [2, 2, 3, 4], cooldown: [-0.3, -0.3, -0.3, -0.2], aoeRadius: [15, 20, 20, 25] },
  ),
};

// ─── 4. Bumerangue (Boomerang) ─────────────────────────────────

export const WEAPON_BOOMERANG: WeaponDef = {
  id: 'boomerang',
  name: 'Boomerang',
  icon: '🪃',
  description: 'Projétil que volta ao jogador.',
  pattern: AttackPattern.Boomerang,
  color: 0x8d6e63,
  projectileRadius: 6,
  levels: levels(
    { damage: 2, cooldown: 1.2, range: 250, count: 1, pierce: 3, aoeRadius: 0, speedMult: 1.0 },
    { damage: [1, 2, 2, 3], count: [1, 0, 1, 0], range: [30, 0, 30, 40], pierce: [1, 1, 2, 2] },
  ),
};

// ─── 5. Raio (Lightning) ──────────────────────────────────────

export const WEAPON_LIGHTNING: WeaponDef = {
  id: 'lightning',
  name: 'Lightning',
  icon: '⚡',
  description: 'Relâmpago em cadeia que salta entre inimigos.',
  pattern: AttackPattern.Lightning,
  color: 0xffee58,
  levels: levels(
    { damage: 4, cooldown: 1.8, range: 200, count: 1, pierce: 2, aoeRadius: 0, speedMult: 1.0 },
    { damage: [2, 3, 3, 4], count: [0, 1, 0, 1], pierce: [1, 1, 1, 2], range: [20, 0, 30, 0] },
  ),
};

// ─── 6. Barreira (Force Field) ────────────────────────────────

export const WEAPON_FORCEFIELD: WeaponDef = {
  id: 'forcefield',
  name: 'Force Field',
  icon: '🛡️',
  description: 'Aura de dano à volta do jogador.',
  pattern: AttackPattern.ForceField,
  color: 0x80cbc4,
  levels: levels(
    { damage: 1, cooldown: 0.5, range: 0, count: 1, pierce: 0, aoeRadius: 70, speedMult: 1.0 },
    { damage: [1, 1, 1, 2], aoeRadius: [10, 15, 15, 20], cooldown: [-0.05, -0.05, -0.05, -0.05] },
  ),
};

// ─── 7. Leitão Aliado (Pig Companion) ─────────────────────────

export const WEAPON_COMPANION: WeaponDef = {
  id: 'companion',
  name: 'Pig Companion',
  icon: '🐷',
  description: 'Um leitão que orbita e ataca inimigos.',
  pattern: AttackPattern.Companion,
  color: 0xff69b4,
  projectileRadius: 3,
  levels: levels(
    { damage: 1, cooldown: 0.5, range: 150, count: 1, pierce: 0, aoeRadius: 0, speedMult: 1.0 },
    { damage: [1, 1, 2, 2], cooldown: [-0.05, -0.05, -0.05, -0.05], range: [15, 15, 20, 20], count: [0, 0, 0, 1] },
  ),
};

// ─── 8. Chuva de Fogo (Fire Rain) ─────────────────────────────

export const WEAPON_FIRERAIN: WeaponDef = {
  id: 'firerain',
  name: 'Fire Rain',
  icon: '🔥',
  description: 'Bolas de fogo caem em posições aleatórias.',
  pattern: AttackPattern.Projectile,
  color: 0xff5722,
  projectileRadius: 8,
  levels: levels(
    { damage: 3, cooldown: 2.0, range: 300, count: 1, pierce: 0, aoeRadius: 50, speedMult: 0.6 },
    { damage: [2, 2, 3, 4], count: [1, 0, 1, 1], aoeRadius: [10, 10, 15, 15], cooldown: [-0.15, -0.15, -0.1, -0.1] },
  ),
};

// ═══════════════════════════════════════════════════════════════
//  EVOLVED WEAPONS — produced by fusing two max-level weapons
// ═══════════════════════════════════════════════════════════════

// ─── E1. Trovão Certeiro (Sharpshot + Lightning) ──────────────

export const WEAPON_EVO_THUNDER_SHOT: WeaponDef = {
  id: 'evo_thundershot',
  name: 'Thunder Shot',
  icon: '🌩️',
  description: 'Projéteis elétricos que encadeiam entre inimigos.',
  pattern: AttackPattern.Projectile,
  color: 0x40c4ff,
  projectileRadius: 5,
  levels: [{
    damage: 14, cooldown: 0.35, range: 240, count: 4,
    pierce: 3, aoeRadius: 0, speedMult: 1.3,
    extra: { chain: 3 },
  }],
};

// ─── E2. Lâmina Protetora (Spinblade + ForceField) ────────────

export const WEAPON_EVO_GUARDIAN_BLADE: WeaponDef = {
  id: 'evo_guardianblade',
  name: 'Guardian Blade',
  icon: '⚔️',
  description: 'Lâminas orbitais com aura de dano contínuo.',
  pattern: AttackPattern.Orbital,
  color: 0x69f0ae,
  levels: [{
    damage: 12, cooldown: 0, range: 100, count: 6,
    pierce: -1, aoeRadius: 120, speedMult: 1.0,
  }],
};

// ─── E3. Apocalipse (Shockwave + Fire Rain) ───────────────────

export const WEAPON_EVO_APOCALYPSE: WeaponDef = {
  id: 'evo_apocalypse',
  name: 'Apocalypse',
  icon: '☄️',
  description: 'Meteoros caem do céu com ondas de choque.',
  pattern: AttackPattern.AoE,
  color: 0xff6e40,
  levels: [{
    damage: 22, cooldown: 1.6, range: 350, count: 3,
    pierce: 0, aoeRadius: 160, speedMult: 1.0,
  }],
};

// ─── E4. Leitão Bumerangue (Boomerang + Companion) ────────────

export const WEAPON_EVO_PIG_RANG: WeaponDef = {
  id: 'evo_pigrang',
  name: 'Pig Boomerang',
  icon: '🐗',
  description: 'Leitões arremessam bumerangues devastadores.',
  pattern: AttackPattern.Companion,
  color: 0xff80ab,
  projectileRadius: 7,
  levels: [{
    damage: 12, cooldown: 0.35, range: 280, count: 3,
    pierce: 5, aoeRadius: 0, speedMult: 1.2,
  }],
};

// ─── Evolution Recipes ────────────────────────────────────────

export const EVOLUTION_RECIPES: ReadonlyArray<EvolutionDef> = [
  {
    id: 'evo_thundershot',
    ingredientA: 'sharpshot',
    ingredientB: 'lightning',
    result: WEAPON_EVO_THUNDER_SHOT,
    description: '🎯 Sharpshot + ⚡ Lightning → 🌩️ Thunder Shot',
  },
  {
    id: 'evo_guardianblade',
    ingredientA: 'spinblade',
    ingredientB: 'forcefield',
    result: WEAPON_EVO_GUARDIAN_BLADE,
    description: '🔪 Spinning Blade + 🛡️ Force Field → ⚔️ Guardian Blade',
  },
  {
    id: 'evo_apocalypse',
    ingredientA: 'shockwave',
    ingredientB: 'firerain',
    result: WEAPON_EVO_APOCALYPSE,
    description: '💥 Shockwave + 🔥 Fire Rain → ☄️ Apocalypse',
  },
  {
    id: 'evo_pigrang',
    ingredientA: 'boomerang',
    ingredientB: 'companion',
    result: WEAPON_EVO_PIG_RANG,
    description: '🪃 Boomerang + 🐷 Pig Companion → 🐗 Pig Boomerang',
  },
];

// ─── Registry ─────────────────────────────────────────────────

/** All available base weapons (acquirable via upgrade popup). */
export const ALL_WEAPONS: ReadonlyArray<WeaponDef> = [
  WEAPON_SHARPSHOT,
  WEAPON_SPINBLADE,
  WEAPON_SHOCKWAVE,
  WEAPON_BOOMERANG,
  WEAPON_LIGHTNING,
  WEAPON_FORCEFIELD,
  WEAPON_COMPANION,
  WEAPON_FIRERAIN,
];

/** All evolved weapons (only via evolution). */
export const ALL_EVOLVED_WEAPONS: ReadonlyArray<WeaponDef> = [
  WEAPON_EVO_THUNDER_SHOT,
  WEAPON_EVO_GUARDIAN_BLADE,
  WEAPON_EVO_APOCALYPSE,
  WEAPON_EVO_PIG_RANG,
];

/** Lookup by id. */
export const WEAPON_BY_ID: ReadonlyMap<string, WeaponDef> = new Map(
  [...ALL_WEAPONS, ...ALL_EVOLVED_WEAPONS].map(w => [w.id, w]),
);

/** Maximum weapon slots the player can hold. */
export const MAX_WEAPON_SLOTS = 6;

/** Maximum level per weapon. */
export const MAX_WEAPON_LEVEL = 5;
