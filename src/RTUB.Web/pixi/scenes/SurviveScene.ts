/**
 * SurviveScene — survivor.io-inspired game engine built on PixiJS (TypeScript).
 *
 * The player spawns center-map on a large scrollable map.
 * Enemies swarm from all edges. Dodge them until the timer runs out.
 * Beat the timer → next level. Get touched → game over.
 */
import * as PIXI from 'pixi.js';
import { SESSION_CACHE_BUST, audioCacheBuster } from '@shared/utils';
import type { SurviveLevelData } from '@types/survive-data';

// ─── Constants ────────────────────────────────────────────────

const PLAYER_RADIUS = 24;
const ENEMY_HIT_RADIUS = 24;
const ELITE_SCALE = 1.6;
const ELITE_SPEED_MULT = 1.4;
const PLAYER_MAX_HP = 100;
const ENEMY_DAMAGE = 10;
const ELITE_DAMAGE = 20;
const INVULN_DURATION = 0.8;

const MINIMAP_SIZE = 100;
const MINIMAP_MARGIN = 8;

const XP_ORB_SPEED = 200;
const XP_ORB_RADIUS = 5;
const XP_PICKUP_RADIUS = 50;

// Auto-attack constants
const ATTACK_RANGE = 180;
const ATTACK_COOLDOWN = 0.45;
const BASE_ATTACK_DAMAGE = 1;
const PROJECTILE_SPEED = 350;
const PROJECTILE_RADIUS = 4;
const PROJECTILE_LIFETIME = 1.5;
const BASE_ENEMY_HP = 2;
const ELITE_HP_MULT = 3;
const ENEMY_HP_PER_LEVEL = 1.0;

// Boss constants
const BOSS_HP_MULT = 80;
const BOSS_SPEED_MULT = 0.75;
const BOSS_DAMAGE = 50;
const BOSS_HIT_RADIUS = 56;

// ─── Upgrade System ───────────────────────────────────────────

const UPGRADE_THRESHOLDS = [5, 10, 25, 65];
const UPGRADE_THRESHOLD_STEP = 45;

interface UpgradeDef {
  id: string;
  icon: string;
  title: string;
  desc: string;
  unique?: boolean;
  maxPicks?: number;
  apply: (scene: SurviveScene) => void;
}

const UPGRADE_DEFS: UpgradeDef[] = [
  {
    id: 'moveSpeed',
    icon: '🏃',
    title: 'Pés Rápidos',
    desc: '+15% velocidade de movimento',
    apply: (s) => { s.playerSpeed *= 1.15; },
  },
  {
    id: 'atkSpeed',
    icon: '⚡',
    title: 'Fogo Rápido',
    desc: '+20% velocidade de ataque',
    apply: (s) => { s.attackCooldownMult *= 0.80; },
  },
  {
    id: 'hp',
    icon: '❤️',
    title: 'Vitalidade',
    desc: '+10 HP Máximo e cura 5',
    apply: (s) => {
      s.maxHP += 10;
      s.playerHP = Math.min(s.playerHP + 5, s.maxHP);
    },
  },
  {
    id: 'damage',
    icon: '⚔️',
    title: 'Tiro Potente',
    desc: '2x dano de ataque',
    apply: (s) => { s.attackDamage *= 2; },
  },
  {
    id: 'coinRate',
    icon: '🪙',
    title: 'Febre do Ouro',
    desc: '2x moedas por inimigo',
    maxPicks: 3,
    apply: (s) => { s.coinDropMult *= 2; },
  },
  {
    id: 'atkRange',
    icon: '🎯',
    title: 'Olho de Águia',
    desc: '+10% alcance de ataque',
    apply: (s) => { s.attackRange *= 1.10; },
  },
  {
    id: 'magnet',
    icon: '🧲',
    title: 'Íman de Moedas',
    desc: 'Moedas voam para ti de longe',
    unique: true,
    apply: (s) => { s.magnetRadius += 150; },
  },
  {
    id: 'companion',
    icon: '🐷',
    title: 'Leitão',
    desc: 'Um leitão pronto a ser praxado e atacar inimigos próximos',
    unique: true,
    apply: (s) => { s.spawnCompanion(); },
  },
];

// ─── SFX Audio Pool (HTML Audio element pool) ─────────────────

const SFX_POOL_SIZE = 4;
const SFX_MAP: Record<string, string> = {
  win: '/audio/games/my-tuno/survive/win.mp3',
  death: '/audio/games/my-tuno/survive/death.mp3',
  hit: '/audio/games/my-tuno/survive/hit.mp3',
};

const sfxPool: Record<string, HTMLAudioElement[]> = {};

function getPooledAudio(type: string): HTMLAudioElement | null {
  const src = SFX_MAP[type];
  if (!src) return null;
  if (!sfxPool[type]) {
    sfxPool[type] = [];
    for (let i = 0; i < SFX_POOL_SIZE; i++) {
      const a = new Audio(src + audioCacheBuster);
      a.volume = 0.5;
      sfxPool[type].push(a);
    }
  }
  for (const a of sfxPool[type]) {
    if (a.paused || a.ended) {
      a.currentTime = 0;
      return a;
    }
  }
  const a = sfxPool[type][0];
  a.currentTime = 0;
  return a;
}

// ─── Utilities ────────────────────────────────────────────────

function clamp(val: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, val));
}

function dist(a: { x: number; y: number }, b: { x: number; y: number }): number {
  return Math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2);
}

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

function formatTime(s: number): string {
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return `${m}:${sec.toString().padStart(2, '0')}`;
}

// ─── Object Pool ──────────────────────────────────────────────

class ObjectPool<T> {
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

class SpatialGrid {
  private cellSize: number;
  private cells = new Map<string, EnemyState[]>();

  constructor(cellSize: number) {
    this.cellSize = cellSize;
  }

  private _key(cx: number, cy: number): string {
    return `${cx},${cy}`;
  }

  clear(): void { this.cells.clear(); }

  insert(entity: EnemyState): void {
    const cx = Math.floor(entity.x / this.cellSize);
    const cy = Math.floor(entity.y / this.cellSize);
    const key = this._key(cx, cy);
    let cell = this.cells.get(key);
    if (!cell) { cell = []; this.cells.set(key, cell); }
    cell.push(entity);
  }

  query(x: number, y: number): EnemyState[] {
    const cx = Math.floor(x / this.cellSize);
    const cy = Math.floor(y / this.cellSize);
    const result: EnemyState[] = [];
    for (let dx = -1; dx <= 1; dx++) {
      for (let dy = -1; dy <= 1; dy++) {
        const cell = this.cells.get(this._key(cx + dx, cy + dy));
        if (cell) {
          for (let i = 0; i < cell.length; i++) result.push(cell[i]);
        }
      }
    }
    return result;
  }
}

// ─── Internal Types ───────────────────────────────────────────

interface EnemyState {
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
}

interface XPOrb {
  gfx: PIXI.Graphics;
  x: number;
  y: number;
  lifetime: number;
}

interface Particle {
  gfx: PIXI.Graphics;
  vx: number;
  vy: number;
  lifetime: number;
  age: number;
}

interface Projectile {
  gfx: PIXI.Graphics;
  x: number;
  y: number;
  vx: number;
  vy: number;
  damage: number;
  lifetime: number;
  age: number;
  hitEnemies?: Set<EnemyState>;
}

interface Companion {
  container: PIXI.Container;
  x: number;
  y: number;
  orbitAngle: number;
  attackCooldown: number;
  attackRange: number;
  orbitRadius: number;
}

interface JoystickState {
  bg: PIXI.Graphics;
  knob: PIXI.Graphics;
  x: number;
  y: number;
  radius: number;
  defaultX: number;
  defaultY: number;
}

// ─── Module-Level State ───────────────────────────────────────

let app: PIXI.Application | null = null;
let bgMusic: HTMLAudioElement | null = null;
let bgMusicLoaded = false;
let audioEnabled = true;

// ─── SurviveScene ─────────────────────────────────────────────

export class SurviveScene {
  readonly containerId: string;
  private data: SurviveLevelData;

  // Level config
  private level: number;
  private biomeName: string;
  private timerDuration: number;
  private baseEnemyCount: number;
  private maxEnemyCount: number;
  private spawnInterval: number;
  private enemySpeed: number;
  private maxEnemySpeed: number;
  playerSpeed: number; // public — upgrade mutates
  private enemyScale: number;
  private hasElites: boolean;
  private eliteChance: number;
  private mapWidth: number;
  private mapHeight: number;
  private vpWidth: number;
  private vpHeight: number;
  private backgroundPath: string;
  private enemySprites: string[];
  private playerSpritePath: string;
  private bossSprites: string[];
  private isFinalLevel: boolean;
  private spawnRampPerMinute: number;
  private speedRampPerMinute: number;

  // Runtime state
  private timeRemaining: number;
  private timeElapsed = 0;
  private enemiesKilled = 0;
  private xpOrbsCollected = 0;
  private totalSpawned = 0;
  private spawnTimer = 0;
  private alive = true;
  private won = false;
  private speedRampTimer = 0;
  private currentEnemySpeed: number;

  // Containers & entities
  private worldContainer!: PIXI.Container;
  private uiContainer!: PIXI.Container;
  private enemies: EnemyState[] = [];
  private _enemyContainerPool: PIXI.Container[] = [];
  private _spatialGrid = new SpatialGrid(128);
  private xpOrbs: XPOrb[] = [];
  private particles: Particle[] = [];
  private projectiles: Projectile[] = [];

  // Object pools
  private _projectilePool: ObjectPool<PIXI.Graphics> | null = null;
  private _orbPool: ObjectPool<PIXI.Graphics> | null = null;
  private _particlePool: ObjectPool<PIXI.Graphics> | null = null;

  // Auto-attack state
  private attackCooldown = 0;
  attackDamage = BASE_ATTACK_DAMAGE; // public — upgrade mutates
  attackCooldownMult = 1.0;           // public — upgrade mutates
  attackRange = ATTACK_RANGE;         // public — upgrade mutates

  // Player HP
  playerHP = PLAYER_MAX_HP;   // public — upgrade mutates
  maxHP = PLAYER_MAX_HP;       // public — upgrade mutates
  private invulnTimer = 0;

