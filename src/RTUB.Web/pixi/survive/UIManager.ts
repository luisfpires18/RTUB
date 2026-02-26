/**
 * Survive Mode — UI Manager.
 *
 * HUD: timer bar, HP bar, kill/coin count, weapon slots display,
 * boss HP bar, level/biome label, minimap, stats panel.
 */
import * as PIXI from 'pixi.js';
import {
  MINIMAP_SIZE, MINIMAP_MARGIN, PLAYER_MAX_HP,
  formatTime, clamp,
} from './types';
import type { EnemyState, PlayerStats, WeaponInstance } from './types';
import type { WeaponSystem } from './WeaponSystem';
import type { UpgradeSystem } from './UpgradeSystem';

export class UIManager {
  private uiContainer: PIXI.Container;
  private vpWidth: number;
  private vpHeight: number;

  // Timer
  private timerText!: PIXI.Text;
  private timerBar!: PIXI.Graphics;
  private timerBarWidth = 0;
  private timerBarHeight = 0;
  private timerBarX = 0;
  private timerBarY = 0;

  // HP
  private hpBar!: PIXI.Graphics;
  private hpBarWidth = 0;
  private hpBarHeight = 0;
  private hpBarX = 0;
  private hpBarY = 0;
  private hpText: PIXI.Text | null = null;

  // Counters
  private killText!: PIXI.Text;
  private orbText: PIXI.Text | null = null;
  private statsText: PIXI.Text | null = null;

  // Weapon slots
  private weaponSlotContainer: PIXI.Container | null = null;
  private weaponSlotIcons: PIXI.Text[] = [];
  private weaponSlotBadges: PIXI.Text[] = [];

  // Boss HP bar
  private bossHPBarBg: PIXI.Graphics | null = null;
  private bossHPBarFill: PIXI.Graphics | null = null;
  private bossHPBarText: PIXI.Text | null = null;
  private _bossBarX = 0;
  private _bossBarY = 0;
  private _bossBarW = 0;
  private _bossBarH = 0;
  private _bossBarFinal = false;

  // Minimap
  private minimapContainer: PIXI.Container | null = null;
  private _mmPlayerDot: PIXI.Graphics | null = null;
  private _mmEnemyDots: PIXI.Graphics | null = null;
  private _mmVpRect: PIXI.Graphics | null = null;

  // Level display
  private levelText: PIXI.Text | null = null;
  private playerLevelText: PIXI.Text | null = null;

  constructor(uiContainer: PIXI.Container, vpWidth: number, vpHeight: number) {
    this.uiContainer = uiContainer;
    this.vpWidth = vpWidth;
    this.vpHeight = vpHeight;
  }

  create(level: number, biomeName: string, timerDuration: number, maxHP: number): void {
    // ─── Timer bar (top center) ───────────────────────────
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
    this.uiContainer.addChild(this.timerBar);

    this.timerText = new PIXI.Text({
      text: formatTime(timerDuration),
      style: { fontFamily: 'Arial', fontSize: 14, fontWeight: 'bold', fill: 0xffffff, align: 'center' },
    });
    this.timerText.anchor.set(0.5, 0.5);
    this.timerText.position.set(this.vpWidth / 2, timerBarY + timerBarH / 2);
    this.uiContainer.addChild(this.timerText);

    // ─── HP bar (below timer) ─────────────────────────────
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
    this.uiContainer.addChild(this.hpBar);

    this.hpText = new PIXI.Text({
      text: `${maxHP}/${maxHP}`,
      style: { fontFamily: 'Arial', fontSize: 10, fontWeight: 'bold', fill: 0xffffff, align: 'center' },
    });
    this.hpText.anchor.set(0.5, 0.5);
    this.hpText.position.set(this.vpWidth / 2, hpBarY + hpBarH / 2);
    this.uiContainer.addChild(this.hpText);

    // ─── Level / Biome (top left) ─────────────────────────
    const levelBg = new PIXI.Graphics();
    levelBg.roundRect(8, 10, 170, 24, 5);
    levelBg.fill({ color: 0x000000, alpha: 0.6 });
    this.uiContainer.addChild(levelBg);

    this.levelText = new PIXI.Text({
      text: `Cap.${level} — ${biomeName}`,
      style: { fontFamily: 'Arial', fontSize: 13, fontWeight: 'bold', fill: 0xffcc00 },
    });
    this.levelText.position.set(14, 13);
    this.uiContainer.addChild(this.levelText);

    // ─── Stats panel ──────────────────────────────────────
    const statsBg = new PIXI.Graphics();
    statsBg.roundRect(8, 38, 170, 38, 5);
    statsBg.fill({ color: 0x000000, alpha: 0.5 });
    this.uiContainer.addChild(statsBg);

    this.statsText = new PIXI.Text({
      text: '',
      style: { fontFamily: 'Arial', fontSize: 10, fill: 0xcccccc, lineHeight: 13 },
    });
    this.statsText.position.set(14, 41);
    this.uiContainer.addChild(this.statsText);

    // ─── Player level indicator ───────────────────────────
    const lvlBg = new PIXI.Graphics();
    lvlBg.roundRect(8, 80, 80, 20, 5);
    lvlBg.fill({ color: 0x000000, alpha: 0.5 });
    this.uiContainer.addChild(lvlBg);

    this.playerLevelText = new PIXI.Text({
      text: 'Lv.1',
      style: { fontFamily: 'Arial', fontSize: 11, fontWeight: 'bold', fill: 0x4fc3f7 },
    });
    this.playerLevelText.position.set(14, 82);
    this.uiContainer.addChild(this.playerLevelText);

    // ─── Kill counter (top right) ─────────────────────────
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

    // ─── Coin counter ─────────────────────────────────────
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

    // ─── Weapon slots (bottom center) ─────────────────────
    this._createWeaponSlots();

    // ─── Minimap ──────────────────────────────────────────
    this._createMinimap();
  }

