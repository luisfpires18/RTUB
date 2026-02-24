/**
 * Stage / Boss Battle Scene — Player vs 1-9 enemies across stages.
 *
 * TypeScript port of `wwwroot/js/pixiStageBattle.js` (StageBattleScene class).
 * Uses shared modules for audio, VFX, tweens, text pooling, and utilities.
 */
import type { Application, Container, Graphics, Sprite, Text, TextStyle } from 'pixi.js';
import type { BattleEvent } from '../types/battle-events';
import type {
  StageBattleData,
  SpellDefinition,
  EnemyDefinition,
  ConsumableQuantities,
  ConsumableImages,
  ActiveBuffs,
} from '../types/battle-data';
import type { CombatActionResult, ConsumableResult, CooldownMap } from '../types/combat-result';
import type { VfxOwner } from '../shared/vfx';
import type { MusicState } from '../shared/audio';
import type { TextPool } from '../shared/text-pool';
import {
  SESSION_CACHE_BUST,
  loadedAssetAliases,
  getEventField,
  formatNum,
  resolveEvents,
  pick,
} from '../shared/utils';
import {
  getSharedAudioContext,
  playSound as sharedPlaySound,
  playSpellSound as sharedPlaySpellSound,
  loadBackgroundMusic,
  stopMusic,
  setMusicVolume,
} from '../shared/audio';
import { animateTo, fadeOut } from '../shared/tween';
import { getPooledText, releaseText, destroyTextPool } from '../shared/text-pool';
import {
  showFloatingText,
  showDamageText,
  showEffectLabel,
  screenShake,
  playSpellVfx,
  playBuffVfx,
} from '../shared/vfx';

// ─── Constants ────────────────────────────────────────────────
const DEFAULT_WIDTH = 800;
const DEFAULT_HEIGHT = 500;
const DEFAULT_EVENT_INTERVAL = 400;

const DEFAULT_SPRITES = {
  player: '/sprites/games/my-tuno/default_tuno.png',
  background: '/sprites/games/my-tuno/backgrounds/forest.png',
  enemies: {
    normal: '/sprites/games/my-tuno/enemies/forest/monkey.png',
    boss: '/sprites/games/my-tuno/enemies/forest/boss_1_bear.png',
  },
} as const;

const DEFAULT_CONSUMABLE_IMAGES: Record<string, string> = {
  fino: '/images/consumables/fino.svg',
  caneca: '/images/consumables/caneca.svg',
  cigarro: '/images/consumables/cigarro.svg',
  canhao: '/images/consumables/canhao.svg',
};

// ─── Module-level State (persists across scene instances) ─────
let globalAudioEnabled = false;
let globalSfxVolume = 0.5;
let musicState: MusicState | null = null;
let currentMusicType: 'stage' | 'boss' | null = null;

// ─── Helper Types ─────────────────────────────────────────────

interface HpBarData {
  bar: Graphics;
  barBg: Graphics;
  border?: Graphics;
  text: Text;
  maxWidth: number;
  barHeight: number;
  x?: number;
  y?: number;
}

interface SpeedBarData {
  bar: Graphics;
  barBg: Graphics;
  border?: Graphics;
  text?: Text;
  maxWidth: number;
  barHeight?: number;
}

interface TimerBarData {
  bar: Graphics;
  barBg: Graphics;
  label: Text;
  text: Text;
  maxWidth: number;
  barHeight: number;
}

interface EnemyIdleOffset {
  baseX: number;
  baseY: number;
  phase: number;
  isAerial: boolean;
  bobAmplitude: number;
  swayAmplitude: number;
}

interface EnemyPosition {
  x: number;
  y: number;
  isAerial: boolean;
}

interface SpellButton {
  container: Container;
  bg: Graphics;
  iconText: Text;
  nameText: Text;
  cdOverlay: Graphics;
  cdText: Text;
  attackId: string;
  cooldownSeconds: number;
  spell: SpellDefinition;
}

interface ConsumableButton {
  type: string;
  container: Container;
  bg: Graphics;
  iconSprite: Sprite | null;
  qtyText: Text;
  cdOverlay: Graphics;
  cdText: Text;
}

// ─── Scene Class ──────────────────────────────────────────────

export class StageBattleScene implements VfxOwner {
  // PIXI application
  app: Application | null = null;
  stage: Container | null = null;

  // Container & config
  private container: HTMLElement;
  private eventInterval: number;

  // Events
  private eventsList: BattleEvent[];
  private dotNetRef: DotNet.DotNetObject | null;

  // Stage info
  private stageNumber: number;
  private enemyType: string;
  private enemyCount: number;
  private playerName: string;
  private enemyName: string;
  private backgroundPath: string;
  private playerSpritePath: string;
  private enemySpritePaths: string[];
  private enemyPlacements: number[];

  // Layout
  private isMobile = false;
  private playerX = 0;
  private playerDisplayHeight = 0;

  // HP tracking
  private playerMaxHp = 100;
  private playerCurrentHp = 100;
  private enemyMaxHp = 100;
  private enemyCurrentHp = 100;
  private enemyHPs: { current: number; max: number }[] = [];
  private isInitialSetup = true;

  // Sprites
  playerSprite: Sprite | null = null;
  private enemySprites: Sprite[] = [];
  private backgroundSprite: Sprite | null = null;
  private _overlay: Graphics | null = null;
  private _ground: Graphics | null = null;

  // HP / Speed bars
  private playerHpBar: HpBarData | null = null;
  private enemyHpBars: HpBarData[] = [];
  private bossHpBar: HpBarData | null = null;
  private playerSpeedBar: SpeedBarData | null = null;
  private enemySpeedBars: SpeedBarData[] = [];
  private bossSpeedBar: SpeedBarData | null = null;

  // Speed bar timers
  private playerActionTime = 3.5;
  private enemyActionTimes: number[] = [];
  private playerSpeedBarTimer = 3500;
  private enemySpeedBarTimers: number[] = [];
  private battleStartTime = 0;
  private currentSimTime = 0;

  // Battle speed (anti-exploit) – only 1x or 5x allowed
  private _battleSpeed = 1.0;
  get battleSpeed(): number { return this._battleSpeed; }
  set battleSpeed(v: number) {
    this._battleSpeed = v === 5 ? 5 : 1;
  }

  // Playback
  private playbackSpeed = 1;
  private currentEventIndex = 0;
  private isPlaying = false;
  private battleFinished = false;
  private eventTimer: ReturnType<typeof setInterval> | null = null;

  // Audio
  private audioContext: AudioContext | null = null;
  private audioEnabled: boolean;
  private sfxVolume: number;

  // Idle animation
  private idleAnimationTime = 0;
  private enemyIdleOffsets: EnemyIdleOffset[] = [];
  private playerIdleOffset: EnemyIdleOffset | null = null;

  // Attack animation flags
  private _playerAttacking = false;
  private _enemyAttacking: Record<number, boolean> = {};

  // Shot / penalty buff visual
  private hasShotBuff: boolean;
  private hasPenaltyBuff: boolean;
  private playerAura: Sprite | null = null;

  // Canhao / Penalty timer bars (bottom-left corner)
  private canhaoTimerBar: TimerBarData | null = null;
  private penaltyTimerBar: TimerBarData | null = null;
  private canhaoBuffExpiresAt: number | null = null;
  private penaltyBuffExpiresAt: number | null = null;
  private canhaoBuffDurationMs = 2 * 60 * 1000;
  private penaltyBuffDurationMs = 2 * 60 * 1000;

  // Asset aliases
  private bgAlias = '';
  private _playerAlias = '';
  private enemySpriteAliases: string[] = [];

  // Interactive mode
  private interactiveMode: boolean;
  private spells: SpellDefinition[];
  private interactivePlayerHP: number | null;
  private interactivePlayerMaxHP: number | null;
  private interactivePlayerActionTime: number | null;
  private interactiveEnemies: EnemyDefinition[];
  private _playerAttackPending = false;
  private _enemyAttackPending: boolean[] = [];
  private _spellPending = false;
  private _cooldownTickAccum = 0;
  private _consumableTickAccum = 0;

  // Spell bar UI
  private spellButtons: SpellButton[] = [];
  private spellCooldowns: Record<string, number> = {};
  private spellBarContainer: Container | null = null;

  // Consumable bar UI
  private consumableBarContainer: Container | null = null;
  private consumableButtons: ConsumableButton[] = [];
  private _consumablePending = false;
  private consumableQuantities: Record<string, number>;
  private activeBuffs: Record<string, boolean>;
  private consumableImages: Record<string, string>;
  private consumableCooldowns: Record<string, number>;

  // Cleanup trackers (VfxOwner requirement)
  _timeoutIds: number[] = [];
  _rafIds: number[] = [];
  _textPool: TextPool = { pool: [] };

  // Destroyed flag — prevents async callbacks from running after destroy
  private _destroyed = false;

  // Event listeners
  private _onContextLost: ((e: Event) => void) | null = null;
  private _onContextRestored: ((e: Event) => void) | null = null;
  private _onResize: (() => void) | null = null;

  /* ────────────────────────── Constructor ────────────────────────── */

  constructor(container: HTMLElement, data: Record<string, unknown>) {
    this.container = container;
    this.eventsList = (data.events as BattleEvent[]) ?? [];
    this.dotNetRef = (data.dotNetRef as DotNet.DotNetObject | null) ?? null;
    this.eventInterval = (data.eventInterval as number | undefined) ?? DEFAULT_EVENT_INTERVAL;
    this.currentEventIndex = 0;

    // Stage info
    this.stageNumber = pick<number>(data, 'StageNumber', 'stageNumber', 1);
    this.enemyType = pick<string>(data, 'EnemyType', 'enemyType', 'normal');
    this.enemyCount = pick<number>(data, 'EnemyCount', 'enemyCount', 1);
    this.playerName = pick<string>(data, 'PlayerName', 'playerName', 'Player');
    this.enemyName = pick<string>(data, 'EnemyName', 'enemyName', 'Enemy');
    this.backgroundPath = pick<string>(data, 'BackgroundPath', 'backgroundPath', DEFAULT_SPRITES.background);
    this.playerSpritePath = pick<string>(data, 'PlayerSpritePath', 'playerSpritePath', DEFAULT_SPRITES.player);

    // Enemy sprites array
    const enemySpriteArr = (data.EnemySprites ?? data.enemySprites) as string[] | undefined;
    if (enemySpriteArr && Array.isArray(enemySpriteArr)) {
      this.enemySpritePaths = enemySpriteArr;
    } else {
      const singlePath =
        pick<string>(data, 'EnemySpritePath', 'enemySpritePath', '') ||
        (DEFAULT_SPRITES.enemies as Record<string, string>)[this.enemyType] ||
        DEFAULT_SPRITES.enemies.normal;
      this.enemySpritePaths = Array(this.enemyCount).fill(singlePath);
    }

    // Enemy placements (0=Terrestrial, 1=Aerial)
    const placementsArr = (data.EnemyPlacements ?? data.enemyPlacements) as number[] | undefined;
    this.enemyPlacements = Array.isArray(placementsArr)
      ? placementsArr
      : Array(this.enemyCount).fill(0);

    this.enemyHPs = Array.from({ length: this.enemyCount }, () => ({ current: 100, max: 100 }));

    // Speed
    this.enemyActionTimes = Array(this.enemyCount).fill(3.5);
    this.enemySpeedBarTimers = Array(this.enemyCount).fill(3500);

    // Animation flags
    this._enemyAttacking = {};

    // Global audio
    this.audioEnabled = globalAudioEnabled;
    this.sfxVolume = globalSfxVolume;

    // Shot buff
    this.hasShotBuff = pick<boolean>(data, 'HasShotBuff', 'hasShotBuff', false);
    this.hasPenaltyBuff = pick<boolean>(data, 'HasPenaltyBuff', 'hasPenaltyBuff', false);

    // Interactive mode
    this.interactiveMode = pick<boolean>(data, 'InteractiveMode', 'interactiveMode', false);
    this.spells = (data.Spells ?? data.spells ?? []) as SpellDefinition[];
    this.interactivePlayerHP = (data.PlayerHP ?? data.playerHP ?? null) as number | null;
    this.interactivePlayerMaxHP = (data.PlayerMaxHP ?? data.playerMaxHP ?? null) as number | null;
    this.interactivePlayerActionTime = (data.PlayerActionTime ?? data.playerActionTime ?? null) as number | null;
    this.interactiveEnemies = (data.Enemies ?? data.enemies ?? []) as EnemyDefinition[];
    this._enemyAttackPending = Array(this.enemyCount).fill(false);

    // Consumables
    const cData = (data.consumables ?? data.Consumables ?? {}) as Record<string, unknown>;
    this.consumableQuantities = {
      fino: pick<number>(cData, 'Fino', 'fino', 0),
      caneca: pick<number>(cData, 'Caneca', 'caneca', 0),
      cigarro: pick<number>(cData, 'Cigarro', 'cigarro', 0),
      canhao: pick<number>(cData, 'Canhao', 'canhao', 0),
      shot: pick<number>(cData, 'Shot', 'shot', 0),
      penalty: pick<number>(cData, 'Penalty', 'penalty', 0),
    };

    const abData = (data.activeBuffs ?? data.ActiveBuffs ?? {}) as Record<string, unknown>;
    this.activeBuffs = {
      cigarro: !!pick<boolean>(abData, 'Cigarro', 'cigarro', false),
      canhao: !!pick<boolean>(abData, 'Canhao', 'canhao', false),
      shot: !!pick<boolean>(abData, 'Shot', 'shot', false),
      penalty: !!pick<boolean>(abData, 'Penalty', 'penalty', false),
    };

    // Canhao / Penalty buff expiry (UTC timestamp from server)
    const canhaoUtc = (data.canhaoBuffExpiresAtUtc ?? data.CanhaoBuffExpiresAtUtc ?? null) as string | null;
    this.canhaoBuffExpiresAt = canhaoUtc ? new Date(canhaoUtc).getTime() : null;

    const penaltyUtc = (data.penaltyBuffExpiresAtUtc ?? data.PenaltyBuffExpiresAtUtc ?? null) as string | null;
    this.penaltyBuffExpiresAt = penaltyUtc ? new Date(penaltyUtc).getTime() : null;

    const ciData = (data.consumableImages ?? data.ConsumableImages ?? {}) as Record<string, unknown>;
    this.consumableImages = {
      fino: pick<string>(ciData, 'Fino', 'fino', DEFAULT_CONSUMABLE_IMAGES.fino),
      caneca: pick<string>(ciData, 'Caneca', 'caneca', DEFAULT_CONSUMABLE_IMAGES.caneca),
      cigarro: pick<string>(ciData, 'Cigarro', 'cigarro', DEFAULT_CONSUMABLE_IMAGES.cigarro),
      canhao: pick<string>(ciData, 'Canhao', 'canhao', DEFAULT_CONSUMABLE_IMAGES.canhao),
    };

    const ccData = (data.consumableCooldowns ?? data.ConsumableCooldowns ?? {}) as Record<string, unknown>;
    this.consumableCooldowns = {
      fino: pick<number>(ccData, 'Fino', 'fino', 0),
      caneca: pick<number>(ccData, 'Caneca', 'caneca', 0),
      cigarro: pick<number>(ccData, 'Cigarro', 'cigarro', 0),
      canhao: pick<number>(ccData, 'Canhao', 'canhao', 0),
      shot: pick<number>(ccData, 'Shot', 'shot', 0),
      penalty: pick<number>(ccData, 'Penalty', 'penalty', 0),
    };

    this.setupAudio();
    this.initPixi();
  }

  /* ────────────────────────── Audio Setup ────────────────────────── */

  private setupAudio(): void {
    this.audioContext = getSharedAudioContext();
    if (this.audioContext) {
      if (this.audioContext.state === 'suspended') {
        this.audioContext.resume().catch(() => {});
      }
      // Start background music if not already playing
      if (!musicState?.source) {
        this.loadBgMusic();
      }
    } else {
      this.audioEnabled = false;
    }
  }