  // Upgrade system
  private upgradeIndex = 0;
  private nextUpgradeAt = UPGRADE_THRESHOLDS[0];
  private upgradesPicked = 0;
  private maxPowerUps: number;
  private pickedUpgradeIds = new Set<string>();
  private upgradePickCounts: Record<string, number> = {};
  coinDropMult = 1;      // public — upgrade mutates
  magnetRadius: number;    // public — upgrade mutates
  companions: Companion[] = [];
  private upgradePaused = false;
  private upgradeOverlay: PIXI.Container | null = null;

  // Per-minute ramp
  private lastMinuteRamp = 0;
  private spawnRampBonus = 0;
  private speedRampBonus = 0;

  // Boss state
  private midBossSpawned = false;
  private finalBossSpawned = false;
  private activeBoss: EnemyState | null = null;
  private bossHPBarBg: PIXI.Graphics | null = null;
  private bossHPBarFill: PIXI.Graphics | null = null;
  private bossHPBarText: PIXI.Text | null = null;
  private _bossBarX = 0;
  private _bossBarY = 0;
  private _bossBarW = 0;
  private _bossBarH = 0;
  private _bossBarFinal = false;
  private timerStopped = false;

  // Input
  private keys: Record<string, boolean> = {};
  private touchActive = false;
  private touchTarget = { x: 0, y: 0 };
  private joystick: JoystickState | null = null;
  private joystickActive = false;
  private joystickAngle = 0;
  private joystickMagnitude = 0;
  private _joystickPointerId: number | null = null;
  private _isTouchDevice = false;

  // Event handler refs (for cleanup)
  private _onKeyDown: ((e: KeyboardEvent) => void) | null = null;
  private _onKeyUp: ((e: KeyboardEvent) => void) | null = null;
  private _onPointerDown: ((e: PointerEvent) => void) | null = null;
  private _onPointerMove: ((e: PointerEvent) => void) | null = null;
  private _onPointerUp: ((e: PointerEvent) => void) | null = null;
  private _onPointerCancel: ((e: PointerEvent) => void) | null = null;

  // Player display
  private player: PIXI.Container | null = null;
  private playerX: number;
  private playerY: number;
  private playerGlow: PIXI.Graphics | null = null;
  private playerArrow: PIXI.Graphics | null = null;

  // Camera
  private camX: number;
  private camY: number;

  // Asset aliases
  private _bgAlias = '';
  private _playerAlias = '';
  private enemySpriteAliases: string[] = [];
  private bossSpriteAliases: string[] = [];

  // UI refs
  private timerText!: PIXI.Text;
  private timerBar!: PIXI.Graphics;
  private timerBarWidth = 0;
  private timerBarHeight = 0;
  private timerBarX = 0;
  private timerBarY = 0;
  private hpBar!: PIXI.Graphics;
  private hpBarWidth = 0;
  private hpBarHeight = 0;
  private hpBarX = 0;
  private hpBarY = 0;
  private hpText: PIXI.Text | null = null;
  private killText!: PIXI.Text;
  private orbText: PIXI.Text | null = null;
  private statsText: PIXI.Text | null = null;
  private minimapContainer: PIXI.Container | null = null;
  private _mmPlayerDot: PIXI.Graphics | null = null;
  private _mmEnemyDots: PIXI.Graphics | null = null;
  private _mmVpRect: PIXI.Graphics | null = null;
  private mmX = 0;
  private mmY = 0;

  // ──────────────────────────────────────────────────────────────

  constructor(containerId: string, levelData: SurviveLevelData) {
    this.containerId = containerId;
    this.data = levelData;

    // Level config (Blazor sends camelCase JSON)
    this.level = levelData.level ?? 1;
    this.biomeName = levelData.biomeName ?? 'Forest';
    this.timerDuration = levelData.timerDurationSeconds ?? 60;
    this.baseEnemyCount = levelData.baseEnemyCount ?? 5;
    this.maxEnemyCount = levelData.maxEnemyCount ?? 80;
    this.spawnInterval = levelData.spawnIntervalSeconds ?? 2;
    this.enemySpeed = levelData.enemySpeed ?? 60;
    this.maxEnemySpeed = levelData.maxEnemySpeed ?? 150;
    this.playerSpeed = levelData.playerSpeed ?? 120;
    this.enemyScale = levelData.enemyScale ?? 1;
    this.hasElites = levelData.hasEliteEnemies ?? false;
    this.eliteChance = levelData.eliteSpawnChance ?? 0;
    this.mapWidth = levelData.mapWidth ?? 2000;
    this.mapHeight = levelData.mapHeight ?? 2000;

    this.vpWidth = levelData.viewportWidth ?? 800;
    this.vpHeight = levelData.viewportHeight ?? 600;
    this.backgroundPath = levelData.backgroundPath ?? '';
    this.enemySprites = levelData.enemySprites ?? [];
    this.playerSpritePath = levelData.playerSpritePath ?? '';
    this.bossSprites = levelData.bossSprites ?? [];
    this.isFinalLevel = levelData.isFinalLevel ?? false;
    this.spawnRampPerMinute = levelData.spawnRampPerMinute ?? 0.20;
    this.speedRampPerMinute = levelData.speedRampPerMinute ?? 0.10;

    // Runtime
    this.timeRemaining = this.timerDuration;
    this.currentEnemySpeed = this.enemySpeed;

    // Upgrade limits
    this.maxPowerUps = Math.floor(this.timerDuration / 60) + (this.level - 1) * 2;
    this.magnetRadius = XP_PICKUP_RADIUS;

    // Player position
    this.playerX = this.mapWidth / 2;
    this.playerY = this.mapHeight / 2;

    // Camera
    this.camX = clamp(this.playerX - this.vpWidth / 2, 0, this.mapWidth - this.vpWidth);
    this.camY = clamp(this.playerY - this.vpHeight / 2, 0, this.mapHeight - this.vpHeight);
  }

  // ─── Initialisation ─────────────────────────────────────────

  async init(): Promise<void> {
    const container = document.getElementById(this.containerId);
    if (!container) throw new Error(`Container #${this.containerId} not found`);

    container.innerHTML = '';

    const containerW = container.clientWidth || this.vpWidth;
    const containerH = container.clientHeight || this.vpHeight;
    this.vpWidth = containerW;
    this.vpHeight = containerH;

    if (!app) {
      app = new PIXI.Application();
      await app.init({
        width: containerW,
        height: containerH,
        backgroundColor: 0x1a1a2e,
        antialias: true,
        resolution: window.devicePixelRatio || 1,
        autoDensity: true,
        resizeTo: container,
      });
    } else {
      app.renderer.resize(containerW, containerH);
    }

    container.appendChild(app.canvas);

    this.worldContainer = new PIXI.Container();
    app.stage.addChild(this.worldContainer);

    this.uiContainer = new PIXI.Container();
    app.stage.addChild(this.uiContainer);

    this._initPools();

    this.worldContainer.position.set(-this.camX, -this.camY);

    await this.loadAssets();
    this.createBackground();
    this.createPlayer();
    this.createUI();
    this.setupInput();
    this.spawnInitialEnemies();
    this.playMusic();

    this.alive = true;
    this.won = false;
    gameActive = true;
    app.ticker.add(this.update, this);
  }

  // ─── Asset Loading ──────────────────────────────────────────

  private async loadAssets(): Promise<void> {
    const assets: { alias: string; src: string }[] = [];
    const cacheBust = SESSION_CACHE_BUST;

    if (this.backgroundPath) {
      this._bgAlias = `surviveBg_${this.backgroundPath}`;
      assets.push({ alias: this._bgAlias, src: this.backgroundPath + cacheBust });
    }

    if (this.playerSpritePath) {
      this._playerAlias = `survivePlayer_${this.playerSpritePath}`;
      assets.push({ alias: this._playerAlias, src: this.playerSpritePath + cacheBust });
    }

    for (let i = 0; i < this.enemySprites.length; i++) {
      const alias = `surviveEnemy_${i}_${this.enemySprites[i]}`;
      assets.push({ alias, src: this.enemySprites[i] + cacheBust });
      this.enemySpriteAliases.push(alias);
    }

    for (let i = 0; i < this.bossSprites.length; i++) {
      const alias = `surviveBoss_${i}_${this.bossSprites[i]}`;
      assets.push({ alias, src: this.bossSprites[i] + cacheBust });
      this.bossSpriteAliases.push(alias);
    }

    if (assets.length > 0) {
      try {
        await PIXI.Assets.load(assets);
      } catch (e) {
        console.warn('Some survive mode assets failed to load:', e);
      }
    }
  }

  // ─── Background ─────────────────────────────────────────────

  private createBackground(): void {
    const biomeColors: Record<string, number> = {
      Forest: 0x2d5a27, Swamp: 0x3a4a2a, Mountains: 0x6b6b6b,
      Snowy: 0xd0e0f0, Tropical: 0x3a8a5a, Caverns: 0x2a2a3a,
      Desert: 0xc4a35a, Volcanic: 0x4a1a1a, Ruins: 0x4a4a3a,
      Dark: 0x1a1a2a, Light: 0xf5e6a0, Void: 0x0a0a1a,
    };
    const bgColor = biomeColors[this.biomeName] ?? 0x2d5a27;

    const pad = Math.max(this.vpWidth, this.vpHeight);
    const ground = new PIXI.Graphics();
    ground.rect(-pad, -pad, this.mapWidth + pad * 2, this.mapHeight + pad * 2);
    ground.fill(bgColor);
    this.worldContainer.addChild(ground);

    // Grid lines for spatial awareness
    const grid = new PIXI.Graphics();
    grid.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.05 });
    for (let x = 0; x <= this.mapWidth; x += 100) {
      grid.moveTo(x, 0);
      grid.lineTo(x, this.mapHeight);
    }
    for (let y = 0; y <= this.mapHeight; y += 100) {
      grid.moveTo(0, y);
      grid.lineTo(this.mapWidth, y);
    }
    grid.stroke();
    this.worldContainer.addChild(grid);

    // Map boundary markers
    const border = new PIXI.Graphics();
    border.setStrokeStyle({ width: 4, color: 0xff4444, alpha: 0.6 });
    border.rect(0, 0, this.mapWidth, this.mapHeight);
    border.stroke();
    this.worldContainer.addChild(border);