  // ─── Frame Update ───────────────────────────────────────────

  update(
    timeRemaining: number, timerDuration: number,
    playerHP: number, maxHP: number,
    enemiesKilled: number, xpOrbs: number,
    stats: PlayerStats, weaponSystem: WeaponSystem,
    upgradeSystem: UpgradeSystem,
  ): void {
    // Timer
    this.timerText.text = formatTime(timeRemaining);
    this._updateTimerBar(timeRemaining, timerDuration);

    // HP
    this._updateHPBar(playerHP, maxHP);
    if (this.hpText) this.hpText.text = `${Math.ceil(playerHP)}/${maxHP}`;

    // Counters
    this.killText.text = `☠ ${enemiesKilled}`;
    if (this.orbText) this.orbText.text = `🪙 ${xpOrbs}`;

    // Player level
    if (this.playerLevelText) this.playerLevelText.text = `Lv.${upgradeSystem.playerLevel}`;

    // Stats
    if (this.statsText) {
      const dmg = `⚔ DMG x${stats.damageMultiplier.toFixed(1)}`;
      const spd = `⚡ SPD x${(1 / stats.cooldownMultiplier).toFixed(1)}`;
      const rng = `🎯 RNG x${stats.rangeMultiplier.toFixed(1)}`;
      this.statsText.text = `${dmg}  ${spd}\n${rng}  🏃 ${Math.round(stats.playerSpeed)}`;
    }

    // Weapon slots
    this._updateWeaponSlots(weaponSystem);
  }

  // ─── Minimap ────────────────────────────────────────────────

