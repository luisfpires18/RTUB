/**
 * Survive Mode — Upgrade System.
 *
 * Manages level-up popups offering: new weapons, weapon level-ups, passive upgrades,
 * and weapon evolutions (combining two max-level weapons into a powerful evolved form).
 * Mirrors survivor.io's pick-3-choose-1 + evolution flow.
 */
import * as PIXI from 'pixi.js';
import type { WeaponDef, WeaponInstance, PassiveDef, EvolutionDef, UpgradeChoice, PlayerStats } from './types';
import { UpgradeChoiceType } from './types';
import { ALL_WEAPONS, MAX_WEAPON_SLOTS, MAX_WEAPON_LEVEL, EVOLUTION_RECIPES } from './weapons/WeaponDefs';
import type { WeaponSystem } from './WeaponSystem';
import type { AudioManager } from './AudioManager';

// ─── XP Level Thresholds ──────────────────────────────────────

const LEVEL_THRESHOLDS = [5, 12, 22, 35, 55, 80];
const LEVEL_THRESHOLD_STEP = 35;

// ─── Passive Definitions ──────────────────────────────────────

const PASSIVE_DEFS: PassiveDef[] = [
  {
    id: 'moveSpeed', icon: '🏃', name: 'Swift Feet',
    description: '+12% velocidade de movimento', maxLevel: 5,
    apply: (s) => { s.playerSpeed *= 1.12; },
  },
  {
    id: 'damage', icon: '⚔️', name: 'Brute Force',
    description: '+15% dano total', maxLevel: 5,
    apply: (s) => { s.damageMultiplier *= 1.15; },
  },
  {
    id: 'hp', icon: '❤️', name: 'Vitality',
    description: '+15 HP máximo e cura 10', maxLevel: 5,
    apply: (s) => { s.maxHP += 15; s.playerHP = Math.min(s.playerHP + 10, s.maxHP); },
  },
  {
    id: 'atkRange', icon: '🎯', name: 'Eagle Eye',
    description: '+12% alcance', maxLevel: 5,
    apply: (s) => { s.rangeMultiplier *= 1.12; },
  },
  {
    id: 'atkSpeed', icon: '⚡', name: 'Rapid Fire',
    description: '+12% velocidade de ataque', maxLevel: 5,
    apply: (s) => { s.cooldownMultiplier *= 0.88; },
  },
  {
    id: 'armor', icon: '🛡️', name: 'Armor',
    description: '-8% dano recebido', maxLevel: 5,
    apply: (s) => { s.armor = 1 - (1 - s.armor) * 0.92; },
  },
  {
    id: 'magnet', icon: '🧲', name: 'Coin Magnet',
    description: '+80 raio de recolha', maxLevel: 3,
    apply: (s) => { s.magnetRadius += 80; },
  },
  {
    id: 'coinRate', icon: '🪙', name: 'Gold Rush',
    description: '2x moedas por inimigo', maxLevel: 3,
    apply: (s) => { s.coinDropMult *= 2; },
  },
];

// ─── Upgrade System ───────────────────────────────────────────

export class UpgradeSystem {
  /** Number of XP orbs collected this run. */
  xpOrbsCollected = 0;

  /** Player level (starts at 1). */
  playerLevel = 1;

  /** Is the upgrade popup showing? */
  paused = false;

  // Private
  private nextLevelAt: number;
  private levelIndex = 0;
  private passiveLevels: Record<string, number> = {};
  private overlay: PIXI.Container | null = null;

  private weaponSystem: WeaponSystem;
  private stats: PlayerStats;
  private audio: AudioManager;
  private uiContainer: PIXI.Container;
  private vpWidth: number;
  private vpHeight: number;

  /** Emitted when player picks an upgrade (for UI refresh). */
  onUpgradePicked: (() => void) | null = null;

  constructor(
    weaponSystem: WeaponSystem,
    stats: PlayerStats,
    audio: AudioManager,
    uiContainer: PIXI.Container,
    vpWidth: number,
    vpHeight: number,
  ) {
    this.weaponSystem = weaponSystem;
    this.stats = stats;
    this.audio = audio;
    this.uiContainer = uiContainer;
    this.vpWidth = vpWidth;
    this.vpHeight = vpHeight;
    this.nextLevelAt = LEVEL_THRESHOLDS[0];
  }

  /** Called when an XP orb is collected. Returns true if level-up triggered. */
  collectOrb(): boolean {
    this.xpOrbsCollected++;
    if (this.xpOrbsCollected >= this.nextLevelAt) {
      this._advanceLevel();
      this.showUpgradePopup();
      return true;
    }
    return false;
  }

  private _advanceLevel(): void {
    this.playerLevel++;
    this.levelIndex++;
    if (this.levelIndex < LEVEL_THRESHOLDS.length) {
      this.nextLevelAt = LEVEL_THRESHOLDS[this.levelIndex];
    } else {
      this.nextLevelAt += LEVEL_THRESHOLD_STEP;
    }
  }