    // Biome decorations
    const decoColors: Record<string, number> = {
      Forest: 0x1a4a17, Swamp: 0x2a3a1a, Mountains: 0x8a8a8a,
      Snowy: 0xffffff, Tropical: 0x2a6a3a, Caverns: 0x3a3a5a,
      Desert: 0xd4b36a, Volcanic: 0x6a2a1a, Ruins: 0x5a5a4a,
      Dark: 0x2a2a4a, Light: 0xd4c870, Void: 0x1a1a3a,
    };
    const decoColor = decoColors[this.biomeName] ?? 0x1a4a17;
    const decorations = new PIXI.Graphics();
    for (let i = 0; i < 200; i++) {
      const x = Math.random() * this.mapWidth;
      const y = Math.random() * this.mapHeight;
      const r = 2 + Math.random() * 6;
      decorations.circle(x, y, r);
    }
    decorations.fill({ color: decoColor, alpha: 0.3 });
    this.worldContainer.addChild(decorations);

    // Try loading background texture as tiled overlay
    try {
      const bgAlias = this._bgAlias || `surviveBg_${this.level}`;
      const bgTexture = PIXI.Assets.get(bgAlias);
      if (bgTexture) {
        const tileW = this.vpWidth;
        const tileH = this.vpHeight;
        for (let tx = 0; tx < this.mapWidth; tx += tileW) {
          for (let ty = 0; ty < this.mapHeight; ty += tileH) {
            const bgSprite = new PIXI.Sprite(bgTexture);
            bgSprite.width = tileW;
            bgSprite.height = tileH;
            bgSprite.alpha = 0.15;
            bgSprite.position.set(tx, ty);
            this.worldContainer.addChild(bgSprite);
          }
        }
      }
    } catch { /* no background texture */ }
  }

  // ─── Player ─────────────────────────────────────────────────

  private createPlayer(): void {
    const playerContainer = new PIXI.Container();

    let playerSprite: PIXI.Sprite | null = null;
    try {
      const tex = PIXI.Assets.get(this._playerAlias || 'survivePlayer');
      if (tex) {
        playerSprite = new PIXI.Sprite(tex);
        playerSprite.anchor.set(0.5, 0.5);
        const maxSize = PLAYER_RADIUS * 4;
        const s = Math.min(maxSize / playerSprite.width, maxSize / playerSprite.height);
        playerSprite.scale.set(s);
        playerContainer.addChild(playerSprite);
      }
    } catch { /* fallback below */ }

    if (!playerSprite) {
      const gfx = new PIXI.Graphics();
      gfx.circle(0, 0, PLAYER_RADIUS);
      gfx.fill(0x4fc3f7);
      gfx.setStrokeStyle({ width: 2, color: 0xffffff });
      gfx.stroke();
      playerContainer.addChild(gfx);

      const arrow = new PIXI.Graphics();
      arrow.moveTo(PLAYER_RADIUS, 0);
      arrow.lineTo(PLAYER_RADIUS - 6, -5);
      arrow.lineTo(PLAYER_RADIUS - 6, 5);
      arrow.closePath();
      arrow.fill(0xffffff);
      playerContainer.addChild(arrow);
      this.playerArrow = arrow;
    }

    // Glow
    const glow = new PIXI.Graphics();
    glow.circle(0, 0, PLAYER_RADIUS + 6);
    glow.fill({ color: 0x4fc3f7, alpha: 0.15 });
    playerContainer.addChildAt(glow, 0);
    this.playerGlow = glow;

    playerContainer.position.set(this.playerX, this.playerY);
    this.worldContainer.addChild(playerContainer);
    this.player = playerContainer;
  }

  // ─── UI ─────────────────────────────────────────────────────

  private createUI(): void {
    // Timer bar + text (top center)
    const timerBarW = 240;
    const timerBarH = 24;
    const timerBarX = this.vpWidth / 2 - timerBarW / 2;
    const timerBarY = 10;
    const timerBarBg = new PIXI.Graphics();
    timerBarBg.roundRect(timerBarX, timerBarY, timerBarW, timerBarH, 6);
    timerBarBg.fill({ color: 0x000000, alpha: 0.7 });
    this.uiContainer.addChild(timerBarBg);

    this.timerBar = new PIXI.Graphics();
    this.timerBarWidth = timerBarW;
    this.timerBarHeight = timerBarH;
    this.timerBarX = timerBarX;
    this.timerBarY = timerBarY;
    this.updateTimerBar();
    this.uiContainer.addChild(this.timerBar);

    this.timerText = new PIXI.Text({
      text: formatTime(this.timerDuration),
      style: {
        fontFamily: 'Arial', fontSize: 14, fontWeight: 'bold',
        fill: 0xffffff, align: 'center',
      },
    });
    this.timerText.anchor.set(0.5, 0.5);
    this.timerText.position.set(this.vpWidth / 2, timerBarY + timerBarH / 2);
    this.uiContainer.addChild(this.timerText);

    // HP bar (red, below timer)
    const hpBarW = 240;
    const hpBarH = 14;
    const hpBarX = this.vpWidth / 2 - hpBarW / 2;
    const hpBarY = timerBarY + timerBarH + 4;
    const hpBarBg = new PIXI.Graphics();
    hpBarBg.roundRect(hpBarX, hpBarY, hpBarW, hpBarH, 5);
    hpBarBg.fill({ color: 0x1a0000, alpha: 0.8 });
    this.uiContainer.addChild(hpBarBg);

    this.hpBar = new PIXI.Graphics();
    this.hpBarWidth = hpBarW;
    this.hpBarHeight = hpBarH;
    this.hpBarX = hpBarX;
    this.hpBarY = hpBarY;
    this.updateHPBar();
    this.uiContainer.addChild(this.hpBar);

    this.hpText = new PIXI.Text({
      text: `${this.playerHP}/${this.maxHP}`,
      style: {
        fontFamily: 'Arial', fontSize: 10, fontWeight: 'bold',
        fill: 0xffffff, align: 'center',
      },
    });
    this.hpText.anchor.set(0.5, 0.5);
    this.hpText.position.set(this.vpWidth / 2, hpBarY + hpBarH / 2);
    this.uiContainer.addChild(this.hpText);

    // Level / Biome (top left)
    const levelBg = new PIXI.Graphics();
    levelBg.roundRect(8, 10, 160, 24, 5);
    levelBg.fill({ color: 0x000000, alpha: 0.6 });
    this.uiContainer.addChild(levelBg);

    const levelText = new PIXI.Text({
      text: `Lv.${this.level} — ${this.biomeName}`,
      style: {
        fontFamily: 'Arial', fontSize: 13, fontWeight: 'bold',
        fill: 0xffcc00,
      },
    });
    levelText.position.set(14, 13);
    this.uiContainer.addChild(levelText);

    // Stats panel
    const statsBg = new PIXI.Graphics();
    statsBg.roundRect(8, 38, 160, 52, 5);
    statsBg.fill({ color: 0x000000, alpha: 0.5 });
    this.uiContainer.addChild(statsBg);

    this.statsText = new PIXI.Text({
      text: this._buildStatsString(),
      style: { fontFamily: 'Arial', fontSize: 10, fill: 0xcccccc, lineHeight: 13 },
    });
    this.statsText.position.set(14, 41);
    this.uiContainer.addChild(this.statsText);

    // Kill counter (top right)
    const killBg = new PIXI.Graphics();
    killBg.roundRect(this.vpWidth - 120, 10, 112, 24, 5);
    killBg.fill({ color: 0x000000, alpha: 0.6 });
    this.uiContainer.addChild(killBg);

    this.killText = new PIXI.Text({
      text: '☠ 0',
      style: { fontFamily: 'Arial', fontSize: 13, fontWeight: 'bold', fill: 0xff6666 },
    });
    this.killText.position.set(this.vpWidth - 114, 13);
    this.uiContainer.addChild(this.killText);

    // Coin counter
    const orbBg = new PIXI.Graphics();
    orbBg.roundRect(this.vpWidth - 120, 38, 112, 24, 5);
    orbBg.fill({ color: 0x000000, alpha: 0.6 });
    this.uiContainer.addChild(orbBg);

    this.orbText = new PIXI.Text({
      text: '🪙 0',
      style: { fontFamily: 'Arial', fontSize: 13, fontWeight: 'bold', fill: 0xffd700 },
    });
    this.orbText.position.set(this.vpWidth - 114, 41);
    this.uiContainer.addChild(this.orbText);

    this.createMinimap();
    this.createJoystick();
  }

  private createMinimap(): void {
    const x = this.vpWidth - MINIMAP_SIZE - MINIMAP_MARGIN;
    const y = this.vpHeight - MINIMAP_SIZE - MINIMAP_MARGIN;

    const mmBg = new PIXI.Graphics();
    mmBg.roundRect(x, y, MINIMAP_SIZE, MINIMAP_SIZE, 4);
    mmBg.fill({ color: 0x000000, alpha: 0.5 });
    mmBg.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.3 });
    mmBg.stroke();
    this.uiContainer.addChild(mmBg);

    this._mmPlayerDot = new PIXI.Graphics();
    this._mmEnemyDots = new PIXI.Graphics();
    this._mmVpRect = new PIXI.Graphics();

    this.minimapContainer = new PIXI.Container();
    this.minimapContainer.position.set(x, y);
    this.minimapContainer.addChild(this._mmEnemyDots);
    this.minimapContainer.addChild(this._mmPlayerDot);
    this.minimapContainer.addChild(this._mmVpRect);
    this.uiContainer.addChild(this.minimapContainer);

    this.mmX = x;
    this.mmY = y;
  }

  private createJoystick(): void {
    const joyRadius = 64;
    const knobRadius = 26;
    const defaultX = 100;
    const defaultY = this.vpHeight - 100;

    const joyBg = new PIXI.Graphics();
    joyBg.circle(0, 0, joyRadius);
    joyBg.fill({ color: 0xffffff, alpha: 0.15 });
    joyBg.setStrokeStyle({ width: 2, color: 0xffffff, alpha: 0.3 });
    joyBg.stroke();
    joyBg.position.set(defaultX, defaultY);
    joyBg.alpha = 0;
    this.uiContainer.addChild(joyBg);

    const joyKnob = new PIXI.Graphics();
    joyKnob.circle(0, 0, knobRadius);
    joyKnob.fill({ color: 0xffffff, alpha: 0.5 });
    joyKnob.position.set(defaultX, defaultY);
    joyKnob.alpha = 0;
    this.uiContainer.addChild(joyKnob);

    this.joystick = {
      bg: joyBg,
      knob: joyKnob,
      x: defaultX,
      y: defaultY,
      radius: joyRadius,
      defaultX,
      defaultY,
    };
    this._joystickPointerId = null;
  }

  private updateJoystick(localX: number, localY: number): void {
    if (!this.joystick) return;
    const dx = localX - this.joystick.x;
    const dy = localY - this.joystick.y;
    const d = Math.sqrt(dx * dx + dy * dy);
    const maxD = this.joystick.radius;
    const clamped = Math.min(d, maxD);

    this.joystickAngle = Math.atan2(dy, dx);
    this.joystickMagnitude = clamped / maxD;

    this.joystick.knob.position.set(
      this.joystick.x + Math.cos(this.joystickAngle) * clamped,
      this.joystick.y + Math.sin(this.joystickAngle) * clamped,
    );
  }

  private resetJoystick(): void {
    if (!this.joystick) return;
    this.joystickActive = false;
    this.joystickMagnitude = 0;
    this._joystickPointerId = null;
    this.joystick.bg.alpha = 0;
    this.joystick.knob.alpha = 0;
    this.joystick.x = this.joystick.defaultX;
    this.joystick.y = this.joystick.defaultY;
    this.joystick.bg.position.set(this.joystick.x, this.joystick.y);
    this.joystick.knob.position.set(this.joystick.x, this.joystick.y);
  }

  // ─── Input ──────────────────────────────────────────────────

  private setupInput(): void {
    this._isTouchDevice = ('ontouchstart' in window) || (navigator.maxTouchPoints > 0);

    this._onKeyDown = (e: KeyboardEvent) => {
      this.keys[e.key.toLowerCase()] = true;
      e.preventDefault();
    };
    this._onKeyUp = (e: KeyboardEvent) => {
      this.keys[e.key.toLowerCase()] = false;
    };
    window.addEventListener('keydown', this._onKeyDown);
    window.addEventListener('keyup', this._onKeyUp);

    if (app?.canvas) {
      (app.canvas as HTMLCanvasElement).style.touchAction = 'none';

      this._onPointerDown = (e: PointerEvent) => {
        const rect = (app!.canvas as HTMLCanvasElement).getBoundingClientRect();
        const scaleX = this.vpWidth / rect.width;
        const scaleY = this.vpHeight / rect.height;
        const localX = (e.clientX - rect.left) * scaleX;
        const localY = (e.clientY - rect.top) * scaleY;

        if (this._isTouchDevice) {
          if (localX < this.vpWidth * 0.55 && this._joystickPointerId === null && this.joystick) {
            this._joystickPointerId = e.pointerId;
            this.joystick.x = localX;
            this.joystick.y = localY;
            this.joystick.bg.position.set(localX, localY);
            this.joystick.knob.position.set(localX, localY);
            this.joystick.bg.alpha = 1;
            this.joystick.knob.alpha = 1;
            this.joystickActive = true;
            this.joystickMagnitude = 0;
          }
        } else {
          this.touchActive = true;
          this.touchTarget.x = localX + this.camX;
          this.touchTarget.y = localY + this.camY;
        }
      };

      this._onPointerMove = (e: PointerEvent) => {
        const rect = (app!.canvas as HTMLCanvasElement).getBoundingClientRect();
        const scaleX = this.vpWidth / rect.width;
        const scaleY = this.vpHeight / rect.height;
        const localX = (e.clientX - rect.left) * scaleX;
        const localY = (e.clientY - rect.top) * scaleY;

        if (this._isTouchDevice) {
          if (this.joystickActive && e.pointerId === this._joystickPointerId) {
            this.updateJoystick(localX, localY);
          }
        } else {
          if (!this.touchActive) return;
          this.touchTarget.x = localX + this.camX;
          this.touchTarget.y = localY + this.camY;
        }
      };

      this._onPointerUp = (e: PointerEvent) => {
        if (this._isTouchDevice) {
          if (e.pointerId === this._joystickPointerId) this.resetJoystick();
        } else {
          this.touchActive = false;
        }
      };

      this._onPointerCancel = (e: PointerEvent) => {
        if (e.pointerId === this._joystickPointerId) this.resetJoystick();
      };

      app.canvas.addEventListener('pointerdown', this._onPointerDown);
      app.canvas.addEventListener('pointermove', this._onPointerMove);
      app.canvas.addEventListener('pointerup', this._onPointerUp);
      app.canvas.addEventListener('pointercancel', this._onPointerCancel);
    }
  }

  // ─── Enemy Spawning ─────────────────────────────────────────

  private spawnInitialEnemies(): void {
    for (let i = 0; i < this.baseEnemyCount; i++) this.spawnEnemy();
  }

  private _getEnemyContainer(): PIXI.Container {
    if (this._enemyContainerPool.length > 0) {
      const c = this._enemyContainerPool.pop()!;
      c.visible = true;
      c.alpha = 1;
      return c;
    }
    return new PIXI.Container();
  }

  private _releaseEnemyContainer(container: PIXI.Container): void {
    if (!container) return;
    if (container.parent) container.parent.removeChild(container);
    while (container.children.length > 0) {
      const child = container.children[0];
      container.removeChild(child);
      if (child.destroy) {
        try { child.destroy({ children: false, texture: false }); } catch { /* noop */ }
      }
    }
    container.visible = false;
    if (this._enemyContainerPool.length < 50) {
      this._enemyContainerPool.push(container);
    } else {
      try { container.destroy({ children: true }); } catch { /* noop */ }
    }
  }

  private spawnEnemy(): void {
    if (this.enemies.length >= this.maxEnemyCount) return;

    const side = Math.floor(Math.random() * 4);
    let ex = 0;
    let ey = 0;
    const margin = 80;

    switch (side) {
      case 0: ex = Math.random() * this.mapWidth; ey = Math.max(0, this.camY - margin); break;
      case 1: ex = Math.min(this.mapWidth, this.camX + this.vpWidth + margin); ey = Math.random() * this.mapHeight; break;
      case 2: ex = Math.random() * this.mapWidth; ey = Math.min(this.mapHeight, this.camY + this.vpHeight + margin); break;
      case 3: ex = Math.max(0, this.camX - margin); ey = Math.random() * this.mapHeight; break;
    }

    const isElite = this.hasElites && Math.random() < this.eliteChance;
    const scale = isElite ? this.enemyScale * ELITE_SCALE : this.enemyScale;
    const speed = isElite
      ? this.currentEnemySpeed * ELITE_SPEED_MULT
      : this.currentEnemySpeed * (0.8 + Math.random() * 0.4);

    const enemyContainer = this._getEnemyContainer();
    let sprite: PIXI.Sprite | null = null;

    if (this.enemySpriteAliases.length > 0) {
      const alias = this.enemySpriteAliases[Math.floor(Math.random() * this.enemySpriteAliases.length)];
      try {
        const tex = PIXI.Assets.get(alias);
        if (tex) {
          sprite = new PIXI.Sprite(tex);
          sprite.anchor.set(0.5, 0.5);
          const maxSize = ENEMY_HIT_RADIUS * 4 * scale;
          const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
          sprite.scale.set(s);
          enemyContainer.addChild(sprite);
        }
      } catch { /* fallback below */ }
    }

    if (!sprite) {
      const gfx = new PIXI.Graphics();
      const radius = ENEMY_HIT_RADIUS * scale;
      gfx.circle(0, 0, radius);
      gfx.fill(isElite ? 0xff4444 : 0xe53935);
      if (isElite) {
        gfx.setStrokeStyle({ width: 2, color: 0xffff00 });
        gfx.stroke();
      }
      enemyContainer.addChild(gfx);
    }

    if (isElite) {
      const glow = new PIXI.Graphics();
      glow.circle(0, 0, ENEMY_HIT_RADIUS * scale + 4);
      glow.fill({ color: 0xff4444, alpha: 0.2 });
      enemyContainer.addChildAt(glow, 0);
    }

    enemyContainer.position.set(ex, ey);
    this.worldContainer.addChild(enemyContainer);

    const computeHP = (): number => {
      const baseHP = BASE_ENEMY_HP + this.level * ENEMY_HP_PER_LEVEL;
      const minuteBonus = 1 + Math.floor(this.timeElapsed / 60) * 0.15;
      const hp = baseHP * minuteBonus;
      return Math.ceil(isElite ? hp * ELITE_HP_MULT : hp);
    };

    const hp = computeHP();
    this.enemies.push({
      container: enemyContainer,
      x: ex, y: ey,
      speed, isElite, scale,
      hitRadius: ENEMY_HIT_RADIUS * scale,
      wobblePhase: Math.random() * Math.PI * 2,
      alive: true,
      hp, maxHp: hp,
    });

    this.totalSpawned++;
  }

  // ─── Boss Spawning ──────────────────────────────────────────

  private spawnBoss(isFinal: boolean): void {
    // Clear existing enemies
    for (const enemy of this.enemies) {
      if (enemy.container) this._releaseEnemyContainer(enemy.container);
    }
    this.enemies = [];

    const bossContainer = this._getEnemyContainer();
    let sprite: PIXI.Sprite | null = null;

    if (this.bossSpriteAliases.length > 0) {
      const alias = this.bossSpriteAliases[Math.floor(Math.random() * this.bossSpriteAliases.length)];
      try {
        const tex = PIXI.Assets.get(alias);
        if (tex) {
          sprite = new PIXI.Sprite(tex);
          sprite.anchor.set(0.5, 0.5);
          const maxSize = BOSS_HIT_RADIUS * 4;
          const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
          sprite.scale.set(s);
          bossContainer.addChild(sprite);
        }
      } catch { /* fallback below */ }
    }

    if (!sprite) {
      const gfx = new PIXI.Graphics();
      gfx.circle(0, 0, BOSS_HIT_RADIUS);
      gfx.fill(isFinal ? 0xaa0000 : 0xcc2222);
      gfx.setStrokeStyle({ width: 3, color: 0xffcc00 });
      gfx.stroke();
      bossContainer.addChild(gfx);
    }

    // Boss glow aura
    const glow = new PIXI.Graphics();
    glow.circle(0, 0, BOSS_HIT_RADIUS + 10);
    glow.fill({ color: isFinal ? 0xff0000 : 0xff6600, alpha: 0.25 });
    bossContainer.addChildAt(glow, 0);

    if (isFinal) {
      const crown = new PIXI.Text({ text: '👑', style: { fontSize: 20 } });
      crown.anchor.set(0.5, 1);
      crown.position.set(0, -BOSS_HIT_RADIUS - 5);
      bossContainer.addChild(crown);
    }

    const angle = Math.random() * Math.PI * 2;
    const spawnDist = 350;
    const bx = clamp(this.playerX + Math.cos(angle) * spawnDist, BOSS_HIT_RADIUS, this.mapWidth - BOSS_HIT_RADIUS);
    const by = clamp(this.playerY + Math.sin(angle) * spawnDist, BOSS_HIT_RADIUS, this.mapHeight - BOSS_HIT_RADIUS);

    bossContainer.position.set(bx, by);
    this.worldContainer.addChild(bossContainer);

    const minuteBonus = 1 + Math.floor(this.timeElapsed / 60) * 0.20;
    const bossHP = Math.ceil(
      (BASE_ENEMY_HP + this.level * ENEMY_HP_PER_LEVEL) *
      BOSS_HP_MULT * minuteBonus * (isFinal ? 2.0 : 1),
    );

    const boss: EnemyState = {
      container: bossContainer,
      x: bx, y: by,
      speed: this.currentEnemySpeed * BOSS_SPEED_MULT,
      isElite: false,
      isBoss: true,
      isFinalBoss: isFinal,
      scale: 2.5, // BOSS_SCALE
      hitRadius: BOSS_HIT_RADIUS,
      wobblePhase: Math.random() * Math.PI * 2,
      alive: true,
      hp: bossHP,
      maxHp: bossHP,
    };

    this.enemies.push(boss);
    this.activeBoss = boss;
    this.totalSpawned++;

    this.showBossHPBar(isFinal);
    this.flashScreen(isFinal ? 0xff0000 : 0xff6600);
  }

  // ─── Boss HP Bar ────────────────────────────────────────────

  private showBossHPBar(isFinal: boolean): void {
    this.hideBossHPBar();

    const barWidth = 300;
    const barHeight = 16;
    const x = (this.vpWidth - barWidth) / 2;
    const y = 70;

    this.bossHPBarBg = new PIXI.Graphics();
    this.bossHPBarBg.roundRect(x - 2, y - 2, barWidth + 4, barHeight + 4, 4);
    this.bossHPBarBg.fill({ color: 0x000000, alpha: 0.7 });
    this.uiContainer.addChild(this.bossHPBarBg);

    this.bossHPBarFill = new PIXI.Graphics();
    this.bossHPBarFill.roundRect(x, y, barWidth, barHeight, 3);
    this.bossHPBarFill.fill(isFinal ? 0xff2222 : 0xff6600);
    this.uiContainer.addChild(this.bossHPBarFill);

    this.bossHPBarText = new PIXI.Text({
      text: isFinal ? '💀 FINAL BOSS' : '⚔️ BOSS',
      style: {
        fontFamily: 'Arial', fontSize: 12, fontWeight: 'bold',
        fill: 0xffffff, stroke: { color: 0x000000, width: 2 },
      },
    });
    this.bossHPBarText.anchor.set(0.5, 0);
    this.bossHPBarText.position.set(this.vpWidth / 2, y - 18);
    this.uiContainer.addChild(this.bossHPBarText);

    this._bossBarX = x;
    this._bossBarY = y;
    this._bossBarW = barWidth;
    this._bossBarH = barHeight;
    this._bossBarFinal = isFinal;
  }

  private updateBossHPBar(): void {
    if (!this.activeBoss || !this.bossHPBarFill) return;
    const ratio = Math.max(0, this.activeBoss.hp / this.activeBoss.maxHp);
    const barWidth = this._bossBarW * ratio;
    this.bossHPBarFill.clear();
    if (barWidth > 0) {
      this.bossHPBarFill.roundRect(this._bossBarX, this._bossBarY, barWidth, this._bossBarH, 3);
      this.bossHPBarFill.fill(this._bossBarFinal ? 0xff2222 : 0xff6600);
    }
  }

  private hideBossHPBar(): void {
    if (this.bossHPBarBg) { try { this.uiContainer.removeChild(this.bossHPBarBg); } catch { /* noop */ } this.bossHPBarBg = null; }
    if (this.bossHPBarFill) { try { this.uiContainer.removeChild(this.bossHPBarFill); } catch { /* noop */ } this.bossHPBarFill = null; }
    if (this.bossHPBarText) { try { this.uiContainer.removeChild(this.bossHPBarText); } catch { /* noop */ } this.bossHPBarText = null; }
  }

  private flashScreen(color: number): void {
    const flash = new PIXI.Graphics();
    flash.rect(0, 0, this.vpWidth, this.vpHeight);
    flash.fill({ color, alpha: 0.35 });
    this.uiContainer.addChild(flash);
    setTimeout(() => {
      try { this.uiContainer.removeChild(flash); } catch { /* noop */ }
    }, 300);
  }

  private _buildStatsString(): string {
    const atkSpd = (1 / this.attackCooldownMult).toFixed(1);
    return `⚔ DMG ${this.attackDamage}  ⚡ SPD x${atkSpd}\n🎯 RNG ${Math.round(this.attackRange)}  🏃 MOV ${Math.round(this.playerSpeed)}\n🪙 DROP x${this.coinDropMult}  ❤ HP ${this.maxHP}`;
  }

  // ─── Object Pools ───────────────────────────────────────────

  private _initPools(): void {
    const gfxFactory = () => new PIXI.Graphics();
    const gfxReset = (gfx: PIXI.Graphics) => {
      gfx.clear();
      gfx.alpha = 1;
      gfx.visible = false;
    };

    this._projectilePool = new ObjectPool(gfxFactory, gfxReset, 30);
    this._orbPool = new ObjectPool(gfxFactory, gfxReset, 40);
    this._particlePool = new ObjectPool(gfxFactory, gfxReset, 50);
  }

  private _getProjectileGfx(color: number, radius: number): PIXI.Graphics {
    const gfx = this._projectilePool!.get();
    gfx.clear();
    gfx.circle(0, 0, radius);
    gfx.fill(color);
    gfx.circle(0, 0, radius + 2);
    gfx.fill({ color, alpha: 0.3 });
    gfx.visible = true;
    gfx.alpha = 1;
    return gfx;
  }

  private _releaseProjectileGfx(gfx: PIXI.Graphics): void {
    gfx.visible = false;
    if (gfx.parent) gfx.parent.removeChild(gfx);
    this._projectilePool!.release(gfx);
  }

  private _getOrbGfx(): PIXI.Graphics {
    const gfx = this._orbPool!.get();
    gfx.clear();
    gfx.circle(0, 0, XP_ORB_RADIUS + 1);
    gfx.fill(0xffd700);
    gfx.circle(0, 0, XP_ORB_RADIUS - 1);
    gfx.fill(0xffb300);
    gfx.circle(0, 0, 2);
    gfx.fill(0xffd700);
    gfx.visible = true;
    gfx.alpha = 1;
    return gfx;
  }

  private _releaseOrbGfx(gfx: PIXI.Graphics): void {
    gfx.visible = false;
    if (gfx.parent) gfx.parent.removeChild(gfx);
    this._orbPool!.release(gfx);
  }

  private _getParticleGfx(color?: number): PIXI.Graphics {
    const gfx = this._particlePool!.get();
    gfx.clear();
    gfx.circle(0, 0, 2 + Math.random() * 3);
    gfx.fill(color ?? 0xff4444);
    gfx.visible = true;
    gfx.alpha = 1;
    return gfx;
  }

  private _releaseParticleGfx(gfx: PIXI.Graphics): void {
    gfx.visible = false;
    if (gfx.parent) gfx.parent.removeChild(gfx);
    this._particlePool!.release(gfx);
  }

  private spawnXPOrb(x: number, y: number): void {
    const gfx = this._getOrbGfx();
    gfx.position.set(x, y);
    this.worldContainer.addChild(gfx);
    this.xpOrbs.push({ gfx, x, y, lifetime: 8.0 });
  }

  private spawnDeathParticles(x: number, y: number, color: number): void {
    for (let i = 0; i < 6; i++) {
      const angle = (Math.PI * 2 / 6) * i + Math.random() * 0.5;
      const speed = 40 + Math.random() * 60;
      const gfx = this._getParticleGfx(color);
      gfx.position.set(x, y);
      this.worldContainer.addChild(gfx);
      this.particles.push({
        gfx,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        lifetime: 0.5 + Math.random() * 0.3,
        age: 0,
      });
    }
  }

  // ─── Main Update Loop ───────────────────────────────────────

  update(ticker: PIXI.Ticker): void {
    if (!this.alive || this.won || gamePaused || this.upgradePaused) return;

    const dt = ticker.deltaMS / 1000;

    // Timers
    this.timeElapsed += dt;
    if (!this.timerStopped) this.timeRemaining -= dt;
    this.spawnTimer += dt;

    // Per-minute difficulty ramp
    const currentMinute = Math.floor(this.timeElapsed / 60);
    if (currentMinute > this.lastMinuteRamp) {
      const newMinutes = currentMinute - this.lastMinuteRamp;
      this.spawnRampBonus += newMinutes * this.spawnRampPerMinute;
      this.speedRampBonus += newMinutes * this.speedRampPerMinute;
      this.lastMinuteRamp = currentMinute;
    }

    // Speed ramp
    this.speedRampTimer += dt;
    const rampFactor = 1 + (this.speedRampTimer / this.timerDuration) * 0.8 + this.speedRampBonus;
    this.currentEnemySpeed = Math.min(this.enemySpeed * rampFactor, this.maxEnemySpeed);

    // Boss spawn at half-time
    const hasBosses = this.bossSpriteAliases.length > 0 && !this.isFinalLevel;
    if (hasBosses && !this.midBossSpawned && this.timeRemaining <= this.timerDuration / 2) {
      this.midBossSpawned = true;
      this.spawnBoss(false);
    }

    // Timer expiry
    if (this.timeRemaining <= 0) {
      this.timeRemaining = 0;

      if (this.isFinalLevel || !hasBosses) {
        this.won = true;
        this.onWin();
        return;
      }

      if (!this.finalBossSpawned) {
        this.finalBossSpawned = true;
        this.timerStopped = true;
        this.spawnBoss(true);
      }
    }

    // Boss HP bar
    if (this.activeBoss) {
      this.updateBossHPBar();
      if (this.activeBoss.container?.children[0]) {
        this.activeBoss.container.children[0].alpha = 0.15 + Math.sin(this.timeElapsed * 4) * 0.1;
      }
    }

    // Invulnerability
    if (this.invulnTimer > 0) {
      this.invulnTimer -= dt;
      if (this.player) this.player.alpha = (Math.floor(this.timeElapsed / 0.08) % 2 === 0) ? 0.4 : 1.0;
    } else if (this.player) {
      this.player.alpha = 1.0;
    }

    // Update UI text
    this.timerText.text = formatTime(this.timeRemaining);
    this.updateTimerBar();
    this.updateHPBar();
    if (this.hpText) this.hpText.text = `${Math.ceil(this.playerHP)}/${this.maxHP}`;
    this.killText.text = `☠ ${this.enemiesKilled}`;
    if (this.orbText) this.orbText.text = `🪙 ${this.xpOrbsCollected}`;
    if (this.statsText) this.statsText.text = this._buildStatsString();

    // Sub-systems
    this.updatePlayer(dt);
    this.updateCamera();

    // Spawn enemies
    if (this.spawnTimer >= this.spawnInterval) {
      this.spawnTimer = 0;
      const timeScale = Math.floor(this.timeElapsed / 5);
      const rampMult = 1 + this.spawnRampBonus;
      const baseSpawn = Math.ceil((3 + timeScale) * rampMult);
      const burstBonus = Math.floor(this.timeElapsed / 35) * 5;
      const toSpawn = Math.min(baseSpawn + burstBonus, 30);
      for (let i = 0; i < toSpawn; i++) this.spawnEnemy();
    }

    this.updateEnemies(dt);
    this.updateAutoAttack(dt);
    this.updateCompanions(dt);
    this.updateProjectiles(dt);
    this.updateXPOrbs(dt);
    this.updateParticles(dt);
    this.rebuildSpatialGrid();
    this.checkCollisions();
    this.updateMinimap();

    // Player glow pulse
    if (this.playerGlow) {
      this.playerGlow.alpha = 0.1 + Math.sin(this.timeElapsed * 3) * 0.08;
    }
  }

  // ─── Movement ───────────────────────────────────────────────

  private updatePlayer(dt: number): void {
    let dx = 0;
    let dy = 0;

    if (this.keys['w'] || this.keys['arrowup']) dy -= 1;
    if (this.keys['s'] || this.keys['arrowdown']) dy += 1;
    if (this.keys['a'] || this.keys['arrowleft']) dx -= 1;
    if (this.keys['d'] || this.keys['arrowright']) dx += 1;

    if (this.joystickActive && this.joystickMagnitude > 0.1) {
      dx = Math.cos(this.joystickAngle) * this.joystickMagnitude;
      dy = Math.sin(this.joystickAngle) * this.joystickMagnitude;
    }

    if (this.touchActive) {
      const tdx = this.touchTarget.x - this.playerX;
      const tdy = this.touchTarget.y - this.playerY;
      const td = Math.sqrt(tdx * tdx + tdy * tdy);
      if (td > 5) { dx = tdx / td; dy = tdy / td; }
    }

    const mag = Math.sqrt(dx * dx + dy * dy);
    if (mag > 0) { dx /= mag; dy /= mag; }

    this.playerX += dx * this.playerSpeed * dt;
    this.playerY += dy * this.playerSpeed * dt;
    this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
    this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
    this.player!.position.set(this.playerX, this.playerY);

    if (this.playerArrow && mag > 0) {
      this.playerArrow.rotation = Math.atan2(dy, dx);
    }
  }

  private updateCamera(): void {
    const targetCamX = this.playerX - this.vpWidth / 2;
    const targetCamY = this.playerY - this.vpHeight / 2;
    this.camX = lerp(this.camX, targetCamX, 0.1);
    this.camY = lerp(this.camY, targetCamY, 0.1);
    this.camX = clamp(this.camX, 0, this.mapWidth - this.vpWidth);
    this.camY = clamp(this.camY, 0, this.mapHeight - this.vpHeight);
    this.worldContainer.position.set(-this.camX, -this.camY);
  }

  // ─── Enemy Update ───────────────────────────────────────────

  private updateEnemies(dt: number): void {
    const cullMargin = 200;
    const camLeft = this.camX - cullMargin;
    const camRight = this.camX + this.vpWidth + cullMargin;
    const camTop = this.camY - cullMargin;
    const camBottom = this.camY + this.vpHeight + cullMargin;

    for (const enemy of this.enemies) {
      if (!enemy.alive) continue;

      const dx = this.playerX - enemy.x;
      const dy = this.playerY - enemy.y;
      const d = Math.sqrt(dx * dx + dy * dy);

      if (d > 1) {
        enemy.wobblePhase += dt * 3;
        const wobbleX = Math.sin(enemy.wobblePhase) * 15;
        const wobbleY = Math.cos(enemy.wobblePhase * 0.7) * 15;
        enemy.x += (dx / d + wobbleX / d) * enemy.speed * dt;
        enemy.y += (dy / d + wobbleY / d) * enemy.speed * dt;
        enemy.x = clamp(enemy.x, 0, this.mapWidth);
        enemy.y = clamp(enemy.y, 0, this.mapHeight);
        enemy.container.position.set(enemy.x, enemy.y);
        enemy.container.scale.x = dx > 0 ? Math.abs(enemy.container.scale.x) : -Math.abs(enemy.container.scale.x);
      }

      enemy.container.visible =
        enemy.x >= camLeft && enemy.x <= camRight &&
        enemy.y >= camTop && enemy.y <= camBottom;
    }
  }

  // ─── XP Orbs ────────────────────────────────────────────────

  private updateXPOrbs(dt: number): void {
    for (let i = this.xpOrbs.length - 1; i >= 0; i--) {
      const orb = this.xpOrbs[i];
      orb.lifetime -= dt;

      const dx = this.playerX - orb.x;
      const dy = this.playerY - orb.y;
      const d = Math.sqrt(dx * dx + dy * dy);

      if (d < this.magnetRadius) {
        const speed = XP_ORB_SPEED * (1 - d / this.magnetRadius);
        orb.x += (dx / d) * speed * dt;
        orb.y += (dy / d) * speed * dt;
        orb.gfx.position.set(orb.x, orb.y);

        if (d < PLAYER_RADIUS) {
          this._releaseOrbGfx(orb.gfx);
          this.xpOrbs.splice(i, 1);
          this.xpOrbsCollected++;
          if (this.xpOrbsCollected >= this.nextUpgradeAt) this.showUpgradePopup();
          continue;
        }
      }

      if (orb.lifetime <= 0) {
        this._releaseOrbGfx(orb.gfx);
        this.xpOrbs.splice(i, 1);
        continue;
      }

      if (orb.lifetime < 2) orb.gfx.alpha = orb.lifetime / 2;
    }
  }

  // ─── Particles ──────────────────────────────────────────────

  private updateParticles(dt: number): void {
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.age += dt;
      p.gfx.position.x += p.vx * dt;
      p.gfx.position.y += p.vy * dt;
      p.gfx.alpha = 1 - (p.age / p.lifetime);

      if (p.age >= p.lifetime) {
        this._releaseParticleGfx(p.gfx);
        this.particles.splice(i, 1);
      }
    }
  }

  // ─── Auto-Attack ────────────────────────────────────────────

  private updateAutoAttack(dt: number): void {
    this.attackCooldown -= dt;
    if (this.attackCooldown > 0) return;

    let nearest: EnemyState | null = null;
    let nearestDist = this.attackRange;

    for (const enemy of this.enemies) {
      if (!enemy.alive) continue;
      const d = dist({ x: this.playerX, y: this.playerY }, { x: enemy.x, y: enemy.y });
      if (d < nearestDist) { nearestDist = d; nearest = enemy; }
    }

    if (!nearest) return;

    this.attackCooldown = ATTACK_COOLDOWN * this.attackCooldownMult;
    const dx = nearest.x - this.playerX;
    const dy = nearest.y - this.playerY;
    const d = Math.sqrt(dx * dx + dy * dy);

    const gfx = this._getProjectileGfx(0x4fc3f7, PROJECTILE_RADIUS);
    gfx.position.set(this.playerX, this.playerY);
    this.worldContainer.addChild(gfx);

    this.projectiles.push({
      gfx,
      x: this.playerX, y: this.playerY,
      vx: (dx / d) * PROJECTILE_SPEED,
      vy: (dy / d) * PROJECTILE_SPEED,
      damage: this.attackDamage,
      lifetime: PROJECTILE_LIFETIME,
      age: 0,
    });

    if (this.playerArrow) this.playerArrow.rotation = Math.atan2(dy, dx);
  }

  // ─── Projectiles ────────────────────────────────────────────

  private updateProjectiles(dt: number): void {
    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const proj = this.projectiles[i];
      proj.age += dt;
      proj.x += proj.vx * dt;
      proj.y += proj.vy * dt;
      proj.gfx.position.set(proj.x, proj.y);

      if (
        proj.age >= proj.lifetime ||
        proj.x < -50 || proj.x > this.mapWidth + 50 ||
        proj.y < -50 || proj.y > this.mapHeight + 50
      ) {
        this._releaseProjectileGfx(proj.gfx);
        this.projectiles.splice(i, 1);
        continue;
      }

      // Check enemy hits
      for (let j = this.enemies.length - 1; j >= 0; j--) {
        const enemy = this.enemies[j];
        if (!enemy.alive) continue;
        if (proj.hitEnemies?.has(enemy)) continue;

        const d = dist({ x: proj.x, y: proj.y }, { x: enemy.x, y: enemy.y });
        if (d < PROJECTILE_RADIUS + enemy.hitRadius) {
          enemy.hp -= proj.damage;

          enemy.container.alpha = 0.5;
          setTimeout(() => { if (enemy.container) enemy.container.alpha = 1; }, 80);

          if (enemy.hp <= 0) {
            enemy.alive = false;
            this.spawnDeathParticles(enemy.x, enemy.y, enemy.isBoss ? 0xff6600 : (enemy.isElite ? 0xffff00 : 0xff4444));
            const coinCount = enemy.isBoss ? this.coinDropMult * 5 : this.coinDropMult;
            for (let c = 0; c < coinCount; c++) {
              const ox = c === 0 ? 0 : (Math.random() - 0.5) * 30;
              const oy = c === 0 ? 0 : (Math.random() - 0.5) * 30;
              this.spawnXPOrb(enemy.x + ox, enemy.y + oy);
            }
            this._releaseEnemyContainer(enemy.container);
            this.enemies.splice(j, 1);
            this.enemiesKilled++;
            this.playSFX('hit');

            if (enemy.isBoss) {
              this.hideBossHPBar();
              if (this.activeBoss === enemy) this.activeBoss = null;
              if (enemy.isFinalBoss) {
                this.won = true;
                this.onWin();
                return;
              }
            }
          }

          if (!proj.hitEnemies) proj.hitEnemies = new Set();
          proj.hitEnemies.add(enemy);
        }
      }
    }
  }

  // ─── Spatial Grid ───────────────────────────────────────────

  private rebuildSpatialGrid(): void {
    this._spatialGrid.clear();
    for (const e of this.enemies) {
      if (e.alive) this._spatialGrid.insert(e);
    }
  }

  private checkCollisions(): void {
    if (this.invulnTimer > 0) return;

    const nearby = this._spatialGrid.query(this.playerX, this.playerY);
    for (let i = 0; i < nearby.length; i++) {
      const enemy = nearby[i];
      if (!enemy.alive) continue;

      const d = dist({ x: this.playerX, y: this.playerY }, { x: enemy.x, y: enemy.y });
      if (d < PLAYER_RADIUS + enemy.hitRadius) {
        const dmg = enemy.isBoss ? BOSS_DAMAGE : (enemy.isElite ? ELITE_DAMAGE : ENEMY_DAMAGE);
        this.playerHP -= dmg;
        this.invulnTimer = INVULN_DURATION;
        this.playSFX('hit');

        const kbDist = 40;
        const angle = Math.atan2(this.playerY - enemy.y, this.playerX - enemy.x);
        this.playerX += Math.cos(angle) * kbDist;
        this.playerY += Math.sin(angle) * kbDist;
        this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
        this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
        this.player!.position.set(this.playerX, this.playerY);

        if (this.playerHP <= 0) {
          this.playerHP = 0;
          this.alive = false;
          this.onDeath();
          return;
        }
        return; // one hit per frame
      }
    }
  }

  // ─── UI Bar Updates ─────────────────────────────────────────

  private updateHPBar(): void {
    if (!this.hpBar) return;
    this.hpBar.clear();
    const progress = clamp(this.playerHP / this.maxHP, 0, 1);
    const barW = this.hpBarWidth * progress;

    let color: number;
    if (progress > 0.6) color = 0xe53935;
    else if (progress > 0.3) color = 0xc62828;
    else color = 0xb71c1c;

    this.hpBar.roundRect(this.hpBarX, this.hpBarY, barW, this.hpBarHeight, 5);
    this.hpBar.fill(color);
  }

  private updateTimerBar(): void {
    if (!this.timerBar) return;
    this.timerBar.clear();
    const progress = clamp(this.timeRemaining / this.timerDuration, 0, 1);
    const barW = this.timerBarWidth * progress;

    let color: number;
    if (progress > 0.5) color = 0x43a047;
    else if (progress > 0.25) color = 0xffb300;
    else color = 0xe53935;

    this.timerBar.roundRect(this.timerBarX, this.timerBarY, barW, this.timerBarHeight, 6);
    this.timerBar.fill(color);
  }

  private updateMinimap(): void {
    if (!this.minimapContainer || !this._mmPlayerDot || !this._mmEnemyDots || !this._mmVpRect) return;

    const scaleX = MINIMAP_SIZE / this.mapWidth;
    const scaleY = MINIMAP_SIZE / this.mapHeight;

    this._mmPlayerDot.clear();
    this._mmPlayerDot.circle(this.playerX * scaleX, this.playerY * scaleY, 3);
    this._mmPlayerDot.fill(0x4fc3f7);

    this._mmEnemyDots.clear();
    for (const enemy of this.enemies) {
      if (!enemy.alive) continue;
      this._mmEnemyDots.circle(enemy.x * scaleX, enemy.y * scaleY, enemy.isElite ? 2 : 1);
    }
    this._mmEnemyDots.fill(0xe53935);

    this._mmVpRect.clear();
    this._mmVpRect.rect(this.camX * scaleX, this.camY * scaleY, this.vpWidth * scaleX, this.vpHeight * scaleY);
    this._mmVpRect.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.5 });
    this._mmVpRect.stroke();
  }

  // ─── Companion PIG System ───────────────────────────────────

  spawnCompanion(): void {
    const companionIdx = this.companions.length;
    const container = new PIXI.Container();

    // Pig body
    const body = new PIXI.Graphics();
    body.circle(0, 0, 12);
    body.fill(0xffb6c1);
    body.setStrokeStyle({ width: 1.5, color: 0xff69b4 });
    body.stroke();
    container.addChild(body);

    // Pig snout
    const snout = new PIXI.Graphics();
    snout.ellipse(10, 0, 5, 4);
    snout.fill(0xff9999);
    snout.circle(12, -1.5, 1);
    snout.fill(0xcc6666);
    snout.circle(12, 1.5, 1);
    snout.fill(0xcc6666);
    container.addChild(snout);

    // Pig ears
    const ear = new PIXI.Graphics();
    ear.moveTo(-5, -10);
    ear.lineTo(0, -16);
    ear.lineTo(5, -10);
    ear.closePath();
    ear.fill(0xff8da1);
    container.addChild(ear);

    // Pig eyes
    const eyes = new PIXI.Graphics();
    eyes.circle(3, -4, 2);
    eyes.fill(0x222222);
    container.addChild(eyes);

    container.position.set(this.playerX, this.playerY);
    this.worldContainer.addChild(container);

    this.companions.push({
      container,
      x: this.playerX,
      y: this.playerY,
      orbitAngle: companionIdx * (Math.PI * 2 / Math.max(this.companions.length + 1, 1)),
      attackCooldown: 0,
      attackRange: this.attackRange,
      orbitRadius: 50 + companionIdx * 20,
    });
  }

  private updateCompanions(dt: number): void {
    if (this.companions.length === 0) return;

    for (const comp of this.companions) {
      comp.orbitAngle += dt * 1.5;
      const targetX = this.playerX + Math.cos(comp.orbitAngle) * comp.orbitRadius;
      const targetY = this.playerY + Math.sin(comp.orbitAngle) * comp.orbitRadius;

      comp.x = lerp(comp.x, targetX, dt * 5);
      comp.y = lerp(comp.y, targetY, dt * 5);
      comp.container.position.set(comp.x, comp.y);

      const dxComp = targetX - comp.x;
      if (Math.abs(dxComp) > 0.5) {
        comp.container.scale.x = dxComp > 0 ? 1 : -1;
      }

      // Auto-attack
      comp.attackCooldown -= dt;
      if (comp.attackCooldown <= 0) {
        let nearest: EnemyState | null = null;
        let nearestDist = comp.attackRange;

        for (const enemy of this.enemies) {
          if (!enemy.alive) continue;
          const d = dist({ x: comp.x, y: comp.y }, { x: enemy.x, y: enemy.y });
          if (d < nearestDist) { nearestDist = d; nearest = enemy; }
        }

        if (nearest) {
          comp.attackCooldown = ATTACK_COOLDOWN * this.attackCooldownMult;
          const pdx = nearest.x - comp.x;
          const pdy = nearest.y - comp.y;
          const pd = Math.sqrt(pdx * pdx + pdy * pdy);

          const gfx = this._getProjectileGfx(0xff69b4, 3);
          gfx.position.set(comp.x, comp.y);
          this.worldContainer.addChild(gfx);

          this.projectiles.push({
            gfx,
            x: comp.x, y: comp.y,
            vx: (pdx / pd) * PROJECTILE_SPEED,
            vy: (pdy / pd) * PROJECTILE_SPEED,
            damage: this.attackDamage,
            lifetime: PROJECTILE_LIFETIME,
            age: 0,
          });
        }
      }
    }
  }

  // ─── Upgrade Popup ──────────────────────────────────────────

  private showUpgradePopup(): void {
    if (this.upgradePaused) return;
    if (this.upgradesPicked >= this.maxPowerUps) return;

    this.upgradePaused = true;

    this.upgradeIndex++;
    if (this.upgradeIndex < UPGRADE_THRESHOLDS.length) {
      this.nextUpgradeAt = UPGRADE_THRESHOLDS[this.upgradeIndex];
    } else {
      this.nextUpgradeAt += UPGRADE_THRESHOLD_STEP;
    }

    const available = UPGRADE_DEFS.filter((u) => {
      if (u.unique && this.pickedUpgradeIds.has(u.id)) return false;
      if (u.maxPicks && (this.upgradePickCounts[u.id] ?? 0) >= u.maxPicks) return false;
      return true;
    });
    const shuffled = [...available].sort(() => Math.random() - 0.5);
    const choices = shuffled.slice(0, 3);

    if (choices.length === 0) { this.upgradePaused = false; return; }

    const overlay = new PIXI.Container();
    const bg = new PIXI.Graphics();
    bg.rect(0, 0, this.vpWidth, this.vpHeight);
    bg.fill({ color: 0x000000, alpha: 0.75 });
    bg.eventMode = 'static';
    overlay.addChild(bg);

    const title = new PIXI.Text({
      text: 'ESCOLHE UM UPGRADE',
      style: {
        fontFamily: 'Arial', fontSize: 22, fontWeight: 'bold',
        fill: 0xffd700, align: 'center',
        dropShadow: { color: 0x000000, blur: 4, distance: 2 },
      },
    });
    title.anchor.set(0.5, 0.5);
    title.position.set(this.vpWidth / 2, this.vpHeight * 0.18);
    overlay.addChild(title);

    const cardW = Math.min(130, (this.vpWidth - 60) / 3);
    const cardH = 160;
    const gap = 12;
    const totalW = cardW * 3 + gap * 2;
    const startX = (this.vpWidth - totalW) / 2;
    const cardY = this.vpHeight / 2 - cardH / 2;

    choices.forEach((upg, idx) => {
      const cx = startX + idx * (cardW + gap);
      const card = new PIXI.Container();
      card.eventMode = 'static';
      card.cursor = 'pointer';

      const cardBg = new PIXI.Graphics();
      cardBg.roundRect(0, 0, cardW, cardH, 10);
      cardBg.fill({ color: 0x1a1a3e, alpha: 0.95 });
      cardBg.setStrokeStyle({ width: 2, color: 0xffd700, alpha: 0.8 });
      cardBg.stroke();
      card.addChild(cardBg);

      const hoverBg = new PIXI.Graphics();
      hoverBg.roundRect(0, 0, cardW, cardH, 10);
      hoverBg.fill({ color: 0x2a2a5e, alpha: 0.95 });
      hoverBg.setStrokeStyle({ width: 3, color: 0xffee55 });
      hoverBg.stroke();
      hoverBg.visible = false;
      card.addChild(hoverBg);

      const icon = new PIXI.Text({ text: upg.icon, style: { fontSize: 36 } });
      icon.anchor.set(0.5, 0.5);
      icon.position.set(cardW / 2, 35);
      card.addChild(icon);

      const tText = new PIXI.Text({
        text: upg.title,
        style: {
          fontFamily: 'Arial', fontSize: 14, fontWeight: 'bold',
          fill: 0xffffff, align: 'center',
          wordWrap: true, wordWrapWidth: cardW - 16,
        },
      });
      tText.anchor.set(0.5, 0);
      tText.position.set(cardW / 2, 62);
      card.addChild(tText);

      const dText = new PIXI.Text({
        text: upg.desc,
        style: {
          fontFamily: 'Arial', fontSize: 11,
          fill: 0xbbbbbb, align: 'center',
          wordWrap: true, wordWrapWidth: cardW - 16,
        },
      });
      dText.anchor.set(0.5, 0);
      dText.position.set(cardW / 2, 90);
      card.addChild(dText);

      card.on('pointerdown', () => {
        this.applyUpgrade(upg);
        this.uiContainer.removeChild(overlay);
        this.upgradeOverlay = null;
        this.upgradePaused = false;
      });

      card.on('pointerover', () => { hoverBg.visible = true; cardBg.visible = false; });
      card.on('pointerout', () => { hoverBg.visible = false; cardBg.visible = true; });

      card.position.set(cx, cardY);
      overlay.addChild(card);
    });

    this.upgradeOverlay = overlay;
    this.uiContainer.addChild(overlay);
  }

  private applyUpgrade(upg: UpgradeDef): void {
    upg.apply(this);
    this.upgradesPicked++;
    this.upgradePickCounts[upg.id] = (this.upgradePickCounts[upg.id] ?? 0) + 1;
    if (upg.unique) this.pickedUpgradeIds.add(upg.id);

    const flash = new PIXI.Graphics();
    flash.rect(0, 0, this.vpWidth, this.vpHeight);
    flash.fill({ color: 0xffd700, alpha: 0.2 });
    this.uiContainer.addChild(flash);
    setTimeout(() => {
      try { this.uiContainer.removeChild(flash); } catch { /* noop */ }
    }, 200);
  }

  // ─── End-Game ───────────────────────────────────────────────

  private onWin(): void {
    gameActive = false;
    const title = this.finalBossSpawned ? 'BOSS DEFEATED!' : 'SURVIVED!';
    this.showMessage(title, 0x43a047, `Level ${this.level} Complete!`);
    this.playSFX('win');

    setTimeout(() => {
      if (dotNetRef) {
        try {
          dotNetRef.invokeMethodAsync('OnLevelComplete', this.enemiesKilled, this.timeElapsed, this.xpOrbsCollected);
        } catch (e) {
          console.error('Failed to invoke OnLevelComplete:', e);
        }
      }
    }, 2000);
  }

  private onDeath(): void {
    gameActive = false;
    this.spawnDeathParticles(this.playerX, this.playerY, 0x4fc3f7);
    if (this.player) this.player.alpha = 0.3;
    this.showMessage('SURVIVAL ENDED', 0xe53935, `Survived ${formatTime(this.timeElapsed)}`);
    this.playSFX('death');

    setTimeout(() => {
      if (dotNetRef) {
        try {
          dotNetRef.invokeMethodAsync('OnPlayerDeath', this.enemiesKilled, this.timeElapsed, this.xpOrbsCollected);
        } catch (e) {
          console.error('Failed to invoke OnPlayerDeath:', e);
        }
      }
    }, 2000);
  }

  private showMessage(titleStr: string, color: number, subtitle?: string): void {
    const overlay = new PIXI.Graphics();
    overlay.rect(0, 0, this.vpWidth, this.vpHeight);
    overlay.fill({ color: 0x000000, alpha: 0.6 });
    this.uiContainer.addChild(overlay);

    const text = new PIXI.Text({
      text: titleStr,
      style: {
        fontFamily: 'Arial', fontSize: 48, fontWeight: 'bold',
        fill: color, stroke: { color: 0x000000, width: 4 },
        align: 'center',
      },
    });
    text.anchor.set(0.5, 0.5);
    text.position.set(this.vpWidth / 2, this.vpHeight / 2 - 20);
    this.uiContainer.addChild(text);

    if (subtitle) {
      const sub = new PIXI.Text({
        text: subtitle,
        style: { fontFamily: 'Arial', fontSize: 18, fill: 0xffffff, align: 'center' },
      });
      sub.anchor.set(0.5, 0.5);
      sub.position.set(this.vpWidth / 2, this.vpHeight / 2 + 30);
      this.uiContainer.addChild(sub);
    }
  }

  // ─── Audio ──────────────────────────────────────────────────

  private playMusic(): void {
    if (!audioEnabled) return;
    try {
      if (bgMusic) { bgMusic.currentTime = 0; bgMusic.play().catch(() => {}); return; }
      bgMusic = new Audio('/sound/survival_battle.mp3' + audioCacheBuster);
      bgMusic.loop = true;
      bgMusic.volume = 0.3;
      bgMusic.play().catch(() => {});
      bgMusicLoaded = true;
    } catch { /* noop */ }
  }

  private playSFX(type: string): void {
    if (!audioEnabled) return;
    try {
      const audio = getPooledAudio(type);
      if (audio) audio.play().catch(() => {});
    } catch { /* noop */ }
  }

  // ─── Cleanup / Destroy ──────────────────────────────────────

  cleanup(): void {
    window.removeEventListener('keydown', this._onKeyDown!);
    window.removeEventListener('keyup', this._onKeyUp!);
    if (app?.canvas) {
      const c = app.canvas as HTMLCanvasElement;
      if (this._onPointerDown) c.removeEventListener('pointerdown', this._onPointerDown);
      if (this._onPointerMove) c.removeEventListener('pointermove', this._onPointerMove);
      if (this._onPointerUp) c.removeEventListener('pointerup', this._onPointerUp);
      if (this._onPointerCancel) c.removeEventListener('pointercancel', this._onPointerCancel);
    }

    if (app?.ticker) {
      try { app.ticker.remove(this.update, this); } catch { /* noop */ }
    }

    // Release pooled objects still active
    for (const proj of this.projectiles) {
      if (proj.gfx) { proj.gfx.visible = false; if (proj.gfx.parent) proj.gfx.parent.removeChild(proj.gfx); }
    }
    for (const orb of this.xpOrbs) {
      if (orb.gfx) { orb.gfx.visible = false; if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx); }
    }
    for (const p of this.particles) {
      if (p.gfx) { p.gfx.visible = false; if (p.gfx.parent) p.gfx.parent.removeChild(p.gfx); }
    }

    if (this.worldContainer) { try { this.worldContainer.removeChildren(); } catch { /* noop */ } }
    if (this.uiContainer) { try { this.uiContainer.removeChildren(); } catch { /* noop */ } }

    this.enemies = [];
    for (const c of this._enemyContainerPool) {
      try { c.destroy({ children: true }); } catch { /* noop */ }
    }
    this._enemyContainerPool = [];
    this.xpOrbs = [];
    this.particles = [];
    this.projectiles = [];
    this.keys = {};

    this._projectilePool = null;
    this._orbPool = null;
    this._particlePool = null;
  }

  destroy(): void {
    this.cleanup();

    if (bgMusic) {
      try { bgMusic.pause(); bgMusic.currentTime = 0; } catch { /* noop */ }
      bgMusic = null;
      bgMusicLoaded = false;
    }

    if (app) {
      try {
        app.stage.removeChildren();
        app.destroy(true, { children: true, texture: false });
      } catch { /* noop */ }
      app = null;
    }

    gameActive = false;
  }
}

