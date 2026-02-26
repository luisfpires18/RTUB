/**
 * Survive Mode — Shared types used across all subsystems.
 */
import * as PIXI from 'pixi.js';

// ─── Constants ────────────────────────────────────────────────

export const PLAYER_RADIUS = 24;
export const ENEMY_HIT_RADIUS = 24;
export const ELITE_SCALE = 1.6;
export const ELITE_SPEED_MULT = 1.4;
export const PLAYER_MAX_HP = 100;
export const ENEMY_DAMAGE = 10;
export const ELITE_DAMAGE = 20;
export const INVULN_DURATION = 0.8;

export const MINIMAP_SIZE = 100;
export const MINIMAP_MARGIN = 8;

export const XP_ORB_SPEED = 200;
export const XP_ORB_RADIUS = 5;
export const XP_PICKUP_RADIUS = 50;

// Base weapon constants (used as defaults — weapons override these)
export const ATTACK_RANGE = 180;
export const ATTACK_COOLDOWN = 0.45;
export const BASE_ATTACK_DAMAGE = 1;
export const PROJECTILE_SPEED = 350;
export const PROJECTILE_RADIUS = 4;
export const PROJECTILE_LIFETIME = 1.5;
export const BASE_ENEMY_HP = 2;
export const ELITE_HP_MULT = 3;
export const ENEMY_HP_PER_LEVEL = 1.0;

// Boss constants
export const BOSS_HP_MULT = 80;
export const BOSS_SPEED_MULT = 0.75;
export const BOSS_DAMAGE = 50;
export const BOSS_HIT_RADIUS = 56;

// ─── Enums ────────────────────────────────────────────────────

/** Attack pattern types for the weapon system. */
export const enum AttackPattern {
  Projectile = 'projectile',
  Orbital = 'orbital',
  AoE = 'aoe',
  Boomerang = 'boomerang',
  Lightning = 'lightning',
  ForceField = 'forcefield',
  Companion = 'companion',
}

// ─── Entity Interfaces ────────────────────────────────────────

export interface EnemyState {
  container: PIXI.Container;
  x: number;
  y: number;
  speed: number;
  isElite: boolean;
  isBoss?: boolean;
  isFinalBoss?: boolean;
  scale: number;
  hitRadius: number;
  wobblePhase: number;
  alive: boolean;
  hp: number;
  maxHp: number;
  /** Enemy behaviour type for variety. */
  behaviour: EnemyBehaviour;
  /** For flanker/sprinter AI — internal timer. */
  aiTimer: number;
}

export const enum EnemyBehaviour {
  Chaser = 'chaser',
  Flanker = 'flanker',
  Tank = 'tank',
  Sprinter = 'sprinter',
}

export interface XPOrb {
  gfx: PIXI.Graphics;
  x: number;
  y: number;
  lifetime: number;
}

export interface Particle {
  gfx: PIXI.Graphics;
  vx: number;
  vy: number;
  lifetime: number;
  age: number;
}

export interface Projectile {
  gfx: PIXI.Graphics;
  x: number;
  y: number;
  vx: number;
  vy: number;
  damage: number;
  lifetime: number;
  age: number;
  hitEnemies?: Set<EnemyState>;
  /** Weapon ID that fired this projectile. */
  weaponId?: string;
  /** Special behaviour flags. */
  homing?: boolean;
  homingStrength?: number;
  /** AoE radius on impact (0 = single target). */
  aoeRadius?: number;
  /** Pierce count remaining (-1 = infinite). */
  pierceLeft?: number;
  /** Boomerang state. */
  returning?: boolean;
  originX?: number;
  originY?: number;
  maxRange?: number;
}

export interface Companion {
  container: PIXI.Container;
  x: number;
  y: number;
  orbitAngle: number;
  attackCooldown: number;
  attackRange: number;
  orbitRadius: number;
  damage: number;
}

export interface JoystickState {
  bg: PIXI.Graphics;
  knob: PIXI.Graphics;
  x: number;
  y: number;
  radius: number;
  defaultX: number;
  defaultY: number;
}

// ─── Weapon System Types ──────────────────────────────────────

/** Per-level stats for a weapon definition. */
export interface WeaponLevelStats {
  damage: number;
  cooldown: number;
  range: number;
  /** Number of projectiles / orbitals / chains. */
  count: number;
  /** Pierce count (for projectile weapons). */
  pierce: number;
  /** AoE radius (for aoe/forcefield). */
  aoeRadius: number;
  /** Projectile speed multiplier. */
  speedMult: number;
  /** Special per-weapon metadata. */
  extra?: Record<string, number>;
}