  updateMinimap(
    playerX: number, playerY: number,
    camX: number, camY: number,
    vpWidth: number, vpHeight: number,
    mapWidth: number, mapHeight: number,
    enemies: EnemyState[],
  ): void {
    if (!this._mmPlayerDot || !this._mmEnemyDots || !this._mmVpRect) return;

    const scaleX = MINIMAP_SIZE / mapWidth;
    const scaleY = MINIMAP_SIZE / mapHeight;

    this._mmPlayerDot.clear();
    this._mmPlayerDot.circle(playerX * scaleX, playerY * scaleY, 3);
    this._mmPlayerDot.fill(0x4fc3f7);

    this._mmEnemyDots.clear();
    for (const enemy of enemies) {
      if (!enemy.alive) continue;
      this._mmEnemyDots.circle(enemy.x * scaleX, enemy.y * scaleY, enemy.isElite ? 2 : 1);
    }
    this._mmEnemyDots.fill(0xe53935);

    this._mmVpRect.clear();
    this._mmVpRect.rect(camX * scaleX, camY * scaleY, vpWidth * scaleX, vpHeight * scaleY);
    this._mmVpRect.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.5 });
    this._mmVpRect.stroke();
  }

  // ─── Boss HP Bar ────────────────────────────────────────────

  showBossHPBar(isFinal: boolean): void {
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
      style: { fontFamily: 'Arial', fontSize: 12, fontWeight: 'bold', fill: 0xffffff, stroke: { color: 0x000000, width: 2 } },
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

  updateBossHPBar(boss: EnemyState | null): void {
    if (!boss || !this.bossHPBarFill) return;
    const ratio = Math.max(0, boss.hp / boss.maxHp);
    const barWidth = this._bossBarW * ratio;
    this.bossHPBarFill.clear();
    if (barWidth > 0) {
      this.bossHPBarFill.roundRect(this._bossBarX, this._bossBarY, barWidth, this._bossBarH, 3);
      this.bossHPBarFill.fill(this._bossBarFinal ? 0xff2222 : 0xff6600);
    }
  }

  hideBossHPBar(): void {
    if (this.bossHPBarBg) { try { this.uiContainer.removeChild(this.bossHPBarBg); } catch { /* noop */ } this.bossHPBarBg = null; }
    if (this.bossHPBarFill) { try { this.uiContainer.removeChild(this.bossHPBarFill); } catch { /* noop */ } this.bossHPBarFill = null; }
    if (this.bossHPBarText) { try { this.uiContainer.removeChild(this.bossHPBarText); } catch { /* noop */ } this.bossHPBarText = null; }
  }

  // ─── End-Game Messages ──────────────────────────────────────

  showMessage(titleStr: string, color: number, subtitle?: string): void {
    const overlay = new PIXI.Graphics();
    overlay.rect(0, 0, this.vpWidth, this.vpHeight);
    overlay.fill({ color: 0x000000, alpha: 0.6 });
    this.uiContainer.addChild(overlay);

    const text = new PIXI.Text({
      text: titleStr,
      style: {
        fontFamily: 'Arial', fontSize: 48, fontWeight: 'bold',
        fill: color, stroke: { color: 0x000000, width: 4 }, align: 'center',
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

  // ─── Private ────────────────────────────────────────────────

  private _updateTimerBar(timeRemaining: number, timerDuration: number): void {
    if (!this.timerBar) return;
    this.timerBar.clear();
    const progress = clamp(timeRemaining / timerDuration, 0, 1);
    const barW = this.timerBarWidth * progress;

    let color: number;
    if (progress > 0.5) color = 0x43a047;
    else if (progress > 0.25) color = 0xffb300;
    else color = 0xe53935;

    this.timerBar.roundRect(this.timerBarX, this.timerBarY, barW, this.timerBarHeight, 6);
    this.timerBar.fill(color);
  }

  private _updateHPBar(playerHP: number, maxHP: number): void {
    if (!this.hpBar) return;
    this.hpBar.clear();
    const progress = clamp(playerHP / maxHP, 0, 1);
    const barW = this.hpBarWidth * progress;

    let color: number;
    if (progress > 0.6) color = 0xe53935;
    else if (progress > 0.3) color = 0xc62828;
    else color = 0xb71c1c;

    this.hpBar.roundRect(this.hpBarX, this.hpBarY, barW, this.hpBarHeight, 5);
    this.hpBar.fill(color);
  }

  private _createWeaponSlots(): void {
    this.weaponSlotContainer = new PIXI.Container();
    const slotSize = 36;
    const gap = 6;
    const totalW = 6 * slotSize + 5 * gap;
    const startX = (this.vpWidth - totalW) / 2;
    const y = this.vpHeight - slotSize - 12;

    for (let i = 0; i < 6; i++) {
      const x = startX + i * (slotSize + gap);

      // Slot background
      const bg = new PIXI.Graphics();
      bg.roundRect(x, y, slotSize, slotSize, 6);
      bg.fill({ color: 0x1a1a2e, alpha: 0.8 });
      bg.setStrokeStyle({ width: 1, color: 0x555577, alpha: 0.6 });
      bg.stroke();
      this.weaponSlotContainer.addChild(bg);

      // Icon (empty initially)
      const icon = new PIXI.Text({
        text: '',
        style: { fontSize: 20 },
      });
      icon.anchor.set(0.5, 0.5);
      icon.position.set(x + slotSize / 2, y + slotSize / 2 - 3);
      this.weaponSlotContainer.addChild(icon);
      this.weaponSlotIcons.push(icon);

      // Level badge
      const badge = new PIXI.Text({
        text: '',
        style: { fontFamily: 'Arial', fontSize: 8, fontWeight: 'bold', fill: 0xffd700 },
      });
      badge.anchor.set(0.5, 0);
      badge.position.set(x + slotSize / 2, y + slotSize - 12);
      this.weaponSlotContainer.addChild(badge);
      this.weaponSlotBadges.push(badge);
    }

    this.uiContainer.addChild(this.weaponSlotContainer);
  }

  private _updateWeaponSlots(weaponSystem: WeaponSystem): void {
    const romanNumerals = ['I', 'II', 'III', 'IV', 'V'];
    for (let i = 0; i < 6; i++) {
      if (i < weaponSystem.weapons.length) {
        const w = weaponSystem.weapons[i];
        this.weaponSlotIcons[i].text = w.def.icon;
        this.weaponSlotBadges[i].text = romanNumerals[w.level - 1] ?? `${w.level}`;
      } else {
        this.weaponSlotIcons[i].text = '';
        this.weaponSlotBadges[i].text = '';
      }
    }
  }

  private _createMinimap(): void {
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
  }
}