  /** Build 3 upgrade choices and show the popup. */
  showUpgradePopup(): void {
    if (this.paused) return;
    this.paused = true;

    const choices = this._buildChoices(3);
    if (choices.length === 0) { this.paused = false; return; }

    this.audio.playSFX('levelup');
    this._renderPopup(choices);
  }

  /** Check which evolutions are currently available (both ingredients at max level). */
  private _getAvailableEvolutions(): EvolutionDef[] {
    const owned = new Map(this.weaponSystem.weapons.map(w => [w.def.id, w]));
    return EVOLUTION_RECIPES.filter(evo => {
      // Neither ingredient already evolved (result not already owned)
      if (owned.has(evo.result.id)) return false;
      const a = owned.get(evo.ingredientA);
      const b = owned.get(evo.ingredientB);
      return a && b && a.level >= MAX_WEAPON_LEVEL && b.level >= MAX_WEAPON_LEVEL;
    });
  }

  private _buildChoices(count: number): UpgradeChoice[] {
    const pool: UpgradeChoice[] = [];

    // ── Evolutions take priority (guaranteed slot if available) ──
    const evolutions = this._getAvailableEvolutions();
    const evoChoices: UpgradeChoice[] = evolutions.map(evo => ({
      type: UpgradeChoiceType.Evolution,
      evolutionDef: evo,
      weaponDef: evo.result,
      icon: evo.result.icon,
      title: `⟐ ${evo.result.name}`,
      desc: evo.description,
    }));

    // New weapons (if we have slots)
    if (!this.weaponSystem.isFull) {
      const owned = new Set(this.weaponSystem.weapons.map(w => w.def.id));
      const available = ALL_WEAPONS.filter(w => !owned.has(w.id));
      for (const wDef of available) {
        pool.push({
          type: UpgradeChoiceType.NewWeapon,
          weaponDef: wDef,
          icon: wDef.icon,
          title: wDef.name,
          desc: wDef.description,
        });
      }
    }

    // Weapon level-ups (for owned weapons not at max, skip evolved weapons which have 1 level)
    for (const weapon of this.weaponSystem.weapons) {
      if (weapon.level < MAX_WEAPON_LEVEL && weapon.def.levels.length > 1) {
        const nextStats = weapon.def.levels[weapon.level]; // level is 1-based, so levels[level] is next
        const desc = nextStats
          ? `Nível ${weapon.level + 1} — +dano, +efeito`
          : `Nível ${weapon.level + 1}`;
        pool.push({
          type: UpgradeChoiceType.WeaponLevelUp,
          weaponDef: weapon.def,
          icon: weapon.def.icon,
          title: `${weapon.def.name} ↑`,
          desc,
        });
      }
    }

    // Passive upgrades
    for (const passive of PASSIVE_DEFS) {
      const current = this.passiveLevels[passive.id] ?? 0;
      if (current < passive.maxLevel) {
        pool.push({
          type: UpgradeChoiceType.Passive,
          passiveDef: passive,
          icon: passive.icon,
          title: `${passive.name} ${current > 0 ? `(${current + 1})` : ''}`,
          desc: passive.description,
        });
      }
    }

    // Shuffle and pick `count`
    const shuffled = pool.sort(() => Math.random() - 0.5);

    // Build result — evolutions first (guaranteed), then fill remaining slots
    const result: UpgradeChoice[] = [];

    // Guarantee evolution choices (up to count)
    for (const evo of evoChoices) {
      if (result.length >= count) break;
      result.push(evo);
    }

    // Guarantee one new weapon if early game and not full
    const newWeapons = shuffled.filter(c => c.type === UpgradeChoiceType.NewWeapon);
    const others = shuffled.filter(c => c.type !== UpgradeChoiceType.NewWeapon);

    if (result.length < count && newWeapons.length > 0 && !this.weaponSystem.isFull && this.weaponSystem.weaponCount < 4) {
      result.push(newWeapons[0]);
    }

    const remaining = [...newWeapons.slice(result.some(r => r.type === UpgradeChoiceType.NewWeapon) ? 1 : 0), ...others];
    const shuffledRemaining = remaining.sort(() => Math.random() - 0.5);
    for (const choice of shuffledRemaining) {
      if (result.length >= count) break;
      if (!result.includes(choice)) result.push(choice);
    }

    return result.slice(0, count);
  }

  private _applyChoice(choice: UpgradeChoice): void {
    switch (choice.type) {
      case UpgradeChoiceType.NewWeapon:
        if (choice.weaponDef) this.weaponSystem.addWeapon(choice.weaponDef);
        break;
      case UpgradeChoiceType.WeaponLevelUp:
        if (choice.weaponDef) this.weaponSystem.levelUpWeapon(choice.weaponDef.id);
        break;
      case UpgradeChoiceType.Passive:
        if (choice.passiveDef) {
          choice.passiveDef.apply(this.stats, (this.passiveLevels[choice.passiveDef.id] ?? 0) + 1);
          this.passiveLevels[choice.passiveDef.id] = (this.passiveLevels[choice.passiveDef.id] ?? 0) + 1;
        }
        break;
      case UpgradeChoiceType.Evolution:
        if (choice.evolutionDef) {
          this.weaponSystem.evolveWeapons(
            choice.evolutionDef.ingredientA,
            choice.evolutionDef.ingredientB,
            choice.evolutionDef.result,
          );
        }
        break;
    }
    this.onUpgradePicked?.();
  }