// ─── Module-level Game State (shared between API and scene) ───

let scene: SurviveScene | null = null;
let gameActive = false;
let gamePaused = false;
let dotNetRef: DotNet.DotNetObject | null = null;

/**
 * Create the public API object for window.surviveModeGame.
 */
export function createSurviveApi() {
  return {
    async start(containerId: string, levelData: SurviveLevelData, netRef: DotNet.DotNetObject): Promise<void> {
      dotNetRef = netRef;
      if (scene) scene.destroy();
      scene = new SurviveScene(containerId, levelData);
      await scene.init();
    },

    async nextLevel(levelData: SurviveLevelData): Promise<void> {
      if (scene) scene.cleanup();
      scene = new SurviveScene(scene?.containerId ?? 'surviveGameContainer', levelData);
      await scene.init();
    },

    pause(): void { gamePaused = true; },
    resume(): void { gamePaused = false; },

    setAudioEnabled(enabled: boolean): void {
      audioEnabled = enabled;
      if (bgMusic) {
        if (enabled) bgMusic.play().catch(() => {});
        else bgMusic.pause();
      }
    },

    destroy(): void {
      if (scene) { scene.destroy(); scene = null; }
      dotNetRef = null;
    },

    isActive(): boolean { return gameActive; },
  };
}