/** Definition for a weapon type (static, not instance). */
export interface WeaponDef {
  id: string;
  name: string;
  icon: string;
  description: string;
  pattern: AttackPattern;
  color: number;
  /** Stats at each level (index 0 = level 1). */
  levels: WeaponLevelStats[];
  /** Projectile radius override (default PROJECTILE_RADIUS). */
  projectileRadius?: number;
}

/** Runtime weapon instance held by the player. */
export interface WeaponInstance {
  def: WeaponDef;
  level: number; // 1-based
  cooldownTimer: number;
  /** Extra runtime state per weapon (e.g. orbital angle). */
  state: Record<string, unknown>;
}

// ─── Passive/Upgrade Types ────────────────────────────────────

export interface PassiveDef {
  id: string;
  icon: string;
  name: string;
  description: string;
  maxLevel: number;
  /** Apply each level of this passive. */
  apply: (scene: PlayerStats, currentLevel: number) => void;
}

/** Mutable player stats that upgrades modify. */
export interface PlayerStats {
  playerSpeed: number;
  maxHP: number;
  playerHP: number;
  damageMultiplier: number;
  cooldownMultiplier: number;
  rangeMultiplier: number;
  magnetRadius: number;
  coinDropMult: number;
  armor: number;
}

// ─── Evolution Types ──────────────────────────────────────────

/** Recipe for evolving two max-level weapons into one evolved weapon. */
export interface EvolutionDef {
  /** Unique id for this evolution. */
  id: string;
  /** First ingredient weapon id (must be at MAX_WEAPON_LEVEL). */
  ingredientA: string;
  /** Second ingredient weapon id (must be at MAX_WEAPON_LEVEL). */
  ingredientB: string;
  /** The evolved weapon definition produced. */
  result: WeaponDef;
  /** Short flavour text shown on the card. */
  description: string;
}

// ─── Upgrade Choice ───────────────────────────────────────────

export const enum UpgradeChoiceType {
  NewWeapon = 'new_weapon',
  WeaponLevelUp = 'weapon_levelup',
  Passive = 'passive',
  Evolution = 'evolution',
}

export interface UpgradeChoice {
  type: UpgradeChoiceType;
  /** The weapon def (for new weapon / levelup). */
  weaponDef?: WeaponDef;
  /** The passive def (for passive). */
  passiveDef?: PassiveDef;
  /** The evolution def (for evolution). */
  evolutionDef?: EvolutionDef;
  /** Display label. */
  icon: string;
  title: string;
  desc: string;
}

// ─── Utility ──────────────────────────────────────────────────

export function clamp(val: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, val));
}

export function dist(a: { x: number; y: number }, b: { x: number; y: number }): number {
  return Math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2);
}

export function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

export function formatTime(s: number): string {
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return `${m}:${sec.toString().padStart(2, '0')}`;
}

// ─── Object Pool ──────────────────────────────────────────────

export class ObjectPool<T> {
  private _factory: () => T;
  private _reset: (obj: T) => void;
  private _pool: T[];

  constructor(factory: () => T, reset: (obj: T) => void, initialSize = 0) {
    this._factory = factory;
    this._reset = reset;
    this._pool = [];
    for (let i = 0; i < initialSize; i++) {
      this._pool.push(this._factory());
    }
  }

  get(): T {
    if (this._pool.length > 0) return this._pool.pop()!;
    return this._factory();
  }

  release(obj: T): void {
    this._reset(obj);
    this._pool.push(obj);
  }

  get size(): number { return this._pool.length; }
}

// ─── Spatial Hash Grid ────────────────────────────────────────

export class SpatialGrid<T extends { x: number; y: number }> {
  private cellSize: number;
  private cells = new Map<string, T[]>();

  constructor(cellSize: number) {
    this.cellSize = cellSize;
  }

  private _key(cx: number, cy: number): string {
    return `${cx},${cy}`;
  }

  clear(): void { this.cells.clear(); }

  insert(entity: T): void {
    const cx = Math.floor(entity.x / this.cellSize);
    const cy = Math.floor(entity.y / this.cellSize);
    const key = this._key(cx, cy);
    let cell = this.cells.get(key);
    if (!cell) { cell = []; this.cells.set(key, cell); }
    cell.push(entity);
  }

  query(x: number, y: number, radius = 1): T[] {
    const cx = Math.floor(x / this.cellSize);
    const cy = Math.floor(y / this.cellSize);
    const result: T[] = [];
    for (let dx = -radius; dx <= radius; dx++) {
      for (let dy = -radius; dy <= radius; dy++) {
        const cell = this.cells.get(this._key(cx + dx, cy + dy));
        if (cell) {
          for (let i = 0; i < cell.length; i++) result.push(cell[i]);
        }
      }
    }
    return result;
  }
}
