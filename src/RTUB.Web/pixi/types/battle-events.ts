/**
 * Battle event types from the C# DeterministicCombatEngine / CombatActionService.
 */

/** All possible event type strings emitted by the combat system. */
export type BattleEventType =
  | 'BattleStart'
  | 'Attack'
  | 'HPUpdate'
  | 'KO'
  | 'Victory'
  | 'StatusEffect';

/** VFX type enum mirroring the C# SpellVfxType. */
export const enum VfxType {
  Projectile = 0,
  Beam = 1,
  AoE = 2,
  // 3 unused
  Melee = 4,
  SoundWave = 5,
  MusicNotes = 6,
}

/**
 * A single battle event as emitted by the server (JSON-serialised).
 * Properties may arrive in PascalCase or camelCase depending on the serialiser.
 */
export interface BattleEvent {
  // Core
  Type?: BattleEventType;
  type?: BattleEventType;

  Attacker?: string;
  attacker?: string;

  Defender?: string;
  defender?: string;

  Damage?: number;
  damage?: number;

  IsCritical?: boolean;
  isCritical?: boolean;

  IsBlocked?: boolean;
  isBlocked?: boolean;

  IsDodged?: boolean;
  isDodged?: boolean;

  IsBoosted?: boolean;
  isBoosted?: boolean;

  SimTime?: number;
  simTime?: number;

  // HP state
  Character?: string;
  character?: string;

  HP?: number;
  hp?: number;

  MaxHP?: number;
  maxHP?: number;

  ActionTime?: number;
  actionTime?: number;

  // Spell / VFX
  AttackId?: string;
  attackId?: string;

  VfxType?: VfxType;
  vfxType?: VfxType;

  VfxColor?: string | number;
  vfxColor?: string | number;

  ScreenShake?: boolean;
  screenShake?: boolean;

  VisualHint?: string;
  visualHint?: string;

  AbilityName?: string;
  abilityName?: string;

  EffectName?: string;
  effectName?: string;

  // Multi-target (AoE spells)
  Targets?: string[];
  targets?: string[];

  TargetDamages?: number[];
  targetDamages?: number[];
}