  private async loadBgMusic(): Promise<void> {
    if (!this.audioContext) return;
    const isBoss = this.enemyType?.toLowerCase() === 'boss';
    const musicFile = isBoss ? '/sound/boss_battle.mp3' : '/sound/stage_battle.mp3';
    currentMusicType = isBoss ? 'boss' : 'stage';
    musicState = await loadBackgroundMusic(
      this.audioContext,
      musicFile,
      0.3,
      this.audioEnabled,
    );
  }

  private _playSound(type: 'attack' | 'critical' | 'ko' | 'victory' | 'defeat' | 'block'): void {
    if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
    sharedPlaySound(this.audioContext, type, this.sfxVolume);
  }

  private _playSpellSound(attackId: string): void {
    if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
    sharedPlaySpellSound(this.audioContext, attackId, this.sfxVolume);
  }

  /* ────────────────────────── PixiJS Init ────────────────────────── */

  private async initPixi(): Promise<void> {
    // Clear container
    while (this.container.firstChild) {
      this.container.removeChild(this.container.firstChild);
    }

    const containerW = this.container.clientWidth || DEFAULT_WIDTH;
    const containerH = this.container.clientHeight || DEFAULT_HEIGHT;

    this.app = new PIXI.Application();
    await this.app.init({
      width: containerW,
      height: containerH,
      backgroundColor: 0x1a1a1a,
      antialias: true,
      resizeTo: this.container,
    });

    this.container.appendChild(this.app.canvas);
    this.stage = this.app.stage;

    // Anti-exploit: remove PixiJS debug global and freeze ticker speed
    delete (globalThis as Record<string, unknown>).__PIXI_APP__;
    delete (globalThis as Record<string, unknown>).__PIXI_STAGE__;
    try {
      Object.defineProperty(this.app.ticker, 'speed', {
        value: 1, writable: false, configurable: false,
      });
    } catch { /* already frozen */ }

    // WebGL context loss — let PixiJS recover automatically
    this._onContextLost = (e: Event) => {
      console.warn('WebGL context lost — battle continues on restore');
      e.preventDefault();
    };
    this.app.canvas.addEventListener('webglcontextlost', this._onContextLost);

    this._onContextRestored = () => {
      console.log('WebGL context restored');
    };
    this.app.canvas.addEventListener('webglcontextrestored', this._onContextRestored);

    // Resize handler
    this._onResize = () => {
      // PixiJS resizeTo handles canvas size; layout scales via aspect ratio
    };
    window.addEventListener('resize', this._onResize);

    await this.loadAssets();
    this.create();
  }

  /* ────────────────────────── Asset Loading ──────────────────────── */

  private async loadAssets(): Promise<void> {
    this.bgAlias = `bg_${this.backgroundPath}`;
    this._playerAlias = `player_${this.playerSpritePath}`;

    const tryLoad = async (alias: string, src: string, fallbackAlias: string | null): Promise<void> => {
      if (loadedAssetAliases.has(alias)) return;
      try {
        await PIXI.Assets.load({ alias, src: src + SESSION_CACHE_BUST });
        loadedAssetAliases.add(alias);
      } catch (e) {
        console.warn(`Sprite 404, using fallback: ${src}`, (e as Error).message);
        if (fallbackAlias && loadedAssetAliases.has(fallbackAlias)) {
          try {
            const fallbackTex = PIXI.Assets.get(fallbackAlias);
            if (fallbackTex) PIXI.Assets.cache.set(alias, fallbackTex);
            loadedAssetAliases.add(alias);
          } catch { /* fallback also failed */ }
        }
      }
    };

    // Background and player
    const defaultBgAlias = `bg_${DEFAULT_SPRITES.background}`;
    const defaultPlayerAlias = `player_${DEFAULT_SPRITES.player}`;
    await tryLoad(defaultBgAlias, DEFAULT_SPRITES.background, null);
    await tryLoad(defaultPlayerAlias, DEFAULT_SPRITES.player, null);
    await tryLoad(this.bgAlias, this.backgroundPath, defaultBgAlias);
    await tryLoad(this._playerAlias, this.playerSpritePath, defaultPlayerAlias);

    // Enemy sprites
    this.enemySpriteAliases = [];
    const defaultEnemyPath = DEFAULT_SPRITES.enemies.normal;
    const defaultEnemyAlias = `enemy_${defaultEnemyPath}`;
    await tryLoad(defaultEnemyAlias, defaultEnemyPath, null);

    if (this.enemySpritePaths && Array.isArray(this.enemySpritePaths)) {
      for (let i = 0; i < this.enemySpritePaths.length; i++) {
        const alias = `enemy_${this.enemySpritePaths[i]}`;
        this.enemySpriteAliases.push(alias);
        await tryLoad(alias, this.enemySpritePaths[i], defaultEnemyAlias);
      }
    }
  }

  /* ────────────────────────── Scene Creation ─────────────────────── */

  private create(): void {
    if (!this.app || !this.stage) return;
    const { width, height } = this.app.screen;
    this.isMobile = width <= height || width < 500;

    // Background
    this.backgroundSprite = PIXI.Sprite.from(this.bgAlias);
    this.backgroundSprite.width = width;
    this.backgroundSprite.height = height;
    this.backgroundSprite.x = width / 2;
    this.backgroundSprite.y = height / 2;
    this.backgroundSprite.anchor.set(0.5);
    this.stage.addChild(this.backgroundSprite);

    // Dark overlay
    const overlay = new PIXI.Graphics();
    overlay.rect(0, 0, width, height);
    overlay.fill({ color: 0x000000, alpha: 0.3 });
    this.stage.addChild(overlay);
    this._overlay = overlay;

    this.createPlayer(width, height);
    this.createEnemies(width, height);
    this.createHudBars(width, height);

    if (this.interactiveMode) {
      this.initInteractiveState();
      this.createSpellBar();
      this.createConsumableBar();
      this.startInteractiveBattle();
    } else {
      this.preprocessInitialEvents();
      this.startTimedBattle();
    }

    this.app.ticker.add(() => this.update());
  }

  /* ────────────────────────── Pre-processing ─────────────────────── */

  private preprocessInitialEvents(): void {
    let firstPlayerAttackIndex = -1;
    for (let i = 0; i < this.eventsList.length; i++) {
      const evt = this.eventsList[i];
      const evtType = getEventField<string>(evt, 'Type');
      const attacker = getEventField<string>(evt, 'Attacker');
      if (evtType === 'Attack' && (attacker === 'Attacker' || attacker === 'Player')) {
        firstPlayerAttackIndex = i;
        break;
      }
    }

    for (let i = 0; i < this.eventsList.length && i < firstPlayerAttackIndex; i++) {
      const evt = this.eventsList[i];
      const evtType = getEventField<string>(evt, 'Type');
      const maxHP = getEventField<number>(evt, 'MaxHP');
      if (evtType === 'HPUpdate' && maxHP) {
        this.processInitialHPEvent(evt);
      }
    }

    this.currentEventIndex = firstPlayerAttackIndex >= 0 ? firstPlayerAttackIndex : 0;
    this.isInitialSetup = false;

    this.playerSpeedBarTimer = this.playerActionTime * 1000;
    for (let i = 0; i < this.enemyCount; i++) {
      this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1000;
    }
  }

