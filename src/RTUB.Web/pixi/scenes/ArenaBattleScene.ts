/**
 * Arena Battle Scene — 1v1 Attacker vs Defender.
 *
 * TypeScript port of `wwwroot/js/pixiBattle.js` (BattleScene class).
 * Uses shared modules for audio, VFX, tweens, text pooling, and utilities.
 */
import type { Application, Container, Graphics, Sprite, Text, TextStyle, Ticker } from 'pixi.js';
import type { BattleEvent } from '../types/battle-events';
import type { ArenaBattleData, SpellDefinition, EnemyDefinition } from '../types/battle-data';
import type { CombatActionResult, CombatOutcome } from '../types/combat-result';
import type { VfxOwner } from '../shared/vfx';
import type { MusicState } from '../shared/audio';
import type { TextPool } from '../shared/text-pool';
import {
  SESSION_CACHE_BUST,
  loadedAssetAliases,
  getEventField,
  formatNum,
  resolveEvents,
  resolveDotNetRef,
  pick,
} from '../shared/utils';
import {
  getSharedAudioContext,
  playSound,
  playSpellSound,
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
const DEFAULT_EVENT_INTERVAL = 800;

const SPRITE_PATHS = {
  attacker: '/sprites/games/my-tuno/tuno_attacking_right.png',
  defender: '/sprites/games/my-tuno/tuno_attacking_left.png',
  background: '/sprites/games/my-tuno/backgrounds/arena.png',
} as const;

// ─── Helper types ─────────────────────────────────────────────

interface CharacterSprite {
  sprite: Sprite;
  originX: number;
  originY: number;
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

interface TimedBattleEvent {
  event: BattleEvent;
  simTime: number;
}

// ─── Scene Class ──────────────────────────────────────────────

export class ArenaBattleScene implements VfxOwner {
  // PIXI application
  app: Application | null = null;
  stage: Container | null = null;

  // Container & config
  private container: HTMLElement;
  private mode: 'live' | 'replay';
  private eventInterval: number;

  // Events
  private eventsList: BattleEvent[];
  private dotNetRef: DotNet.DotNetObject | null;

  // Combatants
  private attackerName: string;
  private defenderName: string;
  characterSprites: Record<string, CharacterSprite> = {};
  private maxHp = { attacker: 100, defender: 100 };
  private currentHp = { attacker: 100, defender: 100 };
  private hpGraphics: Graphics | null = null;
  private hpTexts: Record<string, Text> = {};
  private nameTexts: Record<string, Text> = {};

  // Shot buff visual
  private hasShotBuff: boolean;
  private attackerAura: Graphics | null = null;

  // Speed bars
  private actionTime = { attacker: 5.0, defender: 5.0 };
  private speedBars: Record<string, Graphics | null> = { attacker: null, defender: null };
  private speedBarTimers = { attacker: 0, defender: 0 };
  private battleStartTime = 0;
  private currentSimTime = 0;
  // Anti-exploit: getter/setter restricts battleSpeed to allowed values
  private _battleSpeed = 1.0;
  get battleSpeed(): number { return this._battleSpeed; }
  set battleSpeed(v: number) {
    const allowed = [1, 5];
    this._battleSpeed = allowed.includes(v) ? v : 1;
  }

  // Replay state
  private currentEventIndex = 0;
  private battleEvents: TimedBattleEvent[] | null = null;
  private replayIndex = 0;
  private isPlaying = false;
  private playbackSpeed = 1;
  private replayAccumulator = 0;
  private battleFinished = false;

  // Audio
  private audioContext: AudioContext | null = null;
  private audioEnabled = true;
  private musicVolume = 0.3;
  private sfxVolume = 0.5;
  private musicState: MusicState | null = null;

  // Interactive mode
  private interactiveMode: boolean;
  private spells: SpellDefinition[];
  private interactivePlayerHP: number | null;
  private interactivePlayerMaxHP: number | null;
  private interactivePlayerActionTime: number | null;
  private interactiveEnemies: EnemyDefinition[];
  private _playerAttackPending = false;
  private _enemyAttackPending = false;
  private _spellPending = false;
  private _cooldownTickAccum = 0;
  private spellButtons: SpellButton[] = [];
  private spellCooldowns: Record<string, number> = {};
  private spellBarContainer: Container | null = null;

  // Cleanup trackers (VfxOwner requirement)
  _timeoutIds: number[] = [];
  _rafIds: number[] = [];
  _textPool: TextPool = { pool: [] };

  // WebGL / visibility listeners
  private _onContextLost: ((e: Event) => void) | null = null;
  private _onVisibilityChange: (() => void) | null = null;

  /* ────────────────────────── Constructor ────────────────────────── */

  constructor(container: HTMLElement, data: {
    events: BattleEvent[];
    dotNetRef: DotNet.DotNetObject | null;
    mode: 'live' | 'replay';
    attackerName: string;
    defenderName: string;
    HasShotBuff?: boolean;
    hasShotBuff?: boolean;
    InteractiveMode?: boolean;
    interactiveMode?: boolean;
    Spells?: SpellDefinition[];
    spells?: SpellDefinition[];
    PlayerHP?: number;
    playerHP?: number;
    PlayerMaxHP?: number;
    playerMaxHP?: number;
    PlayerActionTime?: number;
    playerActionTime?: number;
    Enemies?: EnemyDefinition[];
    enemies?: EnemyDefinition[];
    eventInterval?: number;
  }) {
    this.container = container;
    this.eventsList = data.events ?? [];
    this.dotNetRef = data.dotNetRef ?? null;
    this.mode = data.mode ?? 'live';
    this.eventInterval = data.eventInterval ?? DEFAULT_EVENT_INTERVAL;
    this.attackerName = data.attackerName ?? 'Attacker';
    this.defenderName = data.defenderName ?? 'Defender';
    this.hasShotBuff = data.HasShotBuff ?? data.hasShotBuff ?? false;

    // Interactive mode
    this.interactiveMode = data.InteractiveMode ?? data.interactiveMode ?? false;
    this.spells = data.Spells ?? data.spells ?? [];
    this.interactivePlayerHP = data.PlayerHP ?? data.playerHP ?? null;
    this.interactivePlayerMaxHP = data.PlayerMaxHP ?? data.playerMaxHP ?? null;
    this.interactivePlayerActionTime = data.PlayerActionTime ?? data.playerActionTime ?? null;
    this.interactiveEnemies = data.Enemies ?? data.enemies ?? [];

    // Audio
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
      if (!this.musicState) {
        loadBackgroundMusic(
          this.audioContext,
          '/sound/arena_battle.mp3',
          this.musicVolume,
          this.audioEnabled,
        ).then(state => { this.musicState = state; });
      }
    } else {
      this.audioEnabled = false;
    }
  }

  private _playSound(type: 'attack' | 'hit' | 'critical' | 'ko' | 'victory' | 'block'): void {
    if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
    playSound(this.audioContext, type, this.sfxVolume);
  }

  private _playSpellSound(attackId: string): void {
    if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
    playSpellSound(this.audioContext, attackId, this.sfxVolume);
  }

  toggleAudio(): boolean {
    this.audioEnabled = !this.audioEnabled;
    setMusicVolume(this.musicState, this.musicVolume, this.audioEnabled);
    return this.audioEnabled;
  }

  setVolume(musicVol: number, sfxVol: number): void {
    this.musicVolume = Math.max(0, Math.min(1, musicVol));
    this.sfxVolume = Math.max(0, Math.min(1, sfxVol));
    setMusicVolume(this.musicState, this.musicVolume, this.audioEnabled);
  }

  /* ────────────────────────── PixiJS Init ────────────────────────── */

  private async initPixi(): Promise<void> {
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
    } catch (_) { /* already frozen */ }

    // WebGL context loss recovery
    this._onContextLost = (e: Event) => {
      console.warn('WebGL context lost — finishing battle to recover');
      e.preventDefault();
      this.finishBattle();
    };
    this.app.canvas.addEventListener('webglcontextlost', this._onContextLost);

    // Visibility change recovery
    this._onVisibilityChange = () => {
      if (document.visibilityState === 'visible' && !this.battleFinished) {
        const gl =
          (this.app?.canvas as HTMLCanvasElement | undefined)?.getContext?.('webgl2') ||
          (this.app?.canvas as HTMLCanvasElement | undefined)?.getContext?.('webgl');
        if (!gl || gl.isContextLost()) {
          console.warn('App returned from background with lost GL context — finishing battle');
          this.finishBattle();
        }
      }
    };
    document.addEventListener('visibilitychange', this._onVisibilityChange);

    await this.loadAssets();
    this.create();
  }

  /* ────────────────────────── Asset Loading ──────────────────────── */

  private async loadAssets(): Promise<void> {
    const toLoad: { alias: string; src: string }[] = [];

    if (!loadedAssetAliases.has('attackerSprite')) {
      toLoad.push({ alias: 'attackerSprite', src: SPRITE_PATHS.attacker + SESSION_CACHE_BUST });
    }
    if (!loadedAssetAliases.has('defenderSprite')) {
      toLoad.push({ alias: 'defenderSprite', src: SPRITE_PATHS.defender + SESSION_CACHE_BUST });
    }
    if (!loadedAssetAliases.has('arenaBg')) {
      toLoad.push({ alias: 'arenaBg', src: SPRITE_PATHS.background + SESSION_CACHE_BUST });
    }

    if (toLoad.length > 0) {
      await PIXI.Assets.load(toLoad);
      for (const a of toLoad) {
        loadedAssetAliases.add(a.alias);
      }
    }
  }

  /* ────────────────────────── Scene Creation ─────────────────────── */

  private create(): void {
    if (!this.app || !this.stage) return;
    const { width, height } = this.app.screen;

    // Background
    const bg = PIXI.Sprite.from('arenaBg');
    bg.width = width;
    bg.height = height;
    bg.x = width / 2;
    bg.y = height / 2;
    bg.anchor.set(0.5);
    this.stage.addChild(bg);

    this.createArena();
    this.initializeHpFromEvents();
    this.drawHpBars();
    this.drawSpeedBars();
    this.startIdleAnimation();

    if (this.interactiveMode) {
      this.initInteractiveState();
      this.createSpellBar();
      this.startInteractiveBattle();
    } else if (this.mode === 'live') {
      this.startTimedBattle();
    } else {
      this.setupReplayLoop();
    }

    this.app.ticker.add(this.update, this);
  }

  private createArena(): void {
    if (!this.app || !this.stage) return;
    const { width, height } = this.app.screen;
    const isMobile = width < 768;

    this.createCharacters(width, height, isMobile);
  }

  private createCharacters(width: number, height: number, isMobile: boolean): void {
    if (!this.stage) return;
    const maxSpriteHeight = isMobile ? height * 0.25 : height * 0.45;

    // ── Attacker ──
    const atkSprite = PIXI.Sprite.from('attackerSprite');
    atkSprite.anchor.set(0.5, 1);
    const atkScale = this.getSpriteScale(atkSprite, maxSpriteHeight);
    atkSprite.scale.set(atkScale);

    let atkX: number, atkY: number;
    if (isMobile) {
      atkX = width / 2;
      atkY = height * 0.78;
    } else {
      atkX = width * 0.25;
      atkY = height * 0.75;
    }
    atkSprite.x = atkX;
    atkSprite.y = atkY;
    this.stage.addChild(atkSprite);
    this.characterSprites.attacker = { sprite: atkSprite, originX: atkX, originY: atkY };

    // Shot buff aura
    if (this.hasShotBuff) {
      const aura = new PIXI.Graphics();
      const auraRadius = atkSprite.height * 0.6;
      aura.circle(0, 0, auraRadius);
      aura.fill({ color: 0xff6600, alpha: 0.25 });
      aura.x = atkX;
      aura.y = atkY - atkSprite.height / 2;
      this.stage.addChild(aura);
      this.attackerAura = aura;
    }

    // ── Defender ──
    const defSprite = PIXI.Sprite.from('defenderSprite');
    defSprite.anchor.set(0.5, 1);
    const defScale = this.getSpriteScale(defSprite, maxSpriteHeight);
    defSprite.scale.set(defScale);

    let defX: number, defY: number;
    if (isMobile) {
      defX = width / 2;
      defY = height * 0.42;
    } else {
      defX = width * 0.75;
      defY = height * 0.75;
    }
    defSprite.x = defX;
    defSprite.y = defY;
    this.stage.addChild(defSprite);
    this.characterSprites.defender = { sprite: defSprite, originX: defX, originY: defY };

    // Name labels
    const atkNameText = new PIXI.Text({
      text: this.attackerName,
      style: { fontFamily: 'Arial', fontSize: isMobile ? 14 : 18, fontWeight: 'bold', fill: 0xffffff },
    });
    atkNameText.anchor.set(0.5);
    atkNameText.x = atkX;
    atkNameText.y = atkY + 15;
    this.stage.addChild(atkNameText);
    this.nameTexts.attacker = atkNameText;

    const defNameText = new PIXI.Text({
      text: this.defenderName,
      style: { fontFamily: 'Arial', fontSize: isMobile ? 14 : 18, fontWeight: 'bold', fill: 0xffffff },
    });
    defNameText.anchor.set(0.5);
    defNameText.x = defX;
    defNameText.y = defY + 15;
    this.stage.addChild(defNameText);
    this.nameTexts.defender = defNameText;
  }

  private getSpriteScale(sprite: Sprite, maxSpriteHeight: number): number {
    if (!sprite.texture || !sprite.texture.height) {
      return 0.6;
    }
    return Math.min(1, maxSpriteHeight / sprite.texture.height);
  }

  private startIdleAnimation(): void {
    for (const char of Object.values(this.characterSprites)) {
      const data = {
        breathTime: Math.random() * 3,
        scaleTime: Math.random() * 3,
        originalY: char.sprite.y,
        originalX: char.sprite.x,
        originalScale: char.sprite.scale.x,
      };
      (char.sprite as unknown as Record<string, unknown>).idleAnimationData = data;
    }
  }

  /* ────────────────────────── HP & Speed Bars ────────────────────── */

  private initializeHpFromEvents(): void {
    for (const evt of this.eventsList) {
      const type = getEventField<string>(evt, 'Type');
      if (type !== 'HPUpdate') continue;
      const character = getEventField<string>(evt, 'Character');
      const hp = getEventField<number>(evt, 'HP') ?? 0;
      const maxHP = getEventField<number>(evt, 'MaxHP');
      const actionTime = getEventField<number>(evt, 'ActionTime');

      if (character === 'Attacker') {
        if (maxHP != null) this.maxHp.attacker = maxHP;
        this.currentHp.attacker = hp;
        if (actionTime != null) this.actionTime.attacker = actionTime;
      } else if (character === 'Defender') {
        if (maxHP != null) this.maxHp.defender = maxHP;
        this.currentHp.defender = hp;
        if (actionTime != null) this.actionTime.defender = actionTime;
      }
    }
  }

  private drawHpBars(): void {
    if (!this.app || !this.stage) return;
    const { width } = this.app.screen;
    const isMobile = width < 768;

    // Remove previous
    if (this.hpGraphics) {
      this.stage.removeChild(this.hpGraphics);
      this.hpGraphics.destroy();
    }
    for (const t of Object.values(this.hpTexts)) {
      this.stage.removeChild(t);
      t.destroy();
    }
    this.hpTexts = {};

    const g = new PIXI.Graphics();
    this.stage.addChild(g);
    this.hpGraphics = g;

    const barW = isMobile ? 120 : 180;
    const barH = isMobile ? 14 : 18;

    for (const key of ['attacker', 'defender'] as const) {
      const char = this.characterSprites[key];
      if (!char) continue;

      const x = char.sprite.x - barW / 2;
      const y = char.sprite.y - char.sprite.height - (isMobile ? 22 : 30);

      // Background
      g.roundRect(x, y, barW, barH, 4);
      g.fill({ color: 0x333333, alpha: 0.8 });

      // HP fill
      const ratio = Math.max(0, this.currentHp[key] / this.maxHp[key]);
      const fillColor = ratio > 0.5 ? 0x44cc44 : ratio > 0.25 ? 0xcccc44 : 0xcc4444;
      if (ratio > 0) {
        g.roundRect(x, y, barW * ratio, barH, 4);
        g.fill({ color: fillColor, alpha: 0.9 });
      }

      // Border
      g.roundRect(x, y, barW, barH, 4);
      g.stroke({ color: 0x888888, width: 1 });

      // Text
      const hpText = new PIXI.Text({
        text: `${formatNum(this.currentHp[key])} / ${formatNum(this.maxHp[key])}`,
        style: { fontFamily: 'Arial', fontSize: isMobile ? 10 : 12, fill: 0xffffff },
      });
      hpText.anchor.set(0.5);
      hpText.x = char.sprite.x;
      hpText.y = y + barH / 2;
      this.stage.addChild(hpText);
      this.hpTexts[key] = hpText;
    }
  }

  private drawSpeedBars(): void {
    if (!this.app || !this.stage) return;
    const { width } = this.app.screen;
    const isMobile = width < 768;

    for (const key of ['attacker', 'defender'] as const) {
      // Remove old
      if (this.speedBars[key]) {
        this.stage.removeChild(this.speedBars[key]!);
        this.speedBars[key]!.destroy();
        this.speedBars[key] = null;
      }

      const char = this.characterSprites[key];
      if (!char) continue;

      const barW = isMobile ? 120 : 180;
      const barH = 6;
      const x = char.sprite.x - barW / 2;
      const y = char.sprite.y - char.sprite.height - (isMobile ? 8 : 12);

      const g = new PIXI.Graphics();
      // Background
      g.roundRect(x, y, barW, barH, 2);
      g.fill({ color: 0x222222, alpha: 0.6 });

      // Fill — starts full, depletes as timer counts down to 0
      const maxMs = this.actionTime[key] * 1000;
      const ratio = maxMs > 0 ? Math.max(0, this.speedBarTimers[key] / maxMs) : 0;
      if (ratio > 0) {
        g.roundRect(x, y, barW * ratio, barH, 2);
        g.fill({ color: 0x00aaff, alpha: 0.8 });
      }

      this.stage.addChild(g);
      this.speedBars[key] = g;
    }
  }

  /* ──────────────── Interactive Mode Init ────────────────────────── */

  private initInteractiveState(): void {
    // Override HP from interactive data
    if (this.interactivePlayerHP != null) this.currentHp.attacker = this.interactivePlayerHP;
    if (this.interactivePlayerMaxHP != null) this.maxHp.attacker = this.interactivePlayerMaxHP;
    if (this.interactivePlayerActionTime != null) this.actionTime.attacker = this.interactivePlayerActionTime;

    // Single enemy for arena
    const enemy = this.interactiveEnemies[0];
    if (enemy) {
      const hp = enemy.hp ?? enemy.HP ?? 100;
      const maxHP = enemy.maxHP ?? enemy.MaxHP ?? hp;
      const at = enemy.actionTime ?? enemy.ActionTime ?? 5.0;
      this.currentHp.defender = hp;
      this.maxHp.defender = maxHP;
      this.actionTime.defender = at;
    }

    // Start speed bar timers from full
    this.speedBarTimers.attacker = this.actionTime.attacker * 1000;
    this.speedBarTimers.defender = this.actionTime.defender * 1000;

    this.drawHpBars();
    this.drawSpeedBars();
  }

  /* ────────────────────── Spell Bar UI ───────────────────────────── */

  private createSpellBar(): void {
    if (!this.spells || this.spells.length === 0 || !this.app || !this.stage) return;

    const { width, height } = this.app.screen;
    const isMobile = width < 768;
    const btnSize = isMobile ? 52 : 68;
    const btnGap = isMobile ? 10 : 14;
    const totalWidth = this.spells.length * btnSize + (this.spells.length - 1) * btnGap;
    const startX = (width - totalWidth) / 2;
    const barY = height - btnSize - 8;

    this.spellBarContainer = new PIXI.Container();
    this.stage.addChild(this.spellBarContainer);

    const backdrop = new PIXI.Graphics();
    backdrop.roundRect(startX - 8, barY - 6, totalWidth + 16, btnSize + 12, 8);
    backdrop.fill({ color: 0x000000, alpha: 0.5 });
    this.spellBarContainer.addChild(backdrop);

    this.spellButtons = [];

    for (let i = 0; i < this.spells.length; i++) {
      const spell = this.spells[i];
      const attackId = spell.attackId ?? spell.AttackId ?? '';
      const name = spell.name ?? spell.Name ?? attackId;
      const icon = spell.icon ?? spell.Icon ?? '⚡';
      const cooldown = spell.cooldownSeconds ?? spell.CooldownSeconds ?? 10;
      const x = startX + i * (btnSize + btnGap);

      const btnContainer = new PIXI.Container();
      btnContainer.x = x;
      btnContainer.y = barY;

      const bg = new PIXI.Graphics();
      bg.roundRect(0, 0, btnSize, btnSize, 6);
      bg.fill({ color: 0x2a2a4a, alpha: 0.9 });
      bg.stroke({ color: 0x6666aa, width: 2 });
      btnContainer.addChild(bg);

      const iconText = new PIXI.Text({
        text: icon,
        style: { fontSize: isMobile ? 22 : 28, fontFamily: 'Arial, sans-serif', fill: 0xffffff },
      });
      iconText.anchor.set(0.5);
      iconText.x = btnSize / 2;
      iconText.y = btnSize / 2 - 4;
      btnContainer.addChild(iconText);

      const nameText = new PIXI.Text({
        text: name.length > 6 ? name.substring(0, 6) : name,
        style: { fontSize: isMobile ? 8 : 10, fontFamily: 'Arial, sans-serif', fill: 0xcccccc },
      });
      nameText.anchor.set(0.5);
      nameText.x = btnSize / 2;
      nameText.y = btnSize - 6;
      btnContainer.addChild(nameText);

      const cdOverlay = new PIXI.Graphics();
      cdOverlay.roundRect(0, 0, btnSize, btnSize, 6);
      cdOverlay.fill({ color: 0x000000, alpha: 0.7 });
      cdOverlay.visible = false;
      btnContainer.addChild(cdOverlay);

      const cdText = new PIXI.Text({
        text: '',
        style: { fontSize: 16, fontFamily: 'Arial, sans-serif', fontWeight: 'bold', fill: 0xffffff },
      });
      cdText.anchor.set(0.5);
      cdText.x = btnSize / 2;
      cdText.y = btnSize / 2;
      cdText.visible = false;
      btnContainer.addChild(cdText);

      btnContainer.eventMode = 'static';
      btnContainer.cursor = 'pointer';
      btnContainer.on('pointerdown', () => this.onSpellButtonClick(attackId));

      this.spellBarContainer.addChild(btnContainer);

      this.spellButtons.push({
        container: btnContainer, bg, iconText, nameText, cdOverlay, cdText,
        attackId, cooldownSeconds: cooldown, spell,
      });
    }
  }

  /* ────────────────── Interactive Battle Flow ────────────────────── */

  private startInteractiveBattle(): void {
    this.battleStartTime = Date.now();
    this.currentSimTime = 0;
    this.battleFinished = false;
    this.isPlaying = true;
    this._playerAttackPending = false;
    this._enemyAttackPending = false;
    this._spellPending = false;
    this._cooldownTickAccum = 0;
  }

  private onSpellButtonClick(attackId: string): void {
    if (this.battleFinished || !this.isPlaying) return;
    if (this._spellPending) return;
    const cd = this.spellCooldowns[attackId] ?? 0;
    if (cd > 0) return;
    this._spellPending = true;
    this.requestPlayerSpell(attackId);
  }

  private async requestPlayerAutoAttack(): Promise<void> {
    if (!this.dotNetRef || this.battleFinished) {
      this._playerAttackPending = false;
      return;
    }
    try {
      const json = await this.dotNetRef.invokeMethodAsync<string>('OnPlayerAutoAttack');
      if (json) this.processServerResult(JSON.parse(json) as CombatActionResult);
    } catch (e) {
      console.warn('OnPlayerAutoAttack error:', e);
    } finally {
      this._playerAttackPending = false;
    }
  }

  private async requestEnemyAttack(): Promise<void> {
    if (!this.dotNetRef || this.battleFinished) {
      this._enemyAttackPending = false;
      return;
    }
    try {
      const json = await this.dotNetRef.invokeMethodAsync<string>('OnEnemyAttack', 0);
      if (json) this.processServerResult(JSON.parse(json) as CombatActionResult);
    } catch (e) {
      console.warn('OnEnemyAttack error:', e);
    } finally {
      this._enemyAttackPending = false;
    }
  }

  private async requestPlayerSpell(attackId: string): Promise<void> {
    if (!this.dotNetRef || this.battleFinished) {
      this._spellPending = false;
      return;
    }
    try {
      const json = await this.dotNetRef.invokeMethodAsync<string>('OnPlayerSpell', attackId);
      if (json) this.processServerResult(JSON.parse(json) as CombatActionResult);
    } catch (e) {
      console.warn('OnPlayerSpell error:', e);
    } finally {
      this._spellPending = false;
    }
  }

  private async requestTickCooldowns(elapsedSeconds: number): Promise<void> {
    if (!this.dotNetRef || this.battleFinished) return;
    try {
      const json = await this.dotNetRef.invokeMethodAsync<string>('OnTickCooldowns', elapsedSeconds);
      if (json) {
        const data = JSON.parse(json) as Record<string, unknown>;
        const spellCooldowns = (data.spells ?? data) as Record<string, number>;
        for (const [id, remaining] of Object.entries(spellCooldowns)) {
          this.spellCooldowns[id] = remaining;
        }
      }
    } catch (e) {
      console.warn('OnTickCooldowns error:', e);
    }
  }

  private processServerResult(result: CombatActionResult): void {
    if (!result) return;

    const events = result.events ?? result.Events ?? [];
    for (const evt of events) {
      this.processInteractiveEvent(evt);
    }

    const cooldowns = result.spellCooldowns ?? result.SpellCooldowns;
    if (cooldowns) {
      for (const [id, remaining] of Object.entries(cooldowns)) {
        this.spellCooldowns[id] = remaining;
      }
    }

    const battleOver = result.battleOver ?? result.BattleOver ?? false;
    if (battleOver) {
      this.isPlaying = false;
      const outcome = result.outcome ?? result.Outcome;
      if (outcome === 0 /* AttackerWon */) {
        this.showVictory('Attacker');
      } else if (outcome === 2 /* Draw */) {
        this.showDraw();
      } else {
        this.showVictory('Defender');
      }
      const id = setTimeout(() => this.finishBattle(), 2000 / this.battleSpeed) as unknown as number;
      this._timeoutIds.push(id);
    }
  }

  private processInteractiveEvent(evt: BattleEvent): void {
    const evtType = evt.type ?? evt.Type;
    const attackId = evt.attackId ?? evt.AttackId;

    switch (evtType) {
      case 'HPUpdate':
        this.processEvent(evt);
        break;
      case 'Attack':
        if (attackId) {
          this.handleSpellAttack(evt);
        } else {
          this.processEvent(evt);
        }
        break;
      case 'KO':
        this.processEvent(evt);
        break;
      case 'StatusEffect':
        this.handleStatusEffect(evt);
        break;
      case 'Victory':
      case 'BattleStart':
        break;
    }
  }

  /* ────────────────────── Spell / Status VFX ─────────────────────── */

  private handleSpellAttack(evt: BattleEvent): void {
    const attacker = evt.attacker ?? evt.Attacker ?? '';
    const defender = evt.defender ?? evt.Defender ?? '';
    const damage = evt.damage ?? evt.Damage ?? 0;
    const isCritical = evt.isCritical ?? evt.IsCritical ?? false;
    const vfxType = evt.vfxType ?? evt.VfxType;
    const vfxColor = evt.vfxColor ?? evt.VfxColor ?? '#ff6600';
    const doScreenShake = evt.screenShake ?? evt.ScreenShake ?? false;
    const visualHint = evt.visualHint ?? evt.VisualHint;
    const abilityName = evt.abilityName ?? evt.AbilityName ?? 'Spell';
    const effectName = evt.effectName ?? evt.EffectName;
    const attackId = evt.attackId ?? evt.AttackId;

    const color =
      typeof vfxColor === 'string' && vfxColor.startsWith('#')
        ? parseInt(vfxColor.replace('#', ''), 16)
        : typeof vfxColor === 'number'
          ? vfxColor
          : 0xff6600;

    if (doScreenShake || visualHint === 'screenShake') screenShake(this);

    const atkChar = attacker === 'Attacker' ? this.characterSprites.attacker : this.characterSprites.defender;
    const defChar = defender === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
    if (!atkChar || !defChar) return;

    const atkSpr = atkChar.sprite;
    const defSpr = defChar.sprite;

    if (attacker === 'Attacker') {
      // Player spell — lunge animation
      const startX = atkChar.originX;
      animateTo(this, atkSpr, { x: startX + 40 }, 120, () => {
        animateTo(this, atkSpr, { x: startX }, 200);
      });

      // Show ability name above attacker
      showFloatingText(this, abilityName.toUpperCase(), atkSpr.x, atkSpr.y - atkSpr.height * 0.8, color);

      if (defender === 'Attacker') {
        // Self-buff/heal
        playBuffVfx(this, atkSpr, color);
        if (damage < 0) {
          showFloatingText(this, `+${formatNum(Math.abs(damage))}`, atkSpr.x, atkSpr.y - atkSpr.height * 0.6, 0x44ff44);
        }
        if (effectName) showEffectLabel(this, effectName, atkSpr);
      } else {
        // Hit defender
        playSpellVfx(this, vfxType, color, atkSpr, defSpr);
        defSpr.tint = isCritical ? 0xff0000 : 0xff5555;
        const id = setTimeout(
          () => { if (!defSpr.destroyed) defSpr.tint = 0xffffff; },
          200 / this.battleSpeed,
        ) as unknown as number;
        this._timeoutIds.push(id);
        if (damage > 0) {
          showDamageText(this, damage, isCritical, defSpr.x, defSpr.y - defSpr.height * 0.6);
        }
        if (effectName) showEffectLabel(this, effectName, defSpr);
      }

      if (attackId) {
        this._playSpellSound(attackId);
      } else {
        this._playSound(isCritical ? 'critical' : 'attack');
      }
    }
  }

  private handleStatusEffect(evt: BattleEvent): void {
    const character = evt.character ?? evt.Character ?? '';
    const effectName = evt.effectName ?? evt.EffectName ?? '';
    const damage = evt.damage ?? evt.Damage ?? 0;

    const target = character === 'Attacker'
      ? this.characterSprites.attacker?.sprite
      : this.characterSprites.defender?.sprite;
    if (!target) return;

    showEffectLabel(this, effectName, target);
    if (damage > 0) {
      showDamageText(this, damage, false, target.x, target.y - target.height * 0.6);
    }
  }

  private updateSpellCooldownVisuals(): void {
    for (const btn of this.spellButtons) {
      const cd = this.spellCooldowns[btn.attackId] ?? 0;
      if (cd > 0) {
        btn.cdOverlay.visible = true;
        btn.cdText.visible = true;
        btn.cdText.text = Math.ceil(cd).toString();
        btn.container.cursor = 'not-allowed';
        btn.bg.alpha = 0.5;
      } else {
        btn.cdOverlay.visible = false;
        btn.cdText.visible = false;
        btn.container.cursor = 'pointer';
        btn.bg.alpha = 0.9;
      }
    }
  }

  /* ────────────────────── Timed / Replay Modes ───────────────────── */

  private startTimedBattle(): void {
    this.battleEvents = this.eventsList.map(evt => ({
      event: evt,
      simTime: getEventField<number>(evt, 'SimTime') ?? getEventField<number>(evt, 'simTime') ?? 0,
    })).sort((a, b) => a.simTime - b.simTime);

    this.currentEventIndex = 0;
    this.battleStartTime = Date.now();
    this.currentSimTime = 0;
    this.battleFinished = false;
    this.isPlaying = true;

    // Process initial events with simTime 0
    while (this.currentEventIndex < this.battleEvents.length) {
      const eventData = this.battleEvents[this.currentEventIndex];
      if (eventData.simTime > 0) break;
      this.processEvent(eventData.event);
      this.currentEventIndex++;
    }
  }

  private processEvent(evt: BattleEvent): void {
    const type = getEventField<string>(evt, 'Type');

    if (type === 'HPUpdate') {
      const character = getEventField<string>(evt, 'Character');
      const hp = getEventField<number>(evt, 'HP') ?? 0;
      if (character === 'Attacker') this.currentHp.attacker = hp;
      else if (character === 'Defender') this.currentHp.defender = hp;
      this.drawHpBars();
      return;
    }

    if (type === 'Attack') {
      const attackerKey = getEventField<string>(evt, 'Attacker');
      const defenderKey = getEventField<string>(evt, 'Defender');
      const damage = getEventField<number>(evt, 'Damage');
      this.playAttack(attackerKey ?? 'Attacker', defenderKey ?? 'Defender', damage ?? 0, evt);
      return;
    }

    if (type === 'KO') {
      const character = getEventField<string>(evt, 'Character');
      this.playKo(character ?? 'Defender');
      return;
    }

    if (type === 'Victory') {
      const winner = getEventField<string>(evt, 'Winner');
      this.showVictory(winner ?? 'Attacker');
    }
  }

  /* ────────────────────── Attack Animation ───────────────────────── */

  private playAttack(attackerKey: string, defenderKey: string, damage: number, evt: BattleEvent): void {
    const attacker = attackerKey === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
    const defender = defenderKey === 'Attacker' ? this.characterSprites.attacker : this.characterSprites.defender;
    if (!attacker || !defender) return;

    const direction = attackerKey === 'Defender' ? -1 : 1;
    const distance = Math.abs(defender.sprite.x - attacker.sprite.x);
    const lungeOffset = Math.min(220, distance * 0.6);
    const startX = attacker.originX;
    const startY = attacker.originY;
    const targetX = startX + direction * lungeOffset;
    const targetY = startY - 15;

    const damageValue = damage ?? 0;
    const isCritical = evt?.isCritical === true || evt?.IsCritical === true;
    const isBlocked = evt?.isBlocked === true || evt?.IsBlocked === true;
    const isDodged = evt?.isDodged === true || evt?.IsDodged === true;
    const isBoosted = evt?.isBoosted === true || evt?.IsBoosted === true;

    this._playSound((isBlocked || isDodged) ? 'block' : (isCritical ? 'critical' : 'attack'));

    const lungeDuration = isCritical ? 150 : 200;
    animateTo(this, attacker.sprite, {
      x: targetX,
      y: targetY,
      rotation: (direction * (isCritical ? 18 : 12)) * Math.PI / 180,
    }, lungeDuration, () => {
      animateTo(this, attacker.sprite, {
        x: startX,
        y: startY,
        rotation: 0,
      }, 240);
    });

    if (isBlocked || isDodged) {
      defender.sprite.tint = 0x00e5ff;
      const id = setTimeout(() => { defender.sprite.tint = 0xffffff; }, 300 / this.battleSpeed) as unknown as number;
      this._timeoutIds.push(id);
    } else {
      const defenderTintColor = isCritical ? 0xff0000 : 0xff5555;
      defender.sprite.tint = defenderTintColor;
      const id = setTimeout(() => { defender.sprite.tint = 0xffffff; }, 200 / this.battleSpeed) as unknown as number;
      this._timeoutIds.push(id);
    }

    this._playSound('hit');

    if (!isBlocked && !isDodged) {
      const defenderStartX = defender.originX;
      const recoilDistance = isCritical ? 30 : 20;
      animateTo(this, defender.sprite, {
        x: defenderStartX + direction * recoilDistance,
      }, isCritical ? 100 : 120, () => {
        animateTo(this, defender.sprite, { x: defenderStartX }, 100);
      });
    }

    // Impact flash
    const impactX = defender.sprite.x;
    const impactY = defender.sprite.y - defender.sprite.height * 0.4;
    const impactColor = (isBlocked || isDodged) ? 0x00e5ff : (isCritical ? 0xffff00 : 0xffd54f);
    const impactSize = isCritical ? 25 : 18;
    const impact = new PIXI.Graphics();
    impact.circle(impactX, impactY, impactSize);
    impact.fill({ color: impactColor, alpha: 0.9 });
    this.stage!.addChild(impact);

    fadeOut(this, impact, isCritical ? 400 : 300, () => {
      this.stage?.removeChild(impact);
    });

    // Slash trail
    if (!isBlocked && !isDodged) {
      const slash = new PIXI.Graphics();
      const slashColor = isCritical ? 0xffff00 : 0xffffff;
      slash.moveTo(attacker.sprite.x, attacker.sprite.y - attacker.sprite.height * 0.5);
      slash.lineTo(defender.sprite.x, defender.sprite.y - defender.sprite.height * 0.5);
      slash.stroke({ width: isCritical ? 6 : 4, color: slashColor, alpha: 0.9 });
      this.stage!.addChild(slash);

      fadeOut(this, slash, isCritical ? 250 : 200, () => {
        this.stage?.removeChild(slash);
      });
    }

    // Floating text on defender
    if (isDodged) {
      const dodgeText = getPooledText(this._textPool, 'DODGE', {
        fontFamily: 'Arial', fontSize: 28, fontWeight: 'bold',
        fill: 0x00e5ff, stroke: { color: 0x000000, width: 4 },
      });
      if (dodgeText) {
        dodgeText.anchor.set(0.5);
        dodgeText.x = defender.sprite.x;
        dodgeText.y = defender.sprite.y - defender.sprite.height * 0.6;
        this.stage!.addChild(dodgeText);
        animateTo(this, dodgeText, { y: dodgeText.y - 70, alpha: 0 }, 900, () => {
          releaseText(this._textPool, dodgeText);
        });
      }
    } else if (isBlocked) {
      const blockedText = getPooledText(this._textPool, 'BLOCKED', {
        fontFamily: 'Arial', fontSize: 28, fontWeight: 'bold',
        fill: 0x00e5ff, stroke: { color: 0x000000, width: 4 },
      });
      if (blockedText) {
        blockedText.anchor.set(0.5);
        blockedText.x = defender.sprite.x;
        blockedText.y = defender.sprite.y - defender.sprite.height * 0.6;
        this.stage!.addChild(blockedText);
        animateTo(this, blockedText, { y: blockedText.y - 70, alpha: 0 }, 900, () => {
          releaseText(this._textPool, blockedText);
        });
      }
    } else {
      showDamageText(this, damageValue, isCritical, defender.sprite.x, defender.sprite.y - defender.sprite.height * 0.6);
    }

    // Floating text on attacker (boosted, damage dealt)
    if (isBoosted) {
      const extraText = getPooledText(this._textPool, 'EXTRA', {
        fontFamily: 'Arial', fontSize: 26, fontWeight: 'bold',
        fill: 0xff9800, stroke: { color: 0x000000, width: 4 },
      });
      if (extraText) {
        extraText.anchor.set(0.5);
        extraText.x = attacker.sprite.x;
        extraText.y = attacker.sprite.y - attacker.sprite.height * 0.8;
        this.stage!.addChild(extraText);
        animateTo(this, extraText, { y: extraText.y - 50, alpha: 0 }, 800, () => {
          releaseText(this._textPool, extraText);
        });
      }
    }

    if (!isBlocked && !isDodged) {
      const attackerText = getPooledText(this._textPool, `+${formatNum(damageValue)}`, {
        fontFamily: 'Arial', fontSize: 18, fontWeight: 'bold', fill: 0x4caf50,
      });
      if (attackerText) {
        attackerText.anchor.set(0.5);
        attackerText.x = attacker.sprite.x;
        attackerText.y = attacker.sprite.y - attacker.sprite.height * 0.6;
        this.stage!.addChild(attackerText);
        animateTo(this, attackerText, { y: attackerText.y - 20, alpha: 0 }, 700, () => {
          releaseText(this._textPool, attackerText);
        });
      }
    }
  }

  /* ────────────────────── KO / Victory / Draw ────────────────────── */

  private playKo(character: string): void {
    const target = character === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
    if (!target) return;

    this._playSound('ko');

    animateTo(this, target.sprite, {
      alpha: 0.4,
      rotation: (character === 'Defender' ? 90 : -90) * Math.PI / 180,
      y: target.sprite.y + 30,
    }, 600);

    const koText = new PIXI.Text({
      text: 'K.O.!',
      style: {
        fontFamily: 'Arial', fontSize: 36, fontWeight: 'bold',
        fill: 0xff0000, stroke: { color: 0x000000, width: 4 },
      },
    });
    koText.anchor.set(0.5);
    koText.x = target.sprite.x;
    koText.y = target.sprite.y - target.sprite.height - 30;
    koText.alpha = 0;
    this.stage!.addChild(koText);

    animateTo(this, koText, { alpha: 1 }, 200, () => {
      const tid = setTimeout(() => {
        animateTo(this, koText, { alpha: 0 }, 200, () => {
          this.stage?.removeChild(koText);
        });
      }, 400 / this.battleSpeed) as unknown as number;
      this._timeoutIds.push(tid);
    });
  }

  private showVictory(winner: string): void {
    if (!this.stage || !this.app) return;
    const isAttackerWinner = winner === 'Attacker' || winner === this.attackerName;
    const winnerSprite = isAttackerWinner ? this.characterSprites.attacker : this.characterSprites.defender;
    const winnerName = isAttackerWinner ? this.attackerName : this.defenderName;

    this._playSound('victory');

    if (winnerSprite) {
      const originalY = winnerSprite.sprite.y;
      animateTo(this, winnerSprite.sprite, { y: originalY - 20 }, 200, () => {
        animateTo(this, winnerSprite.sprite, { y: originalY }, 200, () => {
          animateTo(this, winnerSprite.sprite, { y: originalY - 20 }, 200, () => {
            animateTo(this, winnerSprite.sprite, { y: originalY }, 200);
          });
        });
      });
    }

    const victoryText = new PIXI.Text({
      text: `${winnerName} vence!`,
      style: {
        fontFamily: 'Arial', fontSize: 48, fontWeight: 'bold',
        fill: 0xffd700,
        stroke: { color: 0x000000, width: 6 },
        dropShadow: { color: 0x000000, blur: 5, angle: Math.PI / 4, distance: 3 },
      },
    });
    victoryText.anchor.set(0.5);
    victoryText.x = this.app.screen.width / 2;
    victoryText.y = this.app.screen.height / 2 - 50;
    victoryText.alpha = 0;
    victoryText.scale.set(0.5);
    this.stage.addChild(victoryText);

    animateTo(this, victoryText, { alpha: 1, scale: 1.2 }, 400);

    const tid = setTimeout(() => {
      animateTo(this, victoryText, { alpha: 0, y: victoryText.y - 30 }, 800, () => {
        this.stage?.removeChild(victoryText);
      });
    }, 1200 / this.battleSpeed) as unknown as number;
    this._timeoutIds.push(tid);
  }

  private showDraw(): void {
    if (!this.stage || !this.app) return;

    const drawText = new PIXI.Text({
      text: 'Empate!',
      style: {
        fontFamily: 'Arial', fontSize: 48, fontWeight: 'bold',
        fill: 0xcccccc,
        stroke: { color: 0x000000, width: 6 },
      },
    });
    drawText.anchor.set(0.5);
    drawText.x = this.app.screen.width / 2;
    drawText.y = this.app.screen.height / 2 - 50;
    drawText.alpha = 0;
    drawText.scale.set(0.5);
    this.stage.addChild(drawText);

    animateTo(this, drawText, { alpha: 1, scale: 1.2 }, 400);

    const tid = setTimeout(() => {
      animateTo(this, drawText, { alpha: 0, y: drawText.y - 30 }, 800, () => {
        this.stage?.removeChild(drawText);
      });
    }, 1200 / this.battleSpeed) as unknown as number;
    this._timeoutIds.push(tid);
  }

  /* ────────────────────── Battle End ─────────────────────────────── */

  private finishBattle(): void {
    if (this.battleFinished) return;
    this.battleFinished = true;

    if (this.dotNetRef?.invokeMethodAsync) {
      this.dotNetRef.invokeMethodAsync('OnBattleFinished').catch(e => {
        console.warn('Could not notify Blazor of battle finish:', e);
      });
    }
  }

  /* ────────────────────── Replay Controls ────────────────────────── */

  private setupReplayLoop(): void {
    this.isPlaying = false;
    this.replayAccumulator = 0;
  }

  private updateCharacterStates(eventIndex: number): void {
    this.initializeHpFromEvents();
    for (let i = 0; i <= eventIndex && i < this.eventsList.length; i++) {
      const evt = this.eventsList[i];
      const type = getEventField<string>(evt, 'Type');
      if (type === 'HPUpdate') {
        const character = getEventField<string>(evt, 'Character');
        const hp = getEventField<number>(evt, 'HP') ?? 0;
        if (character === 'Attacker') this.currentHp.attacker = hp;
        else if (character === 'Defender') this.currentHp.defender = hp;
      }
    }
    this.drawHpBars();
  }

  setReplayPlaying(isPlaying: boolean): void {
    this.isPlaying = isPlaying;
  }

  setReplaySpeed(speed: number): void {
    const validSpeed = speed === 5 ? 5 : 1;
    this.playbackSpeed = validSpeed;
    this._battleSpeed = validSpeed;
  }

  jumpToEvent(index: number): void {
    if (index < 0 || index >= this.eventsList.length) return;
    this.replayIndex = index;
    this.updateCharacterStates(index);
    this.processEvent(this.eventsList[index]);
  }

  /* ────────────────────── Main Update Loop ───────────────────────── */

  private update(): void {
    if (!this.app) return;
    const deltaMs = this.app.ticker.deltaMS;

    // Character idle animations
    for (const char of Object.values(this.characterSprites)) {
      const data = (char.sprite as unknown as Record<string, unknown>).idleAnimationData as
        { breathTime: number; scaleTime: number; originalY: number; originalX: number; originalScale: number } | undefined;
      if (data) {
        data.breathTime += deltaMs / 1000;
        data.scaleTime += deltaMs / 1000;

        const breathOffset = Math.sin(data.breathTime * Math.PI / 1.8) * 8;
        char.sprite.y = data.originalY + breathOffset;

        const swayOffset = Math.sin(data.breathTime * 0.8) * 3;
        char.sprite.x = data.originalX + swayOffset;

        const scaleOffset = Math.sin(data.scaleTime * Math.PI / 2) * 0.02;
        const newScale = data.originalScale * (1 + scaleOffset);
        char.sprite.scale.set(newScale);
      }
    }

    // Animate attacker aura (shot buff glow)
    if (this.attackerAura && !(this.attackerAura as unknown as { destroyed?: boolean }).destroyed && this.characterSprites.attacker) {
      const att = this.characterSprites.attacker;
      this.attackerAura.x = att.sprite.x;
      const spriteHeight = att.sprite.height;
      this.attackerAura.y = att.sprite.y - spriteHeight / 2;
      const time = performance.now() / 1000;
      this.attackerAura.alpha = 0.25 + Math.sin(time * 1.2) * 0.12;
    }

    // ── Interactive mode: speed bars trigger server calls ──
    if (this.interactiveMode && !this.battleFinished && this.isPlaying) {
      const simDelta = deltaMs * this.battleSpeed;
      this.currentSimTime += simDelta;

      // Attacker (player) speed bar
      if (this.currentHp.attacker > 0) {
        this.speedBarTimers.attacker = Math.max(0, this.speedBarTimers.attacker - simDelta);
        if (this.speedBarTimers.attacker <= 0 && !this._playerAttackPending) {
          this._playerAttackPending = true;
          this.speedBarTimers.attacker = this.actionTime.attacker * 1000;
          this.requestPlayerAutoAttack();
        }
      }

      // Defender (enemy) speed bar
      if (this.currentHp.defender > 0) {
        this.speedBarTimers.defender = Math.max(0, this.speedBarTimers.defender - simDelta);
        if (this.speedBarTimers.defender <= 0 && !this._enemyAttackPending) {
          this._enemyAttackPending = true;
          this.speedBarTimers.defender = this.actionTime.defender * 1000;
          this.requestEnemyAttack();
        }
      }

      // Tick cooldowns (~200ms)
      this._cooldownTickAccum += simDelta;
      if (this._cooldownTickAccum >= 200) {
        const elapsed = this._cooldownTickAccum / 1000;
        this._cooldownTickAccum = 0;
        for (const id of Object.keys(this.spellCooldowns)) {
          this.spellCooldowns[id] = Math.max(0, this.spellCooldowns[id] - elapsed);
        }
        this.updateSpellCooldownVisuals();
        this.requestTickCooldowns(elapsed);
      }

      this.drawSpeedBars();
      return; // Don't process pre-computed events
    }

    // Time-based battle simulation (live mode)
    if (this.mode === 'live' && !this.battleFinished && this.isPlaying && this.battleEvents) {
      const simDelta = deltaMs * this.battleSpeed;
      this.currentSimTime += simDelta;

      // Update speed bar timers
      if (this.currentHp.attacker > 0) {
        this.speedBarTimers.attacker = Math.max(0, this.speedBarTimers.attacker - simDelta);
      }
      if (this.currentHp.defender > 0) {
        this.speedBarTimers.defender = Math.max(0, this.speedBarTimers.defender - simDelta);
      }

      // Process events at current sim time
      while (this.currentEventIndex < this.battleEvents.length) {
        const eventData = this.battleEvents[this.currentEventIndex];
        if (eventData.simTime > this.currentSimTime) break;

        const evt = eventData.event;
        const type = getEventField<string>(evt, 'Type');

        // Reset speed bar on attack
        if (type === 'Attack') {
          const attackerField = getEventField<string>(evt, 'Attacker');
          if (attackerField === 'Attacker') {
            this.speedBarTimers.attacker = this.actionTime.attacker * 1000;
          } else if (attackerField === 'Defender') {
            this.speedBarTimers.defender = this.actionTime.defender * 1000;
          }
        }

        this.processEvent(evt);
        this.currentEventIndex++;

        // Battle end
        if (type === 'Victory' || type === 'Draw') {
          this.isPlaying = false;
          const tid = setTimeout(() => this.finishBattle(), 2000 / this.battleSpeed) as unknown as number;
          this._timeoutIds.push(tid);
          break;
        }
      }

      this.drawSpeedBars();
    }

    // Replay mode
    if (this.mode === 'replay' && this.isPlaying) {
      this.replayAccumulator += deltaMs * this.playbackSpeed;
      if (this.replayAccumulator >= 1000) {
        this.replayAccumulator = 0;
        this.replayIndex += 1;
        if (this.replayIndex < this.eventsList.length) {
          this.processEvent(this.eventsList[this.replayIndex]);
          this.updateCharacterStates(this.replayIndex);
        }
      }
    }
  }

  /* ────────────────────── Cleanup ────────────────────────────────── */

  destroy(): void {
    // Remove event listeners
    if (this._onContextLost && this.app?.canvas) {
      this.app.canvas.removeEventListener('webglcontextlost', this._onContextLost);
    }
    if (this._onVisibilityChange) {
      document.removeEventListener('visibilitychange', this._onVisibilityChange);
    }

    // Clear all pending timers
    for (const id of this._timeoutIds) clearTimeout(id);
    for (const id of this._rafIds) cancelAnimationFrame(id);
    this._timeoutIds = [];
    this._rafIds = [];

    // Destroy pooled texts
    destroyTextPool(this._textPool);

    // Clean up interactive mode
    this.spellBarContainer = null;
    this.spellButtons = [];

    if (this.app) {
      this.app.ticker.stop();

      // Clear stage children
      while (this.stage && this.stage.children && this.stage.children.length > 0) {
        const child = this.stage.children[0];
        this.stage.removeChild(child);
        if (child.destroy) {
          try {
            child.destroy({ children: true, texture: false });
          } catch {
            // Ignore errors during destruction
          }
        }
      }

      try {
        this.app.destroy(false);
      } catch (e) {
        console.warn('Error destroying PixiJS app:', e);
      }
      this.app = null;
      this.stage = null;
    }
  }
}