  // ─── Popup Rendering ────────────────────────────────────────

  private _renderPopup(choices: UpgradeChoice[]): void {
    const overlay = new PIXI.Container();

    // Background dim
    const bg = new PIXI.Graphics();
    bg.rect(0, 0, this.vpWidth, this.vpHeight);
    bg.fill({ color: 0x000000, alpha: 0.75 });
    bg.eventMode = 'static';
    overlay.addChild(bg);

    // Title
    const titleText = new PIXI.Text({
      text: `NÍVEL ${this.playerLevel} — ESCOLHE`,
      style: {
        fontFamily: 'Arial', fontSize: 22, fontWeight: 'bold',
        fill: 0xffd700, align: 'center',
        dropShadow: { color: 0x000000, blur: 4, distance: 2 },
      },
    });
    titleText.anchor.set(0.5, 0.5);
    titleText.position.set(this.vpWidth / 2, this.vpHeight * 0.15);
    overlay.addChild(titleText);

    // Cards
    const cardW = Math.min(140, (this.vpWidth - 60) / 3);
    const cardH = 180;
    const gap = 12;
    const totalW = cardW * choices.length + gap * (choices.length - 1);
    const startX = (this.vpWidth - totalW) / 2;
    const cardY = this.vpHeight / 2 - cardH / 2;

    choices.forEach((choice, idx) => {
      const cx = startX + idx * (cardW + gap);
      const card = new PIXI.Container();
      card.eventMode = 'static';
      card.cursor = 'pointer';

      // Card color varies by type
      const borderColor = choice.type === UpgradeChoiceType.Evolution ? 0xff6e40
        : choice.type === UpgradeChoiceType.NewWeapon ? 0x4fc3f7
          : choice.type === UpgradeChoiceType.WeaponLevelUp ? 0xffd700
            : 0x81c784;

      const cardBg = new PIXI.Graphics();
      cardBg.roundRect(0, 0, cardW, cardH, 10);
      cardBg.fill({ color: 0x1a1a3e, alpha: 0.95 });
      cardBg.setStrokeStyle({ width: 2, color: borderColor, alpha: 0.8 });
      cardBg.stroke();
      card.addChild(cardBg);

      const hoverBg = new PIXI.Graphics();
      hoverBg.roundRect(0, 0, cardW, cardH, 10);
      hoverBg.fill({ color: 0x2a2a5e, alpha: 0.95 });
      hoverBg.setStrokeStyle({ width: 3, color: 0xffee55 });
      hoverBg.stroke();
      hoverBg.visible = false;
      card.addChild(hoverBg);

      // Type badge
      const badgeText = choice.type === UpgradeChoiceType.Evolution ? '⟐ EVOLUÇÃO'
        : choice.type === UpgradeChoiceType.NewWeapon ? 'NOVA ARMA'
          : choice.type === UpgradeChoiceType.WeaponLevelUp ? 'UPGRADE'
            : 'PASSIVO';
      const badge = new PIXI.Text({
        text: badgeText,
        style: { fontFamily: 'Arial', fontSize: 8, fontWeight: 'bold', fill: borderColor },
      });
      badge.anchor.set(0.5, 0);
      badge.position.set(cardW / 2, 6);
      card.addChild(badge);

      const icon = new PIXI.Text({ text: choice.icon, style: { fontSize: 36 } });
      icon.anchor.set(0.5, 0.5);
      icon.position.set(cardW / 2, 45);
      card.addChild(icon);

      const tText = new PIXI.Text({
        text: choice.title,
        style: {
          fontFamily: 'Arial', fontSize: 13, fontWeight: 'bold',
          fill: 0xffffff, align: 'center',
          wordWrap: true, wordWrapWidth: cardW - 16,
        },
      });
      tText.anchor.set(0.5, 0);
      tText.position.set(cardW / 2, 70);
      card.addChild(tText);

      const dText = new PIXI.Text({
        text: choice.desc,
        style: {
          fontFamily: 'Arial', fontSize: 10,
          fill: 0xbbbbbb, align: 'center',
          wordWrap: true, wordWrapWidth: cardW - 16,
        },
      });
      dText.anchor.set(0.5, 0);
      dText.position.set(cardW / 2, 100);
      card.addChild(dText);

      card.on('pointerdown', () => {
        this._applyChoice(choice);
        this.uiContainer.removeChild(overlay);
        this.overlay = null;
        this.paused = false;
      });

      card.on('pointerover', () => { hoverBg.visible = true; cardBg.visible = false; });
      card.on('pointerout', () => { hoverBg.visible = false; cardBg.visible = true; });

      card.position.set(cx, cardY);
      overlay.addChild(card);
    });

    this.overlay = overlay;
    this.uiContainer.addChild(overlay);
  }

  cleanup(): void {
    if (this.overlay) {
      try { this.uiContainer.removeChild(this.overlay); } catch { /* noop */ }
      this.overlay = null;
    }
    this.paused = false;
  }
}