  private processInitialHPEvent(evt: BattleEvent): void {
    const character = getEventField<string>(evt, 'Character') ?? '';
    const hp = getEventField<number>(evt, 'HP') ?? 0;
    const maxHP = getEventField<number>(evt, 'MaxHP') ?? 0;
    const actionTime = getEventField<number>(evt, 'ActionTime');

    if (character === 'Attacker' || character === 'Player') {
      this.playerMaxHp = maxHP;
      this.playerCurrentHp = hp;
      if (actionTime) this.playerActionTime = actionTime;
      if (this.playerHpBar) {
        const ratio = Math.max(0, hp / maxHP);
        this.playerHpBar.bar.width = this.playerHpBar.maxWidth * ratio;
        if (this.playerHpBar.text) {
          this.playerHpBar.text.text = `${formatNum(hp)}/${formatNum(maxHP)}`;
        }
      }
    } else if (character.startsWith('Enemy')) {
      const enemyIndex = parseInt(character.replace('Enemy', ''));
      if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemyHPs.length) {
        this.enemyHPs[enemyIndex].max = maxHP;
        this.enemyHPs[enemyIndex].current = hp;
        if (actionTime) this.enemyActionTimes[enemyIndex] = actionTime;
        const hpBarData = this.enemyHpBars[enemyIndex];
        if (hpBarData?.text) {
          hpBarData.bar.width = hpBarData.maxWidth;
          hpBarData.text.text = `${formatNum(hp)}/${formatNum(maxHP)}`;
          hpBarData.text.visible = true;
        }
        if (this.bossHpBar && enemyIndex === 0) {
          const ratio = Math.max(0, hp / maxHP);
          this.bossHpBar.bar.width = this.bossHpBar.maxWidth * ratio;
          this.bossHpBar.text.text = `${formatNum(hp)} / ${formatNum(maxHP)} HP`;
        }
      }
    } else if (character === 'Defender' && this.enemyCount === 1) {
      this.enemyHPs[0].max = maxHP;
      this.enemyHPs[0].current = hp;
      if (actionTime) this.enemyActionTimes[0] = actionTime;
      const hpBarData = this.enemyHpBars[0];
      if (hpBarData?.text) {
        hpBarData.bar.width = hpBarData.maxWidth;
        hpBarData.text.text = `${formatNum(hp)}/${formatNum(maxHP)}`;
        hpBarData.text.visible = true;
      }
      if (this.bossHpBar) {
        const ratio = Math.max(0, hp / maxHP);
        this.bossHpBar.bar.width = this.bossHpBar.maxWidth * ratio;
        this.bossHpBar.text.text = `${formatNum(hp)} / ${formatNum(maxHP)} HP`;
      }
    }
  }

  /* ────────────────────────── Create Player ──────────────────────── */

  private createPlayer(width: number, height: number): void {
    if (!this.stage) return;
    const bottomBarReserve = Math.min(140, height * 0.15);
    const groundOffset = bottomBarReserve + 10;

    let playerX: number, playerY: number;
    if (this.isMobile) {
      playerX = width * 0.5;
      playerY = height - groundOffset;
    } else {
      playerX = width * 0.25;
      playerY = height - groundOffset;
    }

    this.playerSprite = PIXI.Sprite.from(this._playerAlias);
    this.playerSprite.anchor.set(0.5, 1);
    this.playerSprite.x = playerX;
    this.playerSprite.y = playerY;

    const maxSpriteHeight = this.isMobile ? height * 0.25 : height * 0.45;
    const scale = Math.min(1, maxSpriteHeight / this.playerSprite.height);
    this.playerSprite.scale.set(scale);

    this.playerX = playerX;
    this.playerDisplayHeight = this.playerSprite.height;

    // Blue glow outline behind sprite for shot buff (match CSS home page look)
    if (this.hasShotBuff) {
      this.playerAura = PIXI.Sprite.from(this._playerAlias);
      this.playerAura.anchor.set(0.5, 1);
      this.playerAura.scale.set(this.playerSprite.scale.x * 1.25);
      this.playerAura.x = playerX;
      this.playerAura.y = playerY;
      this.playerAura.alpha = 0.8;
      const cm = new PIXI.ColorMatrixFilter();
      cm.matrix = [
        0, 0, 0, 0, 0,
        0, 0, 0, 0, 0.667,
        0, 0, 0, 0, 1,
        0, 0, 0, 1, 0,
      ];
      this.playerAura.filters = [cm, new PIXI.BlurFilter({ strength: 12 })];
      this.stage.addChild(this.playerAura);
    }

    this.stage.addChild(this.playerSprite);

    this.playerIdleOffset = {
      baseX: playerX,
      baseY: playerY,
      phase: Math.PI,
      isAerial: false,
      bobAmplitude: 3,
      swayAmplitude: 2,
    };
  }

  /* ────────────────────────── HUD Bars ───────────────────────────── */

  private createHudBars(width: number, height: number): void {
    if (!this.stage) return;
    const isMobile = this.isMobile;
    const barWidth = isMobile ? Math.min(220, width * 0.32) : Math.min(400, width * 0.40);
    const barHeight = isMobile ? Math.min(22, height * 0.035) : Math.min(36, height * 0.055);
    const topBarHeight = 54;
    const paddingTop = topBarHeight + 8;
    const paddingLeft = Math.min(16, width * 0.03);

    // ── Player HP Bar ──
    const hpBg = new PIXI.Graphics();
    hpBg.roundRect(paddingLeft, paddingTop, barWidth, barHeight, barHeight / 2);
    hpBg.fill({ color: 0x1a1a1a, alpha: 0.85 });
    hpBg.stroke({ color: 0x333333, width: 1 });
    this.stage.addChild(hpBg);

    const hpFill = new PIXI.Graphics();
    hpFill.roundRect(0, 0, barWidth, barHeight, barHeight / 2);
    hpFill.fill(0x4caf50);
    hpFill.x = paddingLeft;
    hpFill.y = paddingTop;
    this.stage.addChild(hpFill);

    const hpBorder = new PIXI.Graphics();
    hpBorder.roundRect(paddingLeft, paddingTop, barWidth, barHeight, barHeight / 2);
    hpBorder.stroke({ width: 1.5, color: 0x66bb6a });
    this.stage.addChild(hpBorder);

    const hpFontSize = isMobile ? Math.min(12, barHeight * 0.55) : Math.min(16, barHeight * 0.5);
    const hpText = new PIXI.Text({
      text: '',
      style: {
        fontFamily: 'Arial, sans-serif', fontSize: hpFontSize, fontWeight: 'bold',
        fill: 0xffffff, stroke: { color: 0x000000, width: 2 },
      },
    });
    hpText.anchor.set(0.5, 0.5);
    hpText.x = paddingLeft + barWidth / 2;
    hpText.y = paddingTop + barHeight / 2;
    this.stage.addChild(hpText);

    this.playerHpBar = {
      bar: hpFill, barBg: hpBg, border: hpBorder,
      text: hpText, maxWidth: barWidth, barHeight,
      x: paddingLeft, y: paddingTop,
    };

    // ── Player Speed Bar ──
    const speedBarHeight = isMobile ? Math.min(10, height * 0.015) : Math.min(18, height * 0.025);
    const speedBarY = paddingTop + barHeight + 3;

    const speedBg = new PIXI.Graphics();
    speedBg.roundRect(paddingLeft, speedBarY, barWidth, speedBarHeight, speedBarHeight / 2);
    speedBg.fill({ color: 0x111111, alpha: 0.85 });
    this.stage.addChild(speedBg);

    const speedFill = new PIXI.Graphics();
    speedFill.roundRect(0, 0, barWidth, speedBarHeight, speedBarHeight / 2);
    speedFill.fill(0x00bcd4);
    speedFill.x = paddingLeft;
    speedFill.y = speedBarY;
    this.stage.addChild(speedFill);

    const speedFontSize = isMobile ? Math.min(8, speedBarHeight * 0.8) : Math.min(14, speedBarHeight * 0.8);
    const speedText = new PIXI.Text({
      text: '',
      style: {
        fontFamily: 'Arial, sans-serif', fontSize: speedFontSize, fontWeight: 'bold',
        fill: 0xffffff, stroke: { color: 0x000000, width: 2 },
      },
    });
    speedText.anchor.set(0.5, 0.5);
    speedText.x = paddingLeft + barWidth / 2;
    speedText.y = speedBarY + speedBarHeight / 2;
    this.stage.addChild(speedText);

    this.playerSpeedBar = {
      bar: speedFill, barBg: speedBg, maxWidth: barWidth,
      text: speedText, barHeight: speedBarHeight,
    };

    // ── Canhão AOE Timer Bar + Penalty Lifesteal Timer Bar ──
    // Anchored to the BOTTOM-LEFT corner of the canvas.
    const bottomPadding = isMobile ? 10 : 12;
    const timerBarX = paddingLeft;
    const timerBarWidth = isMobile ? Math.min(240, width * 0.38) : Math.min(440, width * 0.44);
    const canhaoBarHeight = isMobile ? 14 : 22;
    const penaltyBarHeightCalc = isMobile ? 14 : 22;
    const penaltyBarYCalc = height - bottomPadding - penaltyBarHeightCalc;
    const canhaoBarY = penaltyBarYCalc - 4 - canhaoBarHeight;

    // Canhão bar
    const canhaoBg = new PIXI.Graphics();
    canhaoBg.roundRect(timerBarX, canhaoBarY, timerBarWidth, canhaoBarHeight, canhaoBarHeight / 2);
    canhaoBg.fill({ color: 0x1a0000, alpha: 0.85 });
    this.stage.addChild(canhaoBg);

    const canhaoFill = new PIXI.Graphics();
    canhaoFill.roundRect(0, 0, timerBarWidth, canhaoBarHeight, canhaoBarHeight / 2);
    canhaoFill.fill(0xef5350);
    canhaoFill.x = timerBarX;
    canhaoFill.y = canhaoBarY;
    this.stage.addChild(canhaoFill);

    const canhaoLabelFontSize = isMobile ? 8 : 13;
    const canhaoLabel = new PIXI.Text({
      text: '\u{1F4A5} AOE',
      style: { fontFamily: 'Arial, sans-serif', fontSize: canhaoLabelFontSize, fontWeight: 'bold', fill: 0xffffff, stroke: { color: 0x000000, width: 2 } },
    });
    canhaoLabel.anchor.set(0, 0.5);
    canhaoLabel.x = timerBarX + 6;
    canhaoLabel.y = canhaoBarY + canhaoBarHeight / 2;
    this.stage.addChild(canhaoLabel);

    const canhaoFontSize = isMobile ? 9 : 14;
    const canhaoText = new PIXI.Text({
      text: '',
      style: { fontFamily: 'Arial, sans-serif', fontSize: canhaoFontSize, fontWeight: 'bold', fill: 0xffffff, stroke: { color: 0x000000, width: 2 } },
    });
    canhaoText.anchor.set(1, 0.5);
    canhaoText.x = timerBarX + timerBarWidth - 6;
    canhaoText.y = canhaoBarY + canhaoBarHeight / 2;
    this.stage.addChild(canhaoText);

    const hasCanhao = this.canhaoBuffExpiresAt != null && Date.now() < this.canhaoBuffExpiresAt;
    canhaoBg.visible = hasCanhao;
    canhaoFill.visible = hasCanhao;
    canhaoLabel.visible = hasCanhao;
    canhaoText.visible = hasCanhao;

    this.canhaoTimerBar = {
      bar: canhaoFill, barBg: canhaoBg, label: canhaoLabel,
      text: canhaoText, maxWidth: timerBarWidth, barHeight: canhaoBarHeight,
    };

    // Penalty Lifesteal bar
    const penaltyBarHeight = penaltyBarHeightCalc;
    const penaltyBarY = penaltyBarYCalc;

    const penaltyBg = new PIXI.Graphics();
    penaltyBg.roundRect(timerBarX, penaltyBarY, timerBarWidth, penaltyBarHeight, penaltyBarHeight / 2);
    penaltyBg.fill({ color: 0x1a0a00, alpha: 0.85 });
    this.stage.addChild(penaltyBg);

    const penaltyFill = new PIXI.Graphics();
    penaltyFill.roundRect(0, 0, timerBarWidth, penaltyBarHeight, penaltyBarHeight / 2);
    penaltyFill.fill(0xff9800);
    penaltyFill.x = timerBarX;
    penaltyFill.y = penaltyBarY;
    this.stage.addChild(penaltyFill);

    const penaltyLabelFontSize = isMobile ? 8 : 13;
    const penaltyLabel = new PIXI.Text({
      text: '\u26A1 Lifesteal',
      style: { fontFamily: 'Arial, sans-serif', fontSize: penaltyLabelFontSize, fontWeight: 'bold', fill: 0xffffff, stroke: { color: 0x000000, width: 2 } },
    });
    penaltyLabel.anchor.set(0, 0.5);
    penaltyLabel.x = timerBarX + 6;
    penaltyLabel.y = penaltyBarY + penaltyBarHeight / 2;
    this.stage.addChild(penaltyLabel);

    const penaltyFontSize = isMobile ? 9 : 14;
    const penaltyText = new PIXI.Text({
      text: '',
      style: { fontFamily: 'Arial, sans-serif', fontSize: penaltyFontSize, fontWeight: 'bold', fill: 0xffffff, stroke: { color: 0x000000, width: 2 } },
    });
    penaltyText.anchor.set(1, 0.5);
    penaltyText.x = timerBarX + timerBarWidth - 6;
    penaltyText.y = penaltyBarY + penaltyBarHeight / 2;
    this.stage.addChild(penaltyText);

    const hasPenaltyTimer = this.penaltyBuffExpiresAt != null && Date.now() < this.penaltyBuffExpiresAt;
    penaltyBg.visible = hasPenaltyTimer;
    penaltyFill.visible = hasPenaltyTimer;
    penaltyLabel.visible = hasPenaltyTimer;
    penaltyText.visible = hasPenaltyTimer;

    this.penaltyTimerBar = {
      bar: penaltyFill, barBg: penaltyBg, label: penaltyLabel,
      text: penaltyText, maxWidth: timerBarWidth, barHeight: penaltyBarHeight,
    };

    // ── Boss HUD bars (top-right, mirrored, red) ──
    const isBossMode = this.enemyType?.toLowerCase() === 'boss';
    if (isBossMode) {
      this.createBossHudBars(width, height);
    }
  }

  private createBossHudBars(width: number, height: number): void {
    if (!this.stage) return;
    const isMobile = this.isMobile;
    const barWidth = isMobile ? Math.min(220, width * 0.32) : Math.min(400, width * 0.40);
    const barHeight = isMobile ? Math.min(22, height * 0.035) : Math.min(36, height * 0.055);
    const topBarHeight = 54;
    const paddingTop = topBarHeight + 8;
    const paddingLeft = Math.min(16, width * 0.03);
    const speedBarHeight = isMobile ? Math.min(10, height * 0.015) : Math.min(18, height * 0.025);
    const speedBarY = paddingTop + barHeight + 3;
    const hpFontSize = isMobile ? Math.min(12, barHeight * 0.55) : Math.min(16, barHeight * 0.5);
    const speedFontSize = isMobile ? Math.min(8, speedBarHeight * 0.8) : Math.min(14, speedBarHeight * 0.8);

    const rightX = width - paddingLeft - barWidth;

    // Boss HP
    const bossHpBg = new PIXI.Graphics();
    bossHpBg.roundRect(rightX, paddingTop, barWidth, barHeight, barHeight / 2);
    bossHpBg.fill({ color: 0x1a1a1a, alpha: 0.85 });
    bossHpBg.stroke({ color: 0x333333, width: 1 });
    this.stage.addChild(bossHpBg);

    const bossHpFill = new PIXI.Graphics();
    bossHpFill.roundRect(0, 0, barWidth, barHeight, barHeight / 2);
    bossHpFill.fill(0xf44336);
    bossHpFill.x = rightX;
    bossHpFill.y = paddingTop;
    this.stage.addChild(bossHpFill);

    const bossHpBorder = new PIXI.Graphics();
    bossHpBorder.roundRect(rightX, paddingTop, barWidth, barHeight, barHeight / 2);
    bossHpBorder.stroke({ width: 1.5, color: 0xef5350 });
    this.stage.addChild(bossHpBorder);

    const bossHpText = new PIXI.Text({
      text: '',
      style: {
        fontFamily: 'Arial, sans-serif', fontSize: hpFontSize, fontWeight: 'bold',
        fill: 0xffffff, stroke: { color: 0x000000, width: 2 },
      },
    });
    bossHpText.anchor.set(0.5, 0.5);
    bossHpText.x = rightX + barWidth / 2;
    bossHpText.y = paddingTop + barHeight / 2;
    this.stage.addChild(bossHpText);

    this.bossHpBar = {
      bar: bossHpFill, barBg: bossHpBg, border: bossHpBorder,
      text: bossHpText, maxWidth: barWidth, barHeight,
      x: rightX, y: paddingTop,
    };

    // Boss Speed Bar
    const bossSpeedBg = new PIXI.Graphics();
    bossSpeedBg.roundRect(rightX, speedBarY, barWidth, speedBarHeight, speedBarHeight / 2);
    bossSpeedBg.fill({ color: 0x111111, alpha: 0.85 });
    this.stage.addChild(bossSpeedBg);

    const bossSpeedFill = new PIXI.Graphics();
    bossSpeedFill.roundRect(0, 0, barWidth, speedBarHeight, speedBarHeight / 2);
    bossSpeedFill.fill(0x00bcd4);
    bossSpeedFill.x = rightX;
    bossSpeedFill.y = speedBarY;
    this.stage.addChild(bossSpeedFill);

    const bossSpeedText = new PIXI.Text({
      text: '',
      style: {
        fontFamily: 'Arial, sans-serif', fontSize: speedFontSize, fontWeight: 'bold',
        fill: 0xffffff, stroke: { color: 0x000000, width: 2 },
      },
    });
    bossSpeedText.anchor.set(0.5, 0.5);
    bossSpeedText.x = rightX + barWidth / 2;
    bossSpeedText.y = speedBarY + speedBarHeight / 2;
    this.stage.addChild(bossSpeedText);

    this.bossSpeedBar = {
      bar: bossSpeedFill, barBg: bossSpeedBg,
      text: bossSpeedText, maxWidth: barWidth, barHeight: speedBarHeight,
    };
  }

  /* ────────────────────────── Create Enemies ─────────────────────── */

  private createEnemies(width: number, height: number): void {
    if (!this.stage) return;
    this.enemySprites = [];
    this.enemyHpBars = [];
    this.enemySpeedBars = [];
    this.enemyIdleOffsets = [];

    const isMobile = this.isMobile;
    const bottomBarReserve = Math.min(140, height * 0.15);
    const groundOffset = bottomBarReserve + 10;

    let enemyX: number, baseEnemyY: number;
    if (isMobile) {
      enemyX = width * 0.5;
      const topBarH = 60;
      baseEnemyY = topBarH + height * 0.32;
    } else {
      enemyX = width * 0.72;
      baseEnemyY = height - groundOffset;
    }

    const positions = this.calculateEnemyPositions(
      isMobile, this.enemyCount, enemyX, baseEnemyY, width, height, this.enemyPlacements,
    );

    // Scale factor based on enemy count
    const isBoss = this.enemyType?.toLowerCase() === 'boss';
    let countScaleFactor = 1.0;
    if (isMobile) {
      if (this.enemyCount >= 9) countScaleFactor = 0.38;
      else if (this.enemyCount >= 8) countScaleFactor = 0.42;
      else if (this.enemyCount >= 7) countScaleFactor = 0.48;
      else if (this.enemyCount >= 6) countScaleFactor = 0.52;
      else if (this.enemyCount >= 5) countScaleFactor = 0.60;
      else if (this.enemyCount >= 4) countScaleFactor = 0.75;
      else if (this.enemyCount >= 3) countScaleFactor = 0.85;
    } else {
      if (this.enemyCount >= 9) countScaleFactor = 0.40;
      else if (this.enemyCount >= 8) countScaleFactor = 0.42;
      else if (this.enemyCount >= 7) countScaleFactor = 0.48;
      else if (this.enemyCount >= 6) countScaleFactor = 0.55;
      else if (this.enemyCount >= 5) countScaleFactor = 0.65;
      else if (this.enemyCount >= 4) countScaleFactor = 0.85;
      else if (this.enemyCount >= 3) countScaleFactor = 0.92;
    }
    const bossBoost = isBoss ? 1.25 : 1.0;

    for (let i = 0; i < this.enemyCount; i++) {
      const pos = positions[i];
      if (!pos) continue;

      const isAerial = pos.isAerial || (this.enemyPlacements?.[i] === 1);
      this.enemyIdleOffsets.push({
        baseX: pos.x, baseY: pos.y,
        phase: i * (Math.PI / 2),
        isAerial,
        bobAmplitude: isAerial ? 6 : 3,
        swayAmplitude: isAerial ? 4 : 2,
      });

      const alias = this.enemySpriteAliases?.[i] ?? `enemy_${this.enemySpritePaths[i]}`;
      const enemy = PIXI.Sprite.from(alias);
      enemy.anchor.set(0.5, 1);
      enemy.x = pos.x;
      enemy.y = pos.y;

      const mobileScale = isMobile ? 0.28 : 0.40;
      const maxSpriteHeight = height * mobileScale;
      const baseScale = Math.min(1, maxSpriteHeight / enemy.height);
      const finalScale = baseScale * countScaleFactor * bossBoost;
      enemy.scale.set(finalScale);

      this.stage.addChild(enemy);
      this.enemySprites.push(enemy);

      // Per-enemy HP bar
      const hpBarY = pos.y - enemy.height - 5;
      const hpBarWidth = isMobile ? 35 : 60;
      const hpBarHeight = isMobile ? 4 : 8;

      const barBg = new PIXI.Graphics();
      barBg.rect(pos.x - hpBarWidth / 2, hpBarY - hpBarHeight / 2, hpBarWidth, hpBarHeight);
      barBg.fill(0x333333);
      this.stage.addChild(barBg);

      const bar = new PIXI.Graphics();
      bar.rect(0, 0, hpBarWidth, hpBarHeight);
      bar.fill(0xff4444);
      bar.x = pos.x - hpBarWidth / 2;
      bar.y = hpBarY - hpBarHeight / 2;
      this.stage.addChild(bar);

      const hpFontSize = isMobile ? 7 : 10;
      const hpTxt = new PIXI.Text({
        text: '',
        style: {
          fontFamily: 'Arial', fontSize: hpFontSize, fontWeight: 'bold',
          fill: 0xffffff, stroke: { color: 0x000000, width: 2 },
        },
      });
      const showText = !isMobile || this.enemyCount <= 2;
      hpTxt.anchor.set(0.5, 0.5);
      hpTxt.x = pos.x;
      hpTxt.y = hpBarY - 8;
      hpTxt.visible = showText;
      this.stage.addChild(hpTxt);

      this.enemyHpBars.push({
        bar, barBg, text: hpTxt,
        maxWidth: hpBarWidth, barHeight: hpBarHeight,
      });

      // Per-enemy speed bar (below HP bar)
      const speedBarWidth = hpBarWidth;
      const speedBarH = isMobile ? 2 : 4;
      const speedBarYPos = hpBarY + hpBarHeight / 2 + 2;

      const speedBg = new PIXI.Graphics();
      speedBg.rect(pos.x - speedBarWidth / 2, speedBarYPos, speedBarWidth, speedBarH);
      speedBg.fill({ color: 0x111111, alpha: 0.7 });
      this.stage.addChild(speedBg);

      const speedFill = new PIXI.Graphics();
      speedFill.rect(0, 0, speedBarWidth, speedBarH);
      speedFill.fill(0x00bcd4);
      speedFill.x = pos.x - speedBarWidth / 2;
      speedFill.y = speedBarYPos;
      this.stage.addChild(speedFill);

      this.enemySpeedBars.push({
        bar: speedFill, barBg: speedBg,
        maxWidth: speedBarWidth, barHeight: speedBarH,
      });
    }

    // Hide individual HP + speed bars for bosses (boss uses HUD bars in top-right)
    if (isBoss) {
      for (const hb of this.enemyHpBars) {
        hb.bar.visible = false;
        hb.barBg.visible = false;
        hb.text.visible = false;
      }
      for (const sb of this.enemySpeedBars) {
        sb.bar.visible = false;
        sb.barBg.visible = false;
      }
    }
  }

  /* ───────────────── Enemy Position Calculations ─────────────────── */

  private calculateEnemyPositions(
    isMobile: boolean,
    count: number,
    baseX: number,
    baseY: number,
    width: number,
    height: number,
    placements: number[],
  ): EnemyPosition[] {
    const positions: EnemyPosition[] = [];

    /* ── Optimal grid columns per enemy count ── */
    const colsLookup = isMobile
      //         0  1  2  3  4  5  6  7  8  9
      ? [0, 1, 2, 3, 2, 3, 3, 4, 4, 3]
      : [0, 1, 2, 3, 2, 3, 3, 4, 4, 3];
    const cols = colsLookup[count] ?? Math.min(4, count);
    const rows = Math.ceil(count / cols);

    /* ── Approximate scaled sprite size (must mirror createEnemies) ── */
    let csf = 1.0;
    if (isMobile) {
      if (count >= 9) csf = 0.38;
      else if (count >= 8) csf = 0.42;
      else if (count >= 7) csf = 0.48;
      else if (count >= 6) csf = 0.52;
      else if (count >= 5) csf = 0.60;
      else if (count >= 4) csf = 0.75;
      else if (count >= 3) csf = 0.85;
    } else {
      if (count >= 9) csf = 0.40;
      else if (count >= 8) csf = 0.42;
      else if (count >= 7) csf = 0.48;
      else if (count >= 6) csf = 0.55;
      else if (count >= 5) csf = 0.65;
      else if (count >= 4) csf = 0.85;
      else if (count >= 3) csf = 0.92;
    }
    const spriteH = height * (isMobile ? 0.28 : 0.40) * csf;
    const spriteW = spriteH * 0.7;

    /* ── Horizontal spacing: sprite-width-based, clamped to available area ── */
    const idealHSpace = spriteW * 1.15;                       // snug but no overlap
    const hMargin = spriteW * 0.5 + (isMobile ? 8 : 12);
    const maxLeftHalf = baseX - hMargin;
    const maxRightHalf = width - hMargin - baseX;
    const maxHalfW = Math.min(maxLeftHalf, maxRightHalf);
    const maxFormW = Math.max(0, 2 * maxHalfW);
    const maxHSpace = cols > 1 ? maxFormW / (cols - 1) : 0;
    const hSpace = cols > 1 ? Math.min(idealHSpace, maxHSpace) : 0;

    /* ── Vertical row offset: tiny depth stagger for ground enemies ── */
    const depthOffset = spriteH * 0.12;                       // subtle back-row nudge

    /* ── Aerial lift: enough to visibly float above ground enemies ── */
    const aerialLift = spriteH * 0.50;

    /* ── Place enemies in grid (row 0 = front / bottom, row N = back / top) ── */
    let idx = 0;
    for (let row = 0; row < rows; row++) {
      const inRow = Math.min(cols, count - idx);
      const rowW = (inRow - 1) * hSpace;
      const startX = baseX - rowW / 2;
      for (let col = 0; col < inRow; col++) {
        const isAerial = placements?.[idx] === 1;
        const x = startX + col * hSpace;
        // Ground enemies: stay near baseY with a tiny depth nudge per row
        // Aerial enemies: lift above baseY
        const y = isAerial
          ? baseY - aerialLift - row * depthOffset
          : baseY - row * depthOffset;
        positions.push({ x, y, isAerial });
        idx++;
      }
    }

    return positions;
  }

  /* ────────────────── Timed Battle (pre-computed) ────────────────── */

  private startTimedBattle(): void {
    this.isPlaying = true;
    this.battleStartTime = Date.now();
    this.currentSimTime = 0;
    this.battleFinished = false;
  }

  /* ──────────────── Interactive Mode Init ────────────────────────── */

  private initInteractiveState(): void {
    if (this.interactivePlayerHP != null) this.playerCurrentHp = this.interactivePlayerHP;
    if (this.interactivePlayerMaxHP != null) this.playerMaxHp = this.interactivePlayerMaxHP;
    if (this.interactivePlayerActionTime != null) this.playerActionTime = this.interactivePlayerActionTime;

    for (let i = 0; i < this.interactiveEnemies.length && i < this.enemyCount; i++) {
      const e = this.interactiveEnemies[i];
      const hp = (e.hp ?? e.HP ?? 100) as number;
      const maxHP = (e.maxHP ?? e.MaxHP ?? hp) as number;
      const at = (e.actionTime ?? e.ActionTime ?? 3.5) as number;
      this.enemyHPs[i] = { current: hp, max: maxHP };
      this.enemyActionTimes[i] = at;
    }

    this.playerSpeedBarTimer = this.playerActionTime * 1000;
    for (let i = 0; i < this.enemyCount; i++) {
      this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1000;
    }

    this.updatePlayerHPBar();
    for (let i = 0; i < this.enemyCount; i++) {
      this.updateIndividualEnemyHPBar(i);
    }
  }

  /* ────────────────────── Spell Bar UI ───────────────────────────── */

  private createSpellBar(): void {
    if (!this.spells || this.spells.length === 0 || !this.app || !this.stage) return;

    const { width, height } = this.app.screen;
    const isMobile = this.isMobile;
    const barY = height - (isMobile ? 95 : 85);
    const btnSize = isMobile ? 42 : 52;
    const gap = isMobile ? 6 : 10;
    const totalWidth = this.spells.length * btnSize + (this.spells.length - 1) * gap;
    const startX = (width - totalWidth) / 2;

    this.spellBarContainer = new PIXI.Container();
    this.spellBarContainer.y = barY;
    this.stage.addChild(this.spellBarContainer);

    this.spellButtons = [];
    for (let i = 0; i < this.spells.length; i++) {
      const spell = this.spells[i];
      const attackId = (spell.attackId ?? spell.AttackId ?? '') as string;
      const name = (spell.name ?? spell.Name ?? attackId) as string;
      const icon = (spell.icon ?? spell.Icon ?? '⚔️') as string;
      const cooldownSeconds = (spell.cooldownSeconds ?? spell.CooldownSeconds ?? 0) as number;

      const btnContainer = new PIXI.Container();
      btnContainer.x = startX + i * (btnSize + gap);
      btnContainer.y = 0;

      const bg = new PIXI.Graphics();
      bg.roundRect(0, 0, btnSize, btnSize, 8);
      bg.fill({ color: 0x2a2a3e, alpha: 0.92 });
      bg.stroke({ color: 0x5566aa, width: 2 });
      btnContainer.addChild(bg);

      const iconText = new PIXI.Text({
        text: icon,
        style: { fontSize: isMobile ? 16 : 20, fill: 0xffffff },
      });
      iconText.anchor.set(0.5);
      iconText.x = btnSize / 2;
      iconText.y = btnSize / 2 - 6;
      btnContainer.addChild(iconText);

      const nameText = new PIXI.Text({
        text: name.substring(0, 6),
        style: { fontSize: isMobile ? 7 : 9, fill: 0xcccccc, fontFamily: 'Arial' },
      });
      nameText.anchor.set(0.5);
      nameText.x = btnSize / 2;
      nameText.y = btnSize - 6;
      btnContainer.addChild(nameText);

      // Cooldown overlay
      const cdOverlay = new PIXI.Graphics();
      cdOverlay.roundRect(0, 0, btnSize, btnSize, 8);
      cdOverlay.fill({ color: 0x000000, alpha: 0.6 });
      cdOverlay.visible = false;
      btnContainer.addChild(cdOverlay);

      const cdText = new PIXI.Text({
        text: '',
        style: { fontSize: isMobile ? 14 : 18, fill: 0xff6644, fontWeight: 'bold' },
      });
      cdText.anchor.set(0.5);
      cdText.x = btnSize / 2;
      cdText.y = btnSize / 2;
      cdText.visible = false;
      btnContainer.addChild(cdText);

      btnContainer.eventMode = 'static';
      btnContainer.cursor = 'pointer';
      btnContainer.on('pointerdown', () => this.onSpellButtonClick(attackId, cooldownSeconds));

      this.spellBarContainer.addChild(btnContainer);
      this.spellButtons.push({
        container: btnContainer, bg, iconText, nameText,
        cdOverlay, cdText, attackId, cooldownSeconds, spell,
      });
    }
  }

  /* ────────────────────── Consumable Bar UI ──────────────────────── */

  private createConsumableBar(): void {
    if (!this.app || !this.stage) return;
    const { width, height } = this.app.screen;
    const isMobile = this.isMobile;
    const btnGap = isMobile ? 6 : 10;

    // Only fino and caneca show as clickable buttons (like original JS)
    const allConsumables = [
      { type: 'fino',   name: 'Fino',   color: 0xf5a623, fallbackIcon: '\u{1F37A}' },
      { type: 'caneca', name: 'Caneca', color: 0xf5a623, fallbackIcon: '\u{1F37B}' },
    ];
    const consumables = allConsumables.filter(c => (this.consumableQuantities[c.type] ?? 0) > 0);
    if (consumables.length === 0) return;

    // Responsive button size — larger on desktop
    const maxBarWidth = isMobile ? Math.min(width * 0.9, 320) : Math.min(width * 0.9, 420);
    const btnSize = isMobile
      ? Math.min(60, Math.floor((maxBarWidth - (consumables.length - 1) * btnGap) / consumables.length))
      : Math.min(76, Math.floor((maxBarWidth - (consumables.length - 1) * btnGap) / consumables.length));

    const totalWidth = consumables.length * btnSize + (consumables.length - 1) * btnGap;
    const startX = (width - totalWidth) / 2;
    const barY = height - btnSize - 10;

    this.consumableBarContainer = new PIXI.Container();
    this.stage.addChild(this.consumableBarContainer);

    // Semi-transparent backdrop
    const backdrop = new PIXI.Graphics();
    backdrop.roundRect(startX - 8, barY - 6, totalWidth + 16, btnSize + 12, 10);
    backdrop.fill({ color: 0x0d1117, alpha: 0.7 });
    this.consumableBarContainer.addChild(backdrop);

    this.consumableButtons = [];
    for (let i = 0; i < consumables.length; i++) {
      const c = consumables[i];
      const type = c.type;
      const qty = this.consumableQuantities[type] ?? 0;
      const x = startX + i * (btnSize + btnGap);

      const btnContainer = new PIXI.Container();
      btnContainer.x = x;
      btnContainer.y = barY;

      // Button background
      const bg = new PIXI.Graphics();
      bg.roundRect(0, 0, btnSize, btnSize, 8);
      bg.fill({ color: qty > 0 ? 0x1a2332 : 0x1a1a1a, alpha: 0.95 });
      bg.stroke({ color: qty > 0 ? c.color : 0x444444, width: 2 });
      btnContainer.addChild(bg);

      // Icon: load sprite from CDN/local (or fallback emoji)
      let iconSprite: Sprite | null = null;
      const imageUrl = this.consumableImages[type] ?? DEFAULT_CONSUMABLE_IMAGES[type];
      if (imageUrl) {
        const spriteAlias = `consumable_${type}_${imageUrl}`;
        const spriteContainer = new PIXI.Container();
        spriteContainer.x = btnSize / 2;
        spriteContainer.y = btnSize / 2 - 2;
        btnContainer.addChild(spriteContainer);

        (async () => {
          try {
            if (!loadedAssetAliases.has(spriteAlias)) {
              await PIXI.Assets.load({ alias: spriteAlias, src: imageUrl });
              loadedAssetAliases.add(spriteAlias);
            }
            const spr = PIXI.Sprite.from(spriteAlias);
            spr.anchor.set(0.5);
            const maxDim = btnSize * 0.6;
            const scale = Math.min(maxDim / spr.width, maxDim / spr.height);
            spr.scale.set(scale);
            spriteContainer.addChild(spr);
            const btn = this.consumableButtons.find(b => b.type === type);
            if (btn) btn.iconSprite = spr;
          } catch {
            // Fallback: show emoji icon
            const fallback = new PIXI.Text({
              text: c.fallbackIcon,
              style: { fontSize: Math.min(22, btnSize * 0.45), fontFamily: 'Arial, sans-serif' },
            });
            fallback.anchor.set(0.5);
            spriteContainer.addChild(fallback);
          }
        })();
      } else {
        // No image URL — show emoji fallback
        const fallbackText = new PIXI.Text({
          text: c.fallbackIcon,
          style: { fontSize: Math.min(22, btnSize * 0.45), fontFamily: 'Arial, sans-serif' },
        });
        fallbackText.anchor.set(0.5);
        fallbackText.x = btnSize / 2;
        fallbackText.y = btnSize / 2 - 2;
        btnContainer.addChild(fallbackText);
      }

      // Quantity badge
      const qtyText = new PIXI.Text({
        text: `${qty}`,
        style: { fontSize: isMobile ? 10 : 12, fill: 0xffffff, fontWeight: 'bold', fontFamily: 'Arial' },
      });
      qtyText.anchor.set(1, 0);
      qtyText.x = btnSize - 2;
      qtyText.y = 1;
      btnContainer.addChild(qtyText);

      // CD overlay
      const cdOverlay = new PIXI.Graphics();
      cdOverlay.roundRect(0, 0, btnSize, btnSize, 8);
      cdOverlay.fill({ color: 0x000000, alpha: 0.55 });
      cdOverlay.visible = false;
      btnContainer.addChild(cdOverlay);

      const cdText = new PIXI.Text({
        text: '',
        style: { fontSize: isMobile ? 10 : 14, fill: 0xff8844, fontWeight: 'bold' },
      });
      cdText.anchor.set(0.5);
      cdText.x = btnSize / 2;
      cdText.y = btnSize / 2;
      cdText.visible = false;
      btnContainer.addChild(cdText);

      btnContainer.eventMode = 'static';
      btnContainer.cursor = 'pointer';
      btnContainer.on('pointerdown', () => this.onConsumableClick(type));

      this.consumableBarContainer.addChild(btnContainer);
      this.consumableButtons.push({
        type, container: btnContainer, bg, iconSprite,
        qtyText, cdOverlay, cdText,
      });
    }

    // Apply initial state
    for (const btn of this.consumableButtons) {
      this.updateConsumableButton(btn.type);
    }
  }

  private onConsumableClick(type: string): void {
    if (this.battleFinished || this._consumablePending) return;
    const qty = this.consumableQuantities[type] ?? 0;
    if (qty <= 0) return;
    const cd = this.consumableCooldowns[type] ?? 0;
    if (cd > 0) return;
    // Buffs: can't re-use if already active
    if ((type === 'cigarro' || type === 'canhao') && this.activeBuffs[type]) return;
    this.requestUseConsumable(type);
  }

  private async requestUseConsumable(type: string): Promise<void> {
    if (this._destroyed || !this.dotNetRef || this._consumablePending) return;
    this._consumablePending = true;
    try {
      const json = await this.dotNetRef.invokeMethodAsync('OnUseConsumable', type) as string | null;
      if (this._destroyed || this.battleFinished) { this._consumablePending = false; return; }
      if (!json) { this._consumablePending = false; return; }

      const result = JSON.parse(json) as ConsumableResult;
      const success = result.success ?? result.Success ?? false;
      if (!success) { this._consumablePending = false; return; }

      const newQty = result.newQuantity ?? result.NewQuantity;
      if (newQty != null) this.consumableQuantities[type] = newQty;

      const cd = result.cooldownSeconds ?? result.CooldownSeconds ?? 0;
      if (cd > 0) this.consumableCooldowns[type] = cd;

      // Heal consumables
      if (type === 'fino' || type === 'caneca') {
        const hp = result.playerHP ?? result.PlayerHP;
        const maxHP = result.playerMaxHP ?? result.PlayerMaxHP;
        if (hp != null) this.playerCurrentHp = hp;
        if (maxHP != null) this.playerMaxHp = maxHP;
        this.updatePlayerHPBar();
        const heal = result.healAmount ?? result.HealAmount ?? 0;
        if (heal > 0 && this.playerSprite) {
          showFloatingText(this, `+${formatNum(heal)}`, this.playerSprite.x, this.playerSprite.y - this.playerDisplayHeight, 0x44ff44);
          playBuffVfx(this, this.playerSprite, 0x44ff44);
        }
      }

      // Buff consumables
      if (type === 'cigarro' || type === 'canhao') {
        const buffActive = result.buffActive ?? result.BuffActive ?? false;
        this.activeBuffs[type] = buffActive;
        const msg = result.buffMessage ?? result.BuffMessage ?? '';
        if (msg && this.playerSprite) {
          showFloatingText(this, msg, this.playerSprite.x, this.playerSprite.y - this.playerDisplayHeight - 20, 0xffaa00);
        }
        if (this.playerSprite) {
          playBuffVfx(this, this.playerSprite, type === 'cigarro' ? 0xff6600 : 0xff4444);
        }
        // Activate canhao timer bar
        if (type === 'canhao' && buffActive) {
          this.canhaoBuffExpiresAt = Date.now() + this.canhaoBuffDurationMs;
          this.updateCanhaoTimerBar();
        }
      }

      // Shot aura — blue glow outline (match CSS home page look)
      if (type === 'shot') {
        this.hasShotBuff = true;
        if (!this.playerAura && this.playerSprite && this.stage) {
          this.playerAura = PIXI.Sprite.from(this._playerAlias);
          this.playerAura.anchor.set(0.5, 1);
          this.playerAura.scale.set(this.playerSprite.scale.x * 1.25);
          this.playerAura.x = this.playerSprite.x;
          this.playerAura.y = this.playerSprite.y;
          this.playerAura.alpha = 0.8;
          const cm = new PIXI.ColorMatrixFilter();
          cm.matrix = [
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0.667,
            0, 0, 0, 0, 1,
            0, 0, 0, 1, 0,
          ];
          this.playerAura.filters = [cm, new PIXI.BlurFilter({ strength: 12 })];
          const idx = this.stage.getChildIndex(this.playerSprite);
          this.stage.addChildAt(this.playerAura, idx);
        }
        if (this.playerAura) {
          this.playerAura.visible = true;
          this.playerAura.alpha = 0.8;
        }
        const msg = result.buffMessage ?? result.BuffMessage ?? '';
        if (msg && this.playerSprite) {
          showFloatingText(this, msg, this.playerSprite.x, this.playerSprite.y - this.playerDisplayHeight - 20, 0x44bbff);
        }
        if (this.playerSprite) playBuffVfx(this, this.playerSprite, 0x44bbff);
      }

      // Penalty consumable
      if (type === 'penalty') {
        this.hasPenaltyBuff = true;
        const newAt = result.newActionTime ?? result.NewActionTime;
        if (newAt != null) this.playerActionTime = newAt;
        const msg = result.buffMessage ?? result.BuffMessage ?? '';
        if (msg && this.playerSprite) {
          showFloatingText(this, msg, this.playerSprite.x, this.playerSprite.y - this.playerDisplayHeight - 20, 0xaa44ff);
        }
        if (this.playerSprite) playBuffVfx(this, this.playerSprite, 0xaa44ff);
        // Activate penalty timer bar
        this.penaltyBuffExpiresAt = Date.now() + this.penaltyBuffDurationMs;
        this.updatePenaltyTimerBar();
      }

      this.updateConsumableButton(type);
    } catch (e) {
      console.warn('requestUseConsumable error:', (e as Error).message);
    }
    this._consumablePending = false;
  }

  private updateConsumableButton(type: string): void {
    const btn = this.consumableButtons.find(b => b.type === type);
    if (!btn) return;

    const qty = this.consumableQuantities[type] ?? 0;
    btn.qtyText.text = `${qty}`;

    const cd = this.consumableCooldowns[type] ?? 0;
    const isBuffActive = (type === 'cigarro' || type === 'canhao') && this.activeBuffs[type];
    const disabled = qty <= 0 || cd > 0 || isBuffActive;

    btn.cdOverlay.visible = disabled;
    btn.cdText.visible = cd > 0;
    if (cd > 0) btn.cdText.text = `${Math.ceil(cd)}`;

    // Active buff glow
    if (isBuffActive) {
      btn.bg.clear();
      btn.bg.roundRect(0, 0, btn.container.width, btn.container.height, 6);
      btn.bg.fill({ color: 0x443300, alpha: 0.9 });
      btn.bg.stroke({ color: 0xffaa00, width: 2 });
    }

    btn.container.alpha = disabled ? 0.5 : 1;
  }

  private updateConsumableCooldownVisuals(): void {
    for (const btn of this.consumableButtons) {
      const cd = this.consumableCooldowns[btn.type] ?? 0;
      const isBuffActive = (btn.type === 'cigarro' || btn.type === 'canhao') && this.activeBuffs[btn.type];
      const qty = this.consumableQuantities[btn.type] ?? 0;
      const disabled = qty <= 0 || cd > 0 || isBuffActive;

      btn.cdOverlay.visible = disabled;
      btn.cdText.visible = cd > 0;
      if (cd > 0) btn.cdText.text = `${Math.ceil(cd)}`;
      btn.container.alpha = disabled ? 0.5 : 1;
    }
  }

  /* ───────────────── Interactive Battle Start ────────────────────── */

  private startInteractiveBattle(): void {
    this.isPlaying = true;
    this.battleStartTime = Date.now();
    this.currentSimTime = 0;
    this.battleFinished = false;
    this._cooldownTickAccum = 0;
    this._consumableTickAccum = 0;
  }

  private onSpellButtonClick(attackId: string, cooldownSeconds: number): void {
    if (this.battleFinished || this._spellPending) return;
    const cd = this.spellCooldowns[attackId] ?? 0;
    if (cd > 0) return;
    this.requestPlayerSpell(attackId, cooldownSeconds);
  }

  /* ──────────────── Interactive Server Calls ─────────────────────── */

  private async requestPlayerAutoAttack(): Promise<void> {
    if (this._destroyed || !this.dotNetRef || this.battleFinished) {
      this._playerAttackPending = false;
      return;
    }
    try {
      const json = await this.dotNetRef.invokeMethodAsync('OnPlayerAutoAttack') as string | null;
      if (this._destroyed || this.battleFinished) return;
      if (json) this.processServerResult(JSON.parse(json) as CombatActionResult);
    } catch (e) {
      console.warn('requestPlayerAutoAttack error:', (e as Error).message);
    } finally {
      this._playerAttackPending = false;
    }
  }

  private async requestEnemyAttack(enemyIndex: number): Promise<void> {
    if (this._destroyed || !this.dotNetRef || this.battleFinished) {
      this._enemyAttackPending[enemyIndex] = false;
      return;
    }
    try {
      const json = await this.dotNetRef.invokeMethodAsync('OnEnemyAttack', enemyIndex) as string | null;
      if (this._destroyed || this.battleFinished) return;
      if (json) this.processServerResult(JSON.parse(json) as CombatActionResult);
    } catch (e) {
      console.warn('requestEnemyAttack error:', (e as Error).message);
    } finally {
      this._enemyAttackPending[enemyIndex] = false;
    }
  }

  private async requestPlayerSpell(attackId: string, cooldownSeconds: number): Promise<void> {
    if (this._destroyed || !this.dotNetRef || this._spellPending || this.battleFinished) return;
    this._spellPending = true;
    try {
      this.spellCooldowns[attackId] = cooldownSeconds;
      this.updateSpellCooldownVisuals();

      const json = await this.dotNetRef.invokeMethodAsync('OnPlayerSpell', attackId) as string | null;
      if (this._destroyed || this.battleFinished) return;
      if (json) {
        const result = JSON.parse(json) as CombatActionResult;
        const serverCooldowns = (result.spellCooldowns ?? result.SpellCooldowns) as Record<string, number> | undefined;
        if (serverCooldowns) {
          for (const [id, rem] of Object.entries(serverCooldowns)) {
            this.spellCooldowns[id] = rem;
          }
        }
        this.processServerResult(result);
      }
    } catch (e) {
      console.warn('requestPlayerSpell error:', (e as Error).message);
    } finally {
      this._spellPending = false;
      if (!this._destroyed) this.updateSpellCooldownVisuals();
    }
  }

  private async requestTickCooldowns(elapsedSeconds: number): Promise<void> {
    if (this._destroyed || !this.dotNetRef || this.battleFinished) return;
    try {
      const json = await this.dotNetRef.invokeMethodAsync('OnTickCooldowns', elapsedSeconds) as string | null;
      if (this._destroyed || this.battleFinished) return;
      if (json) {
        const data = JSON.parse(json);
        // Server returns { spells: {...} } or direct map
        const spellCooldowns = data.spells ?? data;
        for (const [id, remaining] of Object.entries(spellCooldowns)) {
          this.spellCooldowns[id] = remaining as number;
        }
        this.updateSpellCooldownVisuals();
      }
    } catch (e) {
      console.warn('OnTickCooldowns error:', (e as Error).message);
    }
  }

  private async requestTickConsumableCooldowns(realElapsedSeconds: number): Promise<void> {
    if (this._destroyed || !this.dotNetRef || this.battleFinished) return;
    try {
      const json = await this.dotNetRef.invokeMethodAsync('OnTickConsumableCooldowns', realElapsedSeconds) as string | null;
      if (this._destroyed || this.battleFinished) return;
      if (json) {
        const data = JSON.parse(json) as Record<string, number>;
        for (const [type, remaining] of Object.entries(data)) {
          this.consumableCooldowns[type] = remaining;
        }
        this.updateConsumableCooldownVisuals();
      }
    } catch (e) {
      console.warn('OnTickConsumableCooldowns error:', (e as Error).message);
    }
  }

  /* ──────────────── Server Result Processing ─────────────────────── */

  private processServerResult(result: CombatActionResult): void {
    if (this._destroyed || this.battleFinished) return;
    const events = (result.events ?? result.Events ?? []) as BattleEvent[];
    for (const evt of events) {
      this.processInteractiveEvent(evt);
    }

    const battleOver = result.battleOver ?? result.BattleOver ?? false;
    if (battleOver) {
      const outcome = result.outcome ?? result.Outcome;
      if (outcome === 0) {
        this.handleVictory({ Winner: 'Player' } as unknown as BattleEvent);
      } else if (outcome === 1) {
        this.handleVictory({ Winner: 'Defender' } as unknown as BattleEvent);
      } else {
        this.handleDraw();
      }
    }
  }

  private processInteractiveEvent(evt: BattleEvent): void {
    const evtType = getEventField<string>(evt, 'Type');
    switch (evtType) {
      case 'HPUpdate': this.handleHPUpdate(evt); break;
      case 'Attack': this.handleAttack(evt); break;
      case 'SpellAttack': this.handleSpellAttack(evt); break;
      case 'StatusEffect': this.handleStatusEffect(evt); break;
      case 'KO': this.handleKO(evt); break;
      default: break;
    }
  }

  private handleSpellAttack(evt: BattleEvent): void {
    const attackId = getEventField<string>(evt, 'AttackId') ?? '';
    const vfxType = getEventField<number>(evt, 'VfxType');
    const vfxColor = getEventField<number>(evt, 'VfxColor') ?? 0x00ccff;
    const isAoe = getEventField<boolean>(evt, 'IsAoE') ?? false;

    this._playSpellSound(attackId);

    if (isAoe) {
      for (let i = 0; i < this.enemySprites.length; i++) {
        const enemy = this.enemySprites[i];
        if (enemy && enemy.alpha > 0.3 && this.playerSprite) {
          playSpellVfx(this, vfxType, vfxColor, this.playerSprite, enemy);
        }
      }
    } else {
      const targetIdx = this.resolveEnemyIndex(evt);
      const target = this.enemySprites[targetIdx];
      if (target && this.playerSprite) {
        playSpellVfx(this, vfxType, vfxColor, this.playerSprite, target);
      }
    }

    screenShake(this);
  }

  private resolveEnemyIndex(evt: BattleEvent): number {
    const defender = getEventField<string>(evt, 'Defender') ?? '';
    if (defender.startsWith('Enemy')) {
      const idx = parseInt(defender.replace('Enemy', ''));
      if (!isNaN(idx) && idx >= 0 && idx < this.enemySprites.length) return idx;
    }
    return 0;
  }

  private handleStatusEffect(evt: BattleEvent): void {
    const target = getEventField<string>(evt, 'Target') ?? '';
    const effect = getEventField<string>(evt, 'Effect') ?? '';
    if (target === 'Attacker' || target === 'Player') {
      if (this.playerSprite) showEffectLabel(this, effect, this.playerSprite);
    } else if (target.startsWith('Enemy')) {
      const idx = parseInt(target.replace('Enemy', ''));
      const sprite = this.enemySprites[idx];
      if (sprite) showEffectLabel(this, effect, sprite);
    }
  }

  private updateSpellCooldownVisuals(): void {
    for (const btn of this.spellButtons) {
      const cd = this.spellCooldowns[btn.attackId] ?? 0;
      btn.cdOverlay.visible = cd > 0;
      btn.cdText.visible = cd > 0;
      if (cd > 0) btn.cdText.text = `${Math.ceil(cd)}`;
      btn.container.alpha = cd > 0 ? 0.5 : 1;
    }
  }

  /* ────────────────────── Main Update Loop ───────────────────────── */

  private update(): void {
    if (this._destroyed || !this.app || !this.stage || this.battleFinished) return;

    const delta = this.app.ticker.deltaMS;
    this.idleAnimationTime += delta * 0.001;

    if (this.interactiveMode && !this.battleFinished && this.isPlaying) {
      // Interactive mode: speed bars count down and trigger server-side actions
      const simDelta = delta * this.battleSpeed;
      this.currentSimTime += simDelta;

      // Player speed bar (only tick if player is alive)
      if (this.playerCurrentHp > 0) {
        this.playerSpeedBarTimer = Math.max(0, this.playerSpeedBarTimer - simDelta);
        this.updatePlayerSpeedBar();

        if (this.playerSpeedBarTimer <= 0 && !this._playerAttackPending) {
          this._playerAttackPending = true;
          this.playerSpeedBarTimer = this.playerActionTime * 1000;
          this.requestPlayerAutoAttack();
        }
      }

      // Enemy speed bars (only tick if enemy is alive)
      for (let i = 0; i < this.enemyCount; i++) {
        if (this.enemyHPs[i] && this.enemyHPs[i].current > 0) {
          this.enemySpeedBarTimers[i] = Math.max(0, this.enemySpeedBarTimers[i] - simDelta);
          this.updateEnemySpeedBar(i);

          if (this.enemySpeedBarTimers[i] <= 0 && !this._enemyAttackPending[i]) {
            this._enemyAttackPending[i] = true;
            this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1000;
            this.requestEnemyAttack(i);
          }
        }
      }

      // Tick spell cooldowns every ~200ms of sim time (scales with battle speed)
      this._cooldownTickAccum += simDelta;
      if (this._cooldownTickAccum >= 200) {
        const spellElapsed = this._cooldownTickAccum / 1000;
        this._cooldownTickAccum = 0;
        for (const id of Object.keys(this.spellCooldowns)) {
          this.spellCooldowns[id] = Math.max(0, this.spellCooldowns[id] - spellElapsed);
        }
        this.updateSpellCooldownVisuals();
        this.requestTickCooldowns(spellElapsed);
      }

      // Tick consumable cooldowns every ~200ms of REAL time (not battle-speed-scaled)
      this._consumableTickAccum += delta;
      if (this._consumableTickAccum >= 200) {
        const realElapsed = this._consumableTickAccum / 1000;
        this._consumableTickAccum = 0;
        for (const type of Object.keys(this.consumableCooldowns)) {
          this.consumableCooldowns[type] = Math.max(0, this.consumableCooldowns[type] - realElapsed);
        }
        this.updateConsumableCooldownVisuals();
        this.requestTickConsumableCooldowns(realElapsed);
      }

      // Update buff timer bars (real wall-clock time, not battle-speed)
      this.updateCanhaoTimerBar();
      this.updatePenaltyTimerBar();

      return; // Don't process pre-computed events
    } else if (!this.interactiveMode && this.isPlaying) {
      // Pre-computed (timed) mode: process events based on speed bar timing
      const scaledDelta = delta * this.battleSpeed;
      this.currentSimTime += scaledDelta;

      this.playerSpeedBarTimer -= scaledDelta;
      if (this.playerSpeedBarTimer <= 0 && this.currentEventIndex < this.eventsList.length) {
        this.playerSpeedBarTimer = this.playerActionTime * 1000;
        this.processNextEvent();
      }
      this.updatePlayerSpeedBar();

      for (let i = 0; i < this.enemyCount; i++) {
        this.enemySpeedBarTimers[i] -= scaledDelta;
        if (this.enemySpeedBarTimers[i] <= 0 && this.currentEventIndex < this.eventsList.length) {
          this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1000;
          this.processNextEvent();
        }
        this.updateEnemySpeedBar(i);
      }
    }

    // Idle animation (always runs)
    this.updateIdleAnimation();
  }

  /* ──────────────── Idle Animation ───────────────────────────────── */

  private updateIdleAnimation(): void {
    const t = this.idleAnimationTime;

    // Player
    if (this.playerSprite && this.playerIdleOffset && !this._playerAttacking) {
      const p = this.playerIdleOffset;
      this.playerSprite.y = p.baseY + Math.sin(t * 1.2 + p.phase) * p.bobAmplitude;
      this.playerSprite.x = p.baseX + Math.sin(t * 0.8 + p.phase + 1) * p.swayAmplitude;
    }
    // Always sync aura to wherever the player sprite currently is
    if (this.playerAura && this.playerSprite) {
      this.playerAura.x = this.playerSprite.x;
      this.playerAura.y = this.playerSprite.y;
      const pTime = performance.now() / 1000;
      this.playerAura.alpha = 0.65 + Math.sin(pTime * 1.2) * 0.15;
    }

    // Enemies
    for (let i = 0; i < this.enemySprites.length; i++) {
      if (this._enemyAttacking[i]) continue;
      const enemy = this.enemySprites[i];
      const offset = this.enemyIdleOffsets[i];
      if (!enemy || !offset) continue;

      const freqMod = offset.isAerial ? 1.5 : 1.0;
      enemy.y = offset.baseY + Math.sin(t * 1.2 * freqMod + offset.phase) * offset.bobAmplitude;
      enemy.x = offset.baseX + Math.sin(t * 0.8 * freqMod + offset.phase + 1) * offset.swayAmplitude;
    }
  }

  /* ──────────────── Speed Bar Updates ────────────────────────────── */

  private updatePlayerSpeedBar(): void {
    if (!this.playerSpeedBar) return;
    const maxMs = this.playerActionTime * 1000;
    const ratio = maxMs > 0 ? Math.max(0, Math.min(1, this.playerSpeedBarTimer / maxMs)) : 0;
    this.playerSpeedBar.bar.width = this.playerSpeedBar.maxWidth * ratio;
    if (this.playerSpeedBar.text) {
      const remaining = Math.max(0, this.playerSpeedBarTimer / 1000);
      this.playerSpeedBar.text.text = `${remaining.toFixed(1)}s`;
    }
  }

  private updateEnemySpeedBar(enemyIndex: number): void {
    const speedBarData = this.enemySpeedBars[enemyIndex];
    if (!speedBarData) return;
    const maxMs = this.enemyActionTimes[enemyIndex] * 1000;
    const ratio = maxMs > 0 ? Math.max(0, Math.min(1, this.enemySpeedBarTimers[enemyIndex] / maxMs)) : 0;
    speedBarData.bar.width = speedBarData.maxWidth * ratio;

    // Sync boss HUD speed bar
    if (this.bossSpeedBar && enemyIndex === 0) {
      this.bossSpeedBar.bar.width = this.bossSpeedBar.maxWidth * ratio;
      if (this.bossSpeedBar.text) {
        const remaining = Math.max(0, this.enemySpeedBarTimers[0] / 1000);
        this.bossSpeedBar.text.text = `${remaining.toFixed(1)}s`;
      }
    }
  }

  /* ──────────────── Buff Timer Bar Updates ────────────────────────── */

  private updateCanhaoTimerBar(): void {
    if (!this.canhaoTimerBar) return;
    const now = Date.now();
    const active = this.canhaoBuffExpiresAt != null && now < this.canhaoBuffExpiresAt;

    this.canhaoTimerBar.bar.visible = active;
    this.canhaoTimerBar.barBg.visible = active;
    this.canhaoTimerBar.label.visible = active;
    this.canhaoTimerBar.text.visible = active;

    if (!active) return;

    const remainingMs = this.canhaoBuffExpiresAt! - now;
    const ratio = Math.max(0, Math.min(1, remainingMs / this.canhaoBuffDurationMs));
    this.canhaoTimerBar.bar.width = this.canhaoTimerBar.maxWidth * ratio;

    // Colour shift: green→yellow→red as time depletes
    const r = ratio > 0.5 ? Math.round(255 * (1 - ratio) * 2) : 255;
    const g = ratio > 0.5 ? 255 : Math.round(255 * ratio * 2);
    this.canhaoTimerBar.bar.tint = (r << 16) | (g << 8) | 0x00;

    // Countdown text: "1:23" or "0:05"
    const totalSec = Math.max(0, Math.ceil(remainingMs / 1000));
    const min = Math.floor(totalSec / 60);
    const sec = totalSec % 60;
    this.canhaoTimerBar.text.text = `${min}:${sec.toString().padStart(2, '0')}`;

    // Pulse bar alpha when ≤ 15 seconds remain
    if (remainingMs <= 15000) {
      this.canhaoTimerBar.bar.alpha = 0.6 + 0.4 * Math.abs(Math.sin(now * 0.005));
    } else {
      this.canhaoTimerBar.bar.alpha = 1;
    }
  }

  private updatePenaltyTimerBar(): void {
    if (!this.penaltyTimerBar) return;
    const now = Date.now();
    const active = this.penaltyBuffExpiresAt != null && now < this.penaltyBuffExpiresAt;

    this.penaltyTimerBar.bar.visible = active;
    this.penaltyTimerBar.barBg.visible = active;
    this.penaltyTimerBar.label.visible = active;
    this.penaltyTimerBar.text.visible = active;

    if (!active) return;

    const remainingMs = this.penaltyBuffExpiresAt! - now;
    const ratio = Math.max(0, Math.min(1, remainingMs / this.penaltyBuffDurationMs));
    this.penaltyTimerBar.bar.width = this.penaltyTimerBar.maxWidth * ratio;

    // Colour shift: orange base, shifts greener as time runs out
    const r = 255;
    const g = Math.round(152 * ratio);
    this.penaltyTimerBar.bar.tint = (r << 16) | (g << 8) | 0x00;

    // Countdown text
    const totalSec = Math.max(0, Math.ceil(remainingMs / 1000));
    const min = Math.floor(totalSec / 60);
    const sec = totalSec % 60;
    this.penaltyTimerBar.text.text = `${min}:${sec.toString().padStart(2, '0')}`;

    // Pulse bar alpha when ≤ 15 seconds remain
    if (remainingMs <= 15000) {
      this.penaltyTimerBar.bar.alpha = 0.6 + 0.4 * Math.abs(Math.sin(now * 0.005));
    } else {
      this.penaltyTimerBar.bar.alpha = 1;
    }
  }

  /* ──────────────── Event Processing (pre-computed) ──────────────── */

  private processEvent(evt: BattleEvent): void {
    const evtType = getEventField<string>(evt, 'Type');
    switch (evtType) {
      case 'HPUpdate': this.handleHPUpdate(evt); break;
      case 'Attack': this.handleAttack(evt); break;
      case 'KO': this.handleKO(evt); break;
      case 'Victory': this.handleVictory(evt); break;
      case 'Draw': this.handleDraw(); break;
      case 'BattleStart': break;
    }
  }

  private processNextEvent(): void {
    if (this._destroyed || this.battleFinished || !this.isPlaying) return;
    if (this.currentEventIndex >= this.eventsList.length) {
      this.finishBattle();
      return;
    }
    const evt = this.eventsList[this.currentEventIndex];
    this.currentEventIndex++;
    this.processEvent(evt);
  }

  /* ──────────────── HP Updates ────────────────────────────────────── */

  private handleHPUpdate(evt: BattleEvent): void {
    const character = getEventField<string>(evt, 'Character') ?? '';
    const hp = getEventField<number>(evt, 'HP') ?? 0;
    const maxHP = getEventField<number>(evt, 'MaxHP');

    if (character === 'Attacker' || character === 'Player') {
      if (maxHP && maxHP > this.playerMaxHp) this.playerMaxHp = maxHP;
      this.playerCurrentHp = hp;
      this.updatePlayerHPBar();
    } else if (character.startsWith('Enemy')) {
      const idx = parseInt(character.replace('Enemy', ''));
      if (!isNaN(idx) && idx >= 0 && idx < this.enemyHpBars.length) {
        if (maxHP && maxHP > this.enemyHPs[idx].max) this.enemyHPs[idx].max = maxHP;
        this.enemyHPs[idx].current = hp;
        this.updateIndividualEnemyHPBar(idx);
      }
    } else {
      // "Defender" single-enemy fallback
      if (maxHP) {
        this.enemyMaxHp = maxHP;
        if (this.enemyHPs[0]) this.enemyHPs[0].max = maxHP;
      }
      this.enemyCurrentHp = hp;
      if (this.enemyHPs[0]) this.enemyHPs[0].current = hp;
      this.updateEnemyHPBar();
    }
  }

  private updatePlayerHPBar(): void {
    if (!this.playerHpBar) return;
    const ratio = Math.min(1, Math.max(0, this.playerCurrentHp / this.playerMaxHp));
    const maxWidth = this.playerHpBar.maxWidth;
    const barHeight = this.playerHpBar.barHeight;
    const radius = barHeight / 2;

    const fillColor = ratio > 0.5 ? 0x4caf50 : ratio > 0.25 ? 0xff9800 : 0xf44336;
    this.playerHpBar.bar.clear();
    this.playerHpBar.bar.roundRect(0, 0, maxWidth, barHeight, radius);
    this.playerHpBar.bar.fill(fillColor);
    animateTo(this, this.playerHpBar.bar, { width: maxWidth * ratio }, 200);
    this.playerHpBar.text.text = `${formatNum(Math.max(0, this.playerCurrentHp))} / ${formatNum(this.playerMaxHp)} HP`;
  }

  private updateIndividualEnemyHPBar(enemyIndex: number): void {
    const enemyHP = this.enemyHPs[enemyIndex];
    const hpBarData = this.enemyHpBars[enemyIndex];
    if (!enemyHP || !hpBarData) return;

    const ratio = Math.max(0, enemyHP.current / enemyHP.max);
    const newWidth = hpBarData.maxWidth * ratio;
    animateTo(this, hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
    hpBarData.text.text = `${formatNum(Math.max(0, Math.round(enemyHP.current)))}/${formatNum(Math.round(enemyHP.max))}`;

    // Sync boss HUD bar
    if (this.bossHpBar && enemyIndex === 0) {
      const maxW = this.bossHpBar.maxWidth;
      const bh = this.bossHpBar.barHeight;
      const r = bh / 2;
      const fillColor = ratio > 0.5 ? 0xf44336 : ratio > 0.25 ? 0xd32f2f : 0xb71c1c;
      this.bossHpBar.bar.clear();
      this.bossHpBar.bar.roundRect(0, 0, maxW, bh, r);
      this.bossHpBar.bar.fill(fillColor);
      animateTo(this, this.bossHpBar.bar, { width: maxW * ratio }, 200);
      this.bossHpBar.text.text = `${formatNum(Math.max(0, Math.round(enemyHP.current)))} / ${formatNum(Math.round(enemyHP.max))} HP`;
    }
  }

  private updateEnemyHPBar(): void {
    const ratio = Math.max(0, this.enemyCurrentHp / this.enemyMaxHp);

    for (const hpBarData of this.enemyHpBars) {
      const newWidth = hpBarData.maxWidth * ratio;
      animateTo(this, hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
      hpBarData.text.text = `${formatNum(Math.max(0, Math.round(this.enemyCurrentHp / this.enemyCount)))}/${formatNum(Math.round(this.enemyMaxHp / this.enemyCount))}`;
    }

    // Sync boss HUD bar
    if (this.bossHpBar) {
      const maxW = this.bossHpBar.maxWidth;
      const bh = this.bossHpBar.barHeight;
      const r = bh / 2;
      const fillColor = ratio > 0.5 ? 0xf44336 : ratio > 0.25 ? 0xd32f2f : 0xb71c1c;
      this.bossHpBar.bar.clear();
      this.bossHpBar.bar.roundRect(0, 0, maxW, bh, r);
      this.bossHpBar.bar.fill(fillColor);
      animateTo(this, this.bossHpBar.bar, { width: maxW * ratio }, 200);
      this.bossHpBar.text.text = `${formatNum(Math.max(0, Math.round(this.enemyCurrentHp)))} / ${formatNum(Math.round(this.enemyMaxHp))} HP`;
    }
  }

  /* ──────────────── Attack Handling ───────────────────────────────── */

  private handleAttack(evt: BattleEvent): void {
    const attacker = getEventField<string>(evt, 'Attacker') ?? '';
    const defender = getEventField<string>(evt, 'Defender') ?? '';
    const damage = getEventField<number>(evt, 'Damage') ?? 0;
    const isCritical = getEventField<boolean>(evt, 'IsCritical') ?? false;
    const isBlocked = getEventField<boolean>(evt, 'IsBlocked') ?? false;
    const isDodged = getEventField<boolean>(evt, 'IsDodged') ?? false;
    const isBoosted = getEventField<boolean>(evt, 'IsBoosted') ?? false;

    if (attacker === 'Attacker' || attacker === 'Player') {
      this.animatePlayerAttack(isCritical);

      if (defender?.startsWith('Enemy')) {
        const enemyIndex = parseInt(defender.replace('Enemy', ''));
        this.flashEnemy(enemyIndex, isCritical);
        const enemy = this.enemySprites[enemyIndex];
        if (enemy && damage > 0) {
          const yOff = enemy.y - (enemy.height || 40) * 0.8;
          if (isBlocked) {
            showFloatingText(this, 'BLOCKED', enemy.x, yOff, 0x00e5ff);
          } else {
            showDamageText(this, damage, isCritical, enemy.x, yOff);
          }
        }
        if (isBoosted && enemy) {
          showFloatingText(this, 'EXTRA', enemy.x, enemy.y - (enemy.height || 40) * 0.8 - 25, 0xff9800);
        }
      } else {
        this.flashEnemies(isCritical);
        const enemy = this.enemySprites[0];
        if (enemy && damage > 0) {
          const yOff = enemy.y - (enemy.height || 40) * 0.8;
          if (isBlocked) {
            showFloatingText(this, 'BLOCKED', enemy.x, yOff, 0x00e5ff);
          } else {
            showDamageText(this, damage, isCritical, enemy.x, yOff);
          }
        }
        if (isBoosted && enemy) {
          showFloatingText(this, 'EXTRA', enemy.x, enemy.y - (enemy.height || 40) * 0.8 - 25, 0xff9800);
        }
      }
    } else if (attacker.startsWith('Enemy')) {
      const enemyIndex = parseInt(attacker.replace('Enemy', ''));
      this.animateSingleEnemyAttack(enemyIndex, isCritical);
      if (this.playerSprite) {
        const yOff = this.playerSprite.y - (this.playerSprite.height || 40) * 0.8;
        if (isBlocked || isDodged) {
          showFloatingText(this, isDodged ? 'DODGE' : 'BLOCKED', this.playerSprite.x, yOff, 0x00e5ff);
        } else {
          this.flashPlayer(isCritical);
          if (damage > 0) showDamageText(this, damage, isCritical, this.playerSprite.x, yOff);
        }
      }
    } else {
      // Generic enemy attacker
      this.animateEnemyAttack(isCritical);
      if (this.playerSprite) {
        const yOff = this.playerSprite.y - (this.playerSprite.height || 40) * 0.8;
        if (isBlocked || isDodged) {
          showFloatingText(this, isDodged ? 'DODGE' : 'BLOCKED', this.playerSprite.x, yOff, 0x00e5ff);
        } else {
          this.flashPlayer(isCritical);
          if (damage > 0) showDamageText(this, damage, isCritical, this.playerSprite.x, yOff);
        }
      }
    }

    this._playSound((isBlocked || isDodged) ? 'block' : (isCritical ? 'critical' : 'attack'));
  }

  /* ──────────────── Attack Animations ─────────────────────────────── */

  private animatePlayerAttack(isCritical: boolean): void {
    if (!this.playerSprite) return;
    this._playerAttacking = true;
    const lungeDistance = isCritical ? 80 : 60;
    const lungeDuration = isCritical ? 120 : 150;

    // Use idle-offset base position to prevent drift at high battle speeds
    const baseX = this.playerIdleOffset?.baseX ?? this.playerSprite.x;
    const baseY = this.playerIdleOffset?.baseY ?? this.playerSprite.y;
    const aura = this.playerAura;

    if (this.isMobile) {
      animateTo(this, this.playerSprite, { y: baseY - lungeDistance }, lungeDuration, () => {
        animateTo(this, this.playerSprite!, { y: baseY }, 240, () => {
          this._playerAttacking = false;
        });
        if (aura) animateTo(this, aura, { y: baseY }, 240);
      });
      if (aura) animateTo(this, aura, { y: baseY - lungeDistance }, lungeDuration);
    } else {
      animateTo(this, this.playerSprite, { x: baseX + lungeDistance }, lungeDuration, () => {
        animateTo(this, this.playerSprite!, { x: baseX }, 240, () => {
          this._playerAttacking = false;
        });
        if (aura) animateTo(this, aura, { x: baseX }, 240);
      });
      if (aura) animateTo(this, aura, { x: baseX + lungeDistance }, lungeDuration);
    }
  }

  private animateEnemyAttack(isCritical: boolean): void {
    for (let index = 0; index < this.enemySprites.length; index++) {
      const enemy = this.enemySprites[index];
      this._enemyAttacking[index] = true;
      const lungeDistance = isCritical ? 80 : 60;
      const lungeDuration = isCritical ? 120 : 150;
      // Use idle-offset base position to prevent drift at high battle speeds
      const eBaseX = this.enemyIdleOffsets[index]?.baseX ?? enemy.x;
      const eBaseY = this.enemyIdleOffsets[index]?.baseY ?? enemy.y;
      const id = setTimeout(() => {
        if (this.isMobile) {
          animateTo(this, enemy, { y: eBaseY + lungeDistance }, lungeDuration, () => {
            animateTo(this, enemy, { y: eBaseY }, 240, () => {
              this._enemyAttacking[index] = false;
            });
          });
        } else {
          animateTo(this, enemy, { x: eBaseX - lungeDistance }, lungeDuration, () => {
            animateTo(this, enemy, { x: eBaseX }, 240, () => {
              this._enemyAttacking[index] = false;
            });
          });
        }
      }, (index * 50) / this.battleSpeed) as unknown as number;
      this._timeoutIds.push(id);
    }
  }

  private animateSingleEnemyAttack(enemyIndex: number, isCritical: boolean): void {
    if (enemyIndex < 0 || enemyIndex >= this.enemySprites.length) return;
    const enemy = this.enemySprites[enemyIndex];
    if (!enemy) return;
    this._enemyAttacking[enemyIndex] = true;
    const lungeDistance = isCritical ? 80 : 60;
    const lungeDuration = isCritical ? 120 : 150;

    // Use idle-offset base position to prevent drift at high battle speeds
    const eBaseX = this.enemyIdleOffsets[enemyIndex]?.baseX ?? enemy.x;
    const eBaseY = this.enemyIdleOffsets[enemyIndex]?.baseY ?? enemy.y;

    if (this.isMobile) {
      animateTo(this, enemy, { y: eBaseY + lungeDistance }, lungeDuration, () => {
        animateTo(this, enemy, { y: eBaseY }, 240, () => {
          this._enemyAttacking[enemyIndex] = false;
        });
      });
    } else {
      animateTo(this, enemy, { x: eBaseX - lungeDistance }, lungeDuration, () => {
        animateTo(this, enemy, { x: eBaseX }, 240, () => {
          this._enemyAttacking[enemyIndex] = false;
        });
      });
    }
  }

  /* ──────────────── Flash Effects ─────────────────────────────────── */

  private flashPlayer(isCritical: boolean): void {
    if (!this.playerSprite) return;
    this.playerSprite.tint = isCritical ? 0xcc0000 : 0xff0000;
    const flashDuration = isCritical ? 180 : 100;
    const id = setTimeout(() => {
      if (this.playerSprite) this.playerSprite.tint = 0xffffff;
    }, flashDuration / this.battleSpeed) as unknown as number;
    this._timeoutIds.push(id);
  }

  private flashEnemy(enemyIndex: number, isCritical: boolean): void {
    if (enemyIndex < 0 || enemyIndex >= this.enemySprites.length) return;
    const enemy = this.enemySprites[enemyIndex];
    if (!enemy) return;
    enemy.tint = isCritical ? 0xcc0000 : 0xff0000;
    const flashDuration = isCritical ? 180 : 100;
    const id = setTimeout(() => {
      enemy.tint = 0xffffff;
    }, flashDuration / this.battleSpeed) as unknown as number;
    this._timeoutIds.push(id);
  }

  private flashEnemies(isCritical: boolean): void {
    for (const enemy of this.enemySprites) {
      enemy.tint = isCritical ? 0xcc0000 : 0xff0000;
      const flashDuration = isCritical ? 180 : 100;
      const id = setTimeout(() => {
        enemy.tint = 0xffffff;
      }, flashDuration / this.battleSpeed) as unknown as number;
      this._timeoutIds.push(id);
    }
  }

  /* ──────────────── KO / Victory / Draw ──────────────────────────── */

  private handleKO(evt: BattleEvent): void {
    const character = getEventField<string>(evt, 'Character') ?? '';
    this._playSound('ko');

    if (character === 'Attacker' || character === 'Player') {
      if (this.playerSprite) {
        animateTo(this, this.playerSprite, { alpha: 0.3, rotation: Math.PI / 2 }, 500);
        if (this.playerAura) animateTo(this, this.playerAura, { alpha: 0 }, 500);
      }
    } else if (character.startsWith('Enemy')) {
      const idx = parseInt(character.replace('Enemy', ''));
      if (!isNaN(idx) && idx >= 0 && idx < this.enemySprites.length) {
        const enemy = this.enemySprites[idx];
        animateTo(this, enemy, { alpha: 0, y: enemy.y - 50 }, 500);

        // Hide HP bar
        const hpBar = this.enemyHpBars[idx];
        if (hpBar) {
          animateTo(this, hpBar.bar, { alpha: 0 }, 300);
          animateTo(this, hpBar.barBg, { alpha: 0 }, 300);
          animateTo(this, hpBar.text, { alpha: 0 }, 300);
        }
        // Hide speed bar
        const speedBar = this.enemySpeedBars[idx];
        if (speedBar) {
          animateTo(this, speedBar.bar, { alpha: 0 }, 300);
          animateTo(this, speedBar.barBg, { alpha: 0 }, 300);
        }
        // Fade boss HUD on boss KO
        this.fadeBossHudBars(idx);
      }
    } else {
      // Generic KO — all enemies
      for (let i = 0; i < this.enemySprites.length; i++) {
        const enemy = this.enemySprites[i];
        animateTo(this, enemy, { alpha: 0, y: enemy.y - 50 }, 500);
        const hpBar = this.enemyHpBars[i];
        if (hpBar) {
          animateTo(this, hpBar.bar, { alpha: 0 }, 300);
          animateTo(this, hpBar.barBg, { alpha: 0 }, 300);
          animateTo(this, hpBar.text, { alpha: 0 }, 300);
        }
        const speedBar = this.enemySpeedBars[i];
        if (speedBar) {
          animateTo(this, speedBar.bar, { alpha: 0 }, 300);
          animateTo(this, speedBar.barBg, { alpha: 0 }, 300);
        }
      }
      this.fadeBossHudBars(0);
    }
  }

  private fadeBossHudBars(enemyIndex: number): void {
    if (enemyIndex !== 0) return;
    if (this.bossHpBar) {
      animateTo(this, this.bossHpBar.bar, { alpha: 0 }, 300);
      animateTo(this, this.bossHpBar.barBg, { alpha: 0 }, 300);
      if (this.bossHpBar.border) animateTo(this, this.bossHpBar.border, { alpha: 0 }, 300);
      animateTo(this, this.bossHpBar.text, { alpha: 0 }, 300);
    }
    if (this.bossSpeedBar) {
      animateTo(this, this.bossSpeedBar.bar, { alpha: 0 }, 300);
      animateTo(this, this.bossSpeedBar.barBg, { alpha: 0 }, 300);
      if (this.bossSpeedBar.text) animateTo(this, this.bossSpeedBar.text, { alpha: 0 }, 300);
    }
  }

  private handleVictory(evt: BattleEvent): void {
    const winner = getEventField<string>(evt, 'Winner') ?? '';
    const isPlayerWin = winner === 'Attacker' || winner === 'Player';

    if (isPlayerWin) {
      this._playSound('victory');
      if (this.playerSprite) {
        const originalY = this.playerSprite.y;
        animateTo(this, this.playerSprite, { y: originalY - 20 }, 200, () => {
          animateTo(this, this.playerSprite!, { y: originalY }, 200, () => {
            animateTo(this, this.playerSprite!, { y: originalY - 20 }, 200, () => {
              animateTo(this, this.playerSprite!, { y: originalY }, 200);
            });
          });
        });
      }
    } else {
      this._playSound('defeat');
    }

    // Show big text only on boss stages or defeat
    const isBossStage = this.enemyType === 'boss' || (this.stageNumber % 10 === 0);
    if (isBossStage || !isPlayerWin) {
      if (this.app && this.stage) {
        const resultText = new PIXI.Text({
          text: isPlayerWin ? 'VICTORY!' : 'DEFEAT',
          style: {
            fontSize: 48, fontFamily: 'Arial, sans-serif', fontWeight: 'bold',
            fill: isPlayerWin ? 0x44ff44 : 0xff4444,
            stroke: { color: 0x000000, width: 6 },
          },
        });
        resultText.anchor.set(0.5);
        resultText.x = this.app.screen.width / 2;
        resultText.y = this.app.screen.height / 2;
        resultText.scale.set(0);
        this.stage.addChild(resultText);
        animateTo(this, resultText, { scale: 1 }, 500);
      }
    }

    const finishDelay = (isBossStage || !isPlayerWin) ? 800 : 400;
    const id = setTimeout(() => this.finishBattle(), finishDelay / this.battleSpeed) as unknown as number;
    this._timeoutIds.push(id);
  }

  private handleDraw(): void {
    if (!this.app || !this.stage) return;
    const drawText = new PIXI.Text({
      text: 'DRAW',
      style: {
        fontSize: 48, fontFamily: 'Arial, sans-serif', fontWeight: 'bold',
        fill: 0xffaa00, stroke: { color: 0x000000, width: 6 },
      },
    });
    drawText.anchor.set(0.5);
    drawText.x = this.app.screen.width / 2;
    drawText.y = this.app.screen.height / 2;
    this.stage.addChild(drawText);

    const id = setTimeout(() => this.finishBattle(), 800 / this.battleSpeed) as unknown as number;
    this._timeoutIds.push(id);
  }

  /* ──────────────── Finish Battle ─────────────────────────────────── */

  private finishBattle(): void {
    if (this._destroyed || this.battleFinished) return;
    this.battleFinished = true;
    this.isPlaying = false;

    if (this.eventTimer) {
      clearInterval(this.eventTimer);
      this.eventTimer = null;
    }

    if (this.dotNetRef) {
      try {
        this.dotNetRef.invokeMethodAsync('OnBattleFinished').catch((e: unknown) => {
          console.warn('Could not notify Blazor of battle finish:', e);
        });
      } catch (e) {
        console.warn('finishBattle: dotNetRef error:', (e as Error).message);
      }
    }
  }

  /* ──────────────── Public API ────────────────────────────────────── */

  setSpeed(speed: number): void {
    const validSpeed = speed === 5 ? 5 : 1;
    this.playbackSpeed = validSpeed;
    this._battleSpeed = validSpeed;
  }

  setAudioEnabled(enabled: boolean): void {
    this.audioEnabled = enabled;
    globalAudioEnabled = enabled;
    setMusicVolume(musicState, 0.3, enabled);
  }

  /* ──────────────── Destroy ──────────────────────────────────────── */

  destroy(): void {
    this._destroyed = true;
    this.battleFinished = true;
    this.isPlaying = false;

    // Remove event listeners
    if (this._onResize) {
      window.removeEventListener('resize', this._onResize);
      this._onResize = null;
    }
    if (this._onContextLost && this.app?.canvas) {
      this.app.canvas.removeEventListener('webglcontextlost', this._onContextLost);
    }
    if (this._onContextRestored && this.app?.canvas) {
      this.app.canvas.removeEventListener('webglcontextrestored', this._onContextRestored);
    }

    // Clear pending timers
    for (const id of this._timeoutIds) clearTimeout(id);
    for (const id of this._rafIds) cancelAnimationFrame(id);
    this._timeoutIds = [];
    this._rafIds = [];

    // Destroy pooled texts
    destroyTextPool(this._textPool);

    // Clean up spell bar
    this.spellButtons = [];
    this.spellBarContainer = null;

    if (this.eventTimer) {
      clearInterval(this.eventTimer);
      this.eventTimer = null;
    }

    if (!this.app) return;

    try { this.app.ticker.stop(); } catch { /* ignore */ }

    // Clear stage
    try {
      if (this.stage?.children) {
        while (this.stage.children.length > 0) {
          const child = this.stage.children[0];
          this.stage.removeChild(child);
          if ((child as { destroy?: Function }).destroy) {
            try { (child as Container).destroy({ children: true, texture: false }); } catch { /* ignore */ }
          }
        }
      }
    } catch { /* ignore */ }

    try {
      this.app.destroy(false);
    } catch (e) {
      console.warn('PixiJS app.destroy error (safe to ignore):', (e as Error).message);
    }
    this.app = null;
    this.stage = null;
  }

  /* ──────────────── Reset For Next Battle ─────────────────────────── */

  async resetForNextBattle(data: Record<string, unknown>): Promise<void> {
    if (this._destroyed || !this.app || !this.stage) {
      console.warn('resetForNextBattle: app/stage destroyed, skipping');
      return;
    }

    // Stop current battle
    this.isPlaying = false;
    this.battleFinished = true;

    // Update battle data
    this.eventsList = (data.events as BattleEvent[]) ?? [];
    this.dotNetRef = (data.dotNetRef as DotNet.DotNetObject | null) ?? this.dotNetRef;
    this.stageNumber = pick<number>(data, 'StageNumber', 'stageNumber', this.stageNumber + 1);
    this.enemyType = pick<string>(data, 'EnemyType', 'enemyType', 'Normal');
    this.enemyCount = pick<number>(data, 'EnemyCount', 'enemyCount', 1);
    this.playerName = pick<string>(data, 'PlayerName', 'playerName', this.playerName);
    this.enemyName = pick<string>(data, 'EnemyName', 'enemyName', this.enemyName);
    this.backgroundPath = pick<string>(data, 'BackgroundPath', 'backgroundPath', this.backgroundPath);
    this.playerSpritePath = pick<string>(data, 'PlayerSpritePath', 'playerSpritePath', this.playerSpritePath);
    this.enemySpritePaths = (data.enemySprites ?? data.EnemySprites ?? []) as string[];
    this.enemyPlacements = (data.enemyPlacements ?? data.EnemyPlacements ?? Array(this.enemyCount).fill(0)) as number[];
    this.hasShotBuff = pick<boolean>(data, 'HasShotBuff', 'hasShotBuff', this.hasShotBuff);
    this.hasPenaltyBuff = pick<boolean>(data, 'HasPenaltyBuff', 'hasPenaltyBuff', false);

    // Interactive mode
    this.interactiveMode = pick<boolean>(data, 'InteractiveMode', 'interactiveMode', this.interactiveMode);
    this.spells = (data.spells ?? data.Spells ?? this.spells) as SpellDefinition[];
    this.interactivePlayerHP = (data.playerHP ?? data.PlayerHP ?? null) as number | null;
    this.interactivePlayerMaxHP = (data.playerMaxHP ?? data.PlayerMaxHP ?? null) as number | null;
    this.interactivePlayerActionTime = (data.playerActionTime ?? data.PlayerActionTime ?? null) as number | null;
    this.interactiveEnemies = (data.enemies ?? data.Enemies ?? []) as EnemyDefinition[];
    this._enemyAttackPending = Array(this.enemyCount).fill(false);

    // Consumable quantities
    const cData = (data.consumables ?? data.Consumables) as Record<string, unknown> | undefined;
    if (cData) {
      this.consumableQuantities = {
        fino: pick<number>(cData, 'Fino', 'fino', this.consumableQuantities.fino),
        caneca: pick<number>(cData, 'Caneca', 'caneca', this.consumableQuantities.caneca),
        cigarro: pick<number>(cData, 'Cigarro', 'cigarro', this.consumableQuantities.cigarro),
        canhao: pick<number>(cData, 'Canhao', 'canhao', this.consumableQuantities.canhao),
        shot: pick<number>(cData, 'Shot', 'shot', this.consumableQuantities.shot),
        penalty: pick<number>(cData, 'Penalty', 'penalty', this.consumableQuantities.penalty),
      };
    }

    // Consumable images
    const ciData = (data.consumableImages ?? data.ConsumableImages) as Record<string, unknown> | undefined;
    if (ciData) {
      this.consumableImages = {
        fino: pick<string>(ciData, 'Fino', 'fino', this.consumableImages.fino),
        caneca: pick<string>(ciData, 'Caneca', 'caneca', this.consumableImages.caneca),
        cigarro: pick<string>(ciData, 'Cigarro', 'cigarro', this.consumableImages.cigarro),
        canhao: pick<string>(ciData, 'Canhao', 'canhao', this.consumableImages.canhao),
      };
    }

    // Active buffs
    const abData = (data.activeBuffs ?? data.ActiveBuffs) as Record<string, unknown> | undefined;
    if (abData) {
      this.activeBuffs = {
        cigarro: !!pick<boolean>(abData, 'Cigarro', 'cigarro', false),
        canhao: !!pick<boolean>(abData, 'Canhao', 'canhao', false),
        shot: !!pick<boolean>(abData, 'Shot', 'shot', false),
        penalty: !!pick<boolean>(abData, 'Penalty', 'penalty', false),
      };
    }

    // Canhao / Penalty buff expiry (UTC timestamp from server)
    const canhaoUtcReset = (data.canhaoBuffExpiresAtUtc ?? data.CanhaoBuffExpiresAtUtc ?? null) as string | null;
    if (canhaoUtcReset) {
      this.canhaoBuffExpiresAt = new Date(canhaoUtcReset).getTime();
    } else if (!(this.canhaoBuffExpiresAt && Date.now() < this.canhaoBuffExpiresAt)) {
      this.canhaoBuffExpiresAt = null;
    }
    const penaltyUtcReset = (data.penaltyBuffExpiresAtUtc ?? data.PenaltyBuffExpiresAtUtc ?? null) as string | null;
    if (penaltyUtcReset) {
      this.penaltyBuffExpiresAt = new Date(penaltyUtcReset).getTime();
    } else if (!(this.penaltyBuffExpiresAt && Date.now() < this.penaltyBuffExpiresAt)) {
      this.penaltyBuffExpiresAt = null;
    }

    // Consumable cooldowns
    const ccData = (data.consumableCooldowns ?? data.ConsumableCooldowns) as Record<string, number> | undefined;
    if (ccData) {
      for (const [type, remaining] of Object.entries(ccData)) {
        this.consumableCooldowns[type] = remaining;
      }
    }

    // Spell cooldowns
    const scData = (data.spellCooldowns ?? data.SpellCooldowns) as Record<string, number> | undefined;
    if (scData) {
      for (const [id, remaining] of Object.entries(scData)) {
        this.spellCooldowns[id] = remaining;
      }
    }

    // Pre-load new textures while old scene is visible
    try {
      await this.loadAssets();
    } catch (e) {
      console.error('Failed to load assets for next stage:', e);
    }

    if (this._destroyed || !this.app || !this.stage) {
      console.warn('resetForNextBattle: destroyed during asset load');
      return;
    }

    // Reset battle state
    this.currentEventIndex = 0;
    this.battleFinished = false;
    this.playerMaxHp = 100;
    this.playerCurrentHp = 100;
    this.playerActionTime = 3.5;
    this.enemyHPs = Array.from({ length: this.enemyCount }, () => ({ current: 100, max: 100 }));
    this.idleAnimationTime = 0;
    this.enemyIdleOffsets = [];

    // Build persistent set (keep player, bars, background, consumable/spell bars)
    const persistent = new Set<Container>();
    if (this.backgroundSprite) persistent.add(this.backgroundSprite);
    if (this._overlay) persistent.add(this._overlay);
    if (this._ground) persistent.add(this._ground);
    if (this.playerSprite) persistent.add(this.playerSprite);
    if (this.playerAura) persistent.add(this.playerAura);
    if (this.playerHpBar) {
      persistent.add(this.playerHpBar.bar);
      persistent.add(this.playerHpBar.barBg);
      persistent.add(this.playerHpBar.text);
      if (this.playerHpBar.border) persistent.add(this.playerHpBar.border);
    }
    if (this.playerSpeedBar) {
      persistent.add(this.playerSpeedBar.bar);
      persistent.add(this.playerSpeedBar.barBg);
      if (this.playerSpeedBar.border) persistent.add(this.playerSpeedBar.border);
      if (this.playerSpeedBar.text) persistent.add(this.playerSpeedBar.text);
    }
    if (this.bossHpBar) {
      persistent.add(this.bossHpBar.bar);
      persistent.add(this.bossHpBar.barBg);
      if (this.bossHpBar.border) persistent.add(this.bossHpBar.border);
      persistent.add(this.bossHpBar.text);
    }
    if (this.bossSpeedBar) {
      persistent.add(this.bossSpeedBar.bar);
      persistent.add(this.bossSpeedBar.barBg);
      if (this.bossSpeedBar.text) persistent.add(this.bossSpeedBar.text);
    }
    if (this.consumableBarContainer) persistent.add(this.consumableBarContainer);
    if (this.spellBarContainer) persistent.add(this.spellBarContainer);
    if (this.canhaoTimerBar) {
      persistent.add(this.canhaoTimerBar.bar);
      persistent.add(this.canhaoTimerBar.barBg);
      persistent.add(this.canhaoTimerBar.label);
      persistent.add(this.canhaoTimerBar.text);
    }
    if (this.penaltyTimerBar) {
      persistent.add(this.penaltyTimerBar.bar);
      persistent.add(this.penaltyTimerBar.barBg);
      persistent.add(this.penaltyTimerBar.label);
      persistent.add(this.penaltyTimerBar.text);
    }

    // Remove non-persistent children
    if (!this.stage) return;
    const toRemove: Container[] = [];
    try {
      for (const child of [...this.stage.children]) {
        if (!persistent.has(child)) toRemove.push(child);
      }
    } catch (e) {
      console.warn('resetForNextBattle: error iterating stage:', (e as Error).message);
      return;
    }
    for (const child of toRemove) {
      try {
        this.stage.removeChild(child);
        if ((child as { destroy?: Function }).destroy) {
          (child as Container).destroy({ children: true, texture: false });
        }
      } catch { /* ignore */ }
    }

    // Reset player visual state
    if (this.playerSprite) {
      this.playerSprite.alpha = 1;
      this.playerSprite.rotation = 0;
      this.playerSprite.tint = 0xffffff;
      if (this.playerIdleOffset) {
        this.playerSprite.x = this.playerIdleOffset.baseX;
        this.playerSprite.y = this.playerIdleOffset.baseY;
      }
    }

    // Reset player HP bar
    if (this.playerHpBar) {
      this.playerHpBar.bar.width = this.playerHpBar.maxWidth;
      this.playerHpBar.text.text = '100/100';
    }

    // Reset player speed bar
    if (this.playerSpeedBar) {
      this.playerSpeedBar.bar.width = this.playerSpeedBar.maxWidth;
    }

    // Reset boss HUD bars
    if (this.bossHpBar) {
      const bh = this.bossHpBar.barHeight;
      const r = bh / 2;
      this.bossHpBar.bar.clear();
      this.bossHpBar.bar.roundRect(0, 0, this.bossHpBar.maxWidth, bh, r);
      this.bossHpBar.bar.fill(0xf44336);
      this.bossHpBar.bar.width = this.bossHpBar.maxWidth;
      this.bossHpBar.bar.alpha = 1;
      this.bossHpBar.barBg.alpha = 1;
      if (this.bossHpBar.border) this.bossHpBar.border.alpha = 1;
      this.bossHpBar.text.alpha = 1;
      this.bossHpBar.text.text = '100/100';
    }
    if (this.bossSpeedBar) {
      this.bossSpeedBar.bar.width = this.bossSpeedBar.maxWidth;
      this.bossSpeedBar.bar.alpha = 1;
      this.bossSpeedBar.barBg.alpha = 1;
      if (this.bossSpeedBar.text) this.bossSpeedBar.text.alpha = 1;
    }

    // Handle shot buff aura changes — blue glow outline (match CSS home page look)
    if (this.hasShotBuff && !this.playerAura) {
      if (this.playerSprite && this.stage) {
        this.playerAura = PIXI.Sprite.from(this._playerAlias);
        this.playerAura.anchor.set(0.5, 1);
        this.playerAura.scale.set(this.playerSprite.scale.x * 1.25);
        this.playerAura.x = this.playerSprite.x;
        this.playerAura.y = this.playerSprite.y;
        this.playerAura.alpha = 0.8;
        const cm = new PIXI.ColorMatrixFilter();
        cm.matrix = [
          0, 0, 0, 0, 0,
          0, 0, 0, 0, 0.667,
          0, 0, 0, 0, 1,
          0, 0, 0, 1, 0,
        ];
        this.playerAura.filters = [cm, new PIXI.BlurFilter({ strength: 12 })];
        const playerIdx = this.stage.getChildIndex(this.playerSprite);
        this.stage.addChildAt(this.playerAura, playerIdx);
      }
    } else if (!this.hasShotBuff && this.playerAura) {
      this.playerAura.visible = false;
    } else if (this.hasShotBuff && this.playerAura) {
      this.playerAura.visible = true;
      this.playerAura.alpha = 0.8;
    }

    // Update background texture if changed
    if (this.backgroundSprite) {
      const newBgTexture = PIXI.Assets.get(this.bgAlias);
      if (newBgTexture && this.backgroundSprite.texture !== newBgTexture) {
        this.backgroundSprite.texture = newBgTexture;
      }
    }

    // Reset enemy arrays
    this.enemySprites = [];
    this.enemyHpBars = [];
    this.enemySpeedBars = [];
    this.enemySpeedBarTimers = Array(this.enemyCount).fill(3500);
    this.enemyActionTimes = Array(this.enemyCount).fill(3.5);

    // Reset attack animation flags
    this._playerAttacking = false;
    this._enemyAttacking = {};

    // Create new enemies
    const width = this.app.screen.width;
    const height = this.app.screen.height;
    this.isMobile = width <= height || width < 500;
    this.createEnemies(width, height);

    // Boss HUD bar management
    const isBossNow = this.enemyType?.toLowerCase() === 'boss';
    if (isBossNow) {
      if (!this.bossHpBar) this.createBossHudBars(width, height);
      if (this.bossHpBar) {
        this.bossHpBar.bar.visible = true;
        this.bossHpBar.barBg.visible = true;
        if (this.bossHpBar.border) this.bossHpBar.border.visible = true;
        this.bossHpBar.text.visible = true;
      }
      if (this.bossSpeedBar) {
        this.bossSpeedBar.bar.visible = true;
        this.bossSpeedBar.barBg.visible = true;
        if (this.bossSpeedBar.text) this.bossSpeedBar.text.visible = true;
      }
    } else {
      if (this.bossHpBar) {
        this.bossHpBar.bar.visible = false;
        this.bossHpBar.barBg.visible = false;
        if (this.bossHpBar.border) this.bossHpBar.border.visible = false;
        this.bossHpBar.text.visible = false;
      }
      if (this.bossSpeedBar) {
        this.bossSpeedBar.bar.visible = false;
        this.bossSpeedBar.barBg.visible = false;
        if (this.bossSpeedBar.text) this.bossSpeedBar.text.visible = false;
      }
    }

    // Fade in new enemies
    for (const enemy of this.enemySprites) {
      if (enemy) {
        enemy.alpha = 0;
        animateTo(this, enemy, { alpha: 1 }, 250);
      }
    }
    for (const hpBar of this.enemyHpBars) {
      if (hpBar) {
        if (hpBar.bar) { hpBar.bar.alpha = 0; animateTo(this, hpBar.bar, { alpha: 1 }, 250); }
        if (hpBar.barBg) { hpBar.barBg.alpha = 0; animateTo(this, hpBar.barBg, { alpha: 1 }, 250); }
        if (hpBar.text) { hpBar.text.alpha = 0; animateTo(this, hpBar.text, { alpha: 1 }, 250); }
      }
    }
    for (const speedBar of this.enemySpeedBars) {
      if (speedBar) {
        if (speedBar.bar) { speedBar.bar.alpha = 0; animateTo(this, speedBar.bar, { alpha: 1 }, 250); }
        if (speedBar.barBg) { speedBar.barBg.alpha = 0; animateTo(this, speedBar.barBg, { alpha: 1 }, 250); }
      }
    }

    // Final _destroyed guard — destroy() may have run while we were
    // synchronously rebuilding. If so, abort before starting the ticker loop.
    if (this._destroyed || !this.app || !this.stage) return;

    // Restart battle
    if (this.interactiveMode) {
      this.initInteractiveState();
      if (this.spells?.length > 0) {
        if (this.spellBarContainer) {
          this.spellBarContainer.destroy({ children: true, texture: false });
          this.spellBarContainer = null;
          this.spellButtons = [];
        }
        this.createSpellBar();
      }
      // Update consumable bar in-place (no destroy to avoid CDN flicker)
      if (this.consumableBarContainer && this.consumableButtons.length > 0) {
        for (const btn of this.consumableButtons) {
          this.updateConsumableButton(btn.type);
        }
      } else {
        this.createConsumableBar();
      }
      this.startInteractiveBattle();
    } else {
      this.preprocessInitialEvents();
      this.startTimedBattle();
    }
  }

  /* ──────────────── Static Music Control ─────────────────────────── */

  static stopBackgroundMusic(): void {
    stopMusic(musicState);
    musicState = null;
    currentMusicType = null;
  }

  static setGlobalAudioEnabled(enabled: boolean): void {
    globalAudioEnabled = enabled;
    setMusicVolume(musicState, 0.3, enabled);
  }
}
