/**
 * VillageScene — Main Phaser scene for the village mode.
 *
 * Renders a Travian-style top-down village with:
 *  - Circular ground area with radiating paths
 *  - 5 interactive buildings (Town Hall + 4 resource producers)
 *  - Decorative trees, shadows, and environment
 *  - Camera drag/pan and zoom
 *  - Resource production ticking
 *  - Building click → popup with info & upgrade
 */
import Phaser from 'phaser';
import type { BuildingSlot, ResourceState, ResourceTickData } from '@village-types/village-data';
import { BuildingType, ResourceType } from '@village-types/village-data';
import {
  COLORS,
  BUILDING_COLORS,
  BUILDING_ICONS,
  createDefaultBuildings,
  createDefaultResources,
  productionPerSecond,
  storageCapacity,
  upgradeCostForLevel,
} from '@village-shared/constants';
import { formatNumber } from '@village-shared/village-utils';
import { BuildingPopup } from '@village-ui/BuildingPopup';

/* ═══════════════════════════════════════════
   Constants
   ═══════════════════════════════════════════ */

const SCENE_KEY = 'VillageScene';
const WORLD_SIZE = 1200;
const GROUND_RADIUS = 480;
const CENTER_X = WORLD_SIZE / 2;
const CENTER_Y = WORLD_SIZE / 2;
const RESOURCE_TICK_MS = 1000;

/* ═══════════════════════════════════════════
   Scene
   ═══════════════════════════════════════════ */

export class VillageScene extends Phaser.Scene {
  private netRef: DotNet.DotNetObject | null = null;
  private buildings: BuildingSlot[] = [];
  private resources: ResourceState[] = [];
  private buildingContainers: Map<number, Phaser.GameObjects.Container> = new Map();
  private resourceTimer: Phaser.Time.TimerEvent | null = null;
  private popup: BuildingPopup | null = null;
  private isDragging = false;
  private dragStartX = 0;
  private dragStartY = 0;
  private smokeEmitters: Phaser.GameObjects.Particles.ParticleEmitter[] = [];

  constructor() {
    super({ key: SCENE_KEY });
  }

  init(data: { netRef: DotNet.DotNetObject }) {
    this.netRef = data.netRef;
    this.buildings = createDefaultBuildings();
    this.resources = createDefaultResources();
  }

  create() {
    // World bounds & camera
    this.cameras.main.setBounds(0, 0, WORLD_SIZE, WORLD_SIZE);
    this.cameras.main.centerOn(CENTER_X, CENTER_Y);

    // Draw environment
    this.drawGround();
    this.drawPaths();
    this.drawDecorations();

    // Draw buildings
    for (const b of this.buildings) {
      this.createBuilding(b);
    }

    // Setup camera controls
    this.setupCameraControls();

    // Start resource ticking
    this.resourceTimer = this.time.addEvent({
      delay: RESOURCE_TICK_MS,
      callback: this.tickResources,
      callbackScope: this,
      loop: true,
    });

    // Create particle texture for smoke
    this.createSmokeTexture();

    // Add smoke to production buildings
    for (const b of this.buildings) {
      if (b.type !== BuildingType.TownHall) {
        this.addSmoke(b);
      }
    }
  }

  /* ─── Ground ─── */

  private drawGround() {
    // Outer dark border
    const outerBg = this.add.graphics();
    outerBg.fillStyle(0x2a4a20, 1);
    outerBg.fillRect(0, 0, WORLD_SIZE, WORLD_SIZE);

    // Main ground circle
    const ground = this.add.graphics();
    ground.fillStyle(COLORS.groundGreen, 1);
    ground.fillCircle(CENTER_X, CENTER_Y, GROUND_RADIUS);

    // Inner lighter circle
    const inner = this.add.graphics();
    inner.fillStyle(COLORS.groundDark, 0.3);
    inner.fillCircle(CENTER_X, CENTER_Y, GROUND_RADIUS - 40);

    // Circular border ring
    const ring = this.add.graphics();
    ring.lineStyle(6, 0x7a5a30, 0.8);
    ring.strokeCircle(CENTER_X, CENTER_Y, GROUND_RADIUS);

    // Second inner ring for depth
    ring.lineStyle(2, 0x9e7a44, 0.4);
    ring.strokeCircle(CENTER_X, CENTER_Y, GROUND_RADIUS - 15);
  }

  /* ─── Paths ─── */

  private drawPaths() {
    const paths = this.add.graphics();
    paths.lineStyle(16, COLORS.pathTan, 0.7);

    // Radial paths from center to each building
    for (const b of this.buildings) {
      if (b.type === BuildingType.TownHall) continue;
      const bx = CENTER_X + b.x;
      const by = CENTER_Y + b.y;
      paths.beginPath();
      paths.moveTo(CENTER_X, CENTER_Y);
      paths.lineTo(bx, by);
      paths.strokePath();
    }

    // Path border
    const borderPaths = this.add.graphics();
    borderPaths.lineStyle(20, COLORS.pathBorder, 0.3);
    for (const b of this.buildings) {
      if (b.type === BuildingType.TownHall) continue;
      const bx = CENTER_X + b.x;
      const by = CENTER_Y + b.y;
      borderPaths.beginPath();
      borderPaths.moveTo(CENTER_X, CENTER_Y);
      borderPaths.lineTo(bx, by);
      borderPaths.strokePath();
    }

    // Central plaza circle
    const plaza = this.add.graphics();
    plaza.fillStyle(COLORS.pathTan, 0.5);
    plaza.fillCircle(CENTER_X, CENTER_Y, 50);
    plaza.lineStyle(3, COLORS.pathBorder, 0.6);
    plaza.strokeCircle(CENTER_X, CENTER_Y, 50);
  }

  /* ─── Decorations (trees, rocks, river) ─── */

  private drawDecorations() {
    // Trees around the perimeter
    const treePositions = [
      { x: -350, y: -350 }, { x: 350, y: -350 },
      { x: -350, y: 350 }, { x: 350, y: 350 },
      { x: -420, y: 0 }, { x: 420, y: 0 },
      { x: 0, y: -420 }, { x: 0, y: 420 },
      { x: -300, y: -380 }, { x: 300, y: -380 },
      { x: -380, y: -200 }, { x: 380, y: -200 },
      { x: -380, y: 200 }, { x: 380, y: 200 },
      { x: -300, y: 380 }, { x: 300, y: 380 },
      // Additional scattered trees
      { x: -450, y: -150 }, { x: 450, y: -150 },
      { x: -450, y: 150 }, { x: 450, y: 150 },
      { x: -150, y: -450 }, { x: 150, y: -450 },
      { x: -150, y: 450 }, { x: 150, y: 450 },
    ];

    for (const pos of treePositions) {
      this.drawTree(CENTER_X + pos.x, CENTER_Y + pos.y, 0.6 + Math.random() * 0.6);
    }

    // Small rocks
    const rockPositions = [
      { x: -100, y: -320 }, { x: 280, y: 300 },
      { x: -320, y: 100 }, { x: 100, y: 350 },
    ];
    for (const pos of rockPositions) {
      this.drawRock(CENTER_X + pos.x, CENTER_Y + pos.y);
    }

    // River on the right side
    this.drawRiver();
  }

  private drawTree(x: number, y: number, scale: number) {
    const g = this.add.graphics();
    // Shadow
    g.fillStyle(COLORS.shadowColor, 0.15);
    g.fillEllipse(x + 3, y + 20 * scale, 24 * scale, 10 * scale);
    // Trunk
    g.fillStyle(COLORS.treeTrunk, 1);
    g.fillRect(x - 3 * scale, y - 5 * scale, 6 * scale, 20 * scale);
    // Leaves (layered circles)
    g.fillStyle(COLORS.treeLeaves, 1);
    g.fillCircle(x, y - 15 * scale, 14 * scale);
    g.fillStyle(COLORS.treeLeavesLight, 0.7);
    g.fillCircle(x - 4 * scale, y - 20 * scale, 10 * scale);
    g.fillCircle(x + 5 * scale, y - 12 * scale, 10 * scale);
  }

  private drawRock(x: number, y: number) {
    const g = this.add.graphics();
    g.fillStyle(0x888888, 1);
    g.fillRoundedRect(x - 8, y - 5, 16, 10, 4);
    g.fillStyle(0x999999, 0.7);
    g.fillRoundedRect(x - 5, y - 8, 10, 8, 3);
  }

  private drawRiver() {
    // River waypoints — gentle S-curve along the right edge
    const W = WORLD_SIZE;
    const points = [
      { x: W - 50, y: 100 },
      { x: W - 80, y: 250 },
      { x: W - 40, y: 400 },
      { x: W - 90, y: 550 },
      { x: W - 50, y: 700 },
      { x: W - 80, y: 850 },
      { x: W - 40, y: 1000 },
      { x: W - 70, y: W - 50 },
    ];

    const drawCurve = (gfx: Phaser.GameObjects.Graphics, pts: { x: number; y: number }[]) => {
      if (pts.length < 2) return;
      gfx.beginPath();
      gfx.moveTo(pts[0].x, pts[0].y);
      // Use quadratic curves between midpoints for a smooth path
      for (let i = 0; i < pts.length - 1; i++) {
        const mx = (pts[i].x + pts[i + 1].x) / 2;
        const my = (pts[i].y + pts[i + 1].y) / 2;
        if (i === pts.length - 2) {
          // Last segment — curve to final point
          gfx.lineTo(pts[i + 1].x, pts[i + 1].y);
        } else {
          gfx.lineTo(mx, my);
        }
      }
      gfx.strokePath();
    };

    const river = this.add.graphics();
    // Main water
    river.lineStyle(18, COLORS.water, 0.6);
    drawCurve(river, points);

    // Highlight offset
    const highlight = points.map(p => ({ x: p.x + 4, y: p.y }));
    river.lineStyle(6, 0x8ec5e8, 0.3);
    drawCurve(river, highlight);
  }

  /* ─── Buildings ─── */

  private createBuilding(slot: BuildingSlot) {
    const bx = CENTER_X + slot.x;
    const by = CENTER_Y + slot.y;
    const isTownHall = slot.type === BuildingType.TownHall;
    const scale = isTownHall ? 1.3 : 1.0;
    const colors = BUILDING_COLORS[slot.type];

    const container = this.add.container(bx, by);

    // Shadow
    const shadow = this.add.graphics();
    shadow.fillStyle(COLORS.shadowColor, 0.2);
    shadow.fillEllipse(4, 30 * scale, 70 * scale, 18 * scale);
    container.add(shadow);

    // Building base (house body)
    const base = this.add.graphics();
    base.fillStyle(colors.base, 1);
    base.fillRoundedRect(-30 * scale, -10 * scale, 60 * scale, 40 * scale, 4);
    // Base outline
    base.lineStyle(2, 0x333333, 0.4);
    base.strokeRoundedRect(-30 * scale, -10 * scale, 60 * scale, 40 * scale, 4);
    container.add(base);

    // Roof (triangle)
    const roof = this.add.graphics();
    roof.fillStyle(colors.roof, 1);
    roof.beginPath();
    roof.moveTo(-35 * scale, -10 * scale);
    roof.lineTo(0, -40 * scale);
    roof.lineTo(35 * scale, -10 * scale);
    roof.closePath();
    roof.fillPath();
    // Roof outline
    roof.lineStyle(2, 0x333333, 0.3);
    roof.beginPath();
    roof.moveTo(-35 * scale, -10 * scale);
    roof.lineTo(0, -40 * scale);
    roof.lineTo(35 * scale, -10 * scale);
    roof.closePath();
    roof.strokePath();
    container.add(roof);

    // Windows
    if (isTownHall) {
      const win = this.add.graphics();
      win.fillStyle(0xffe4a0, 0.8);
      win.fillRect(-15, 2, 10, 10);
      win.fillRect(5, 2, 10, 10);
      win.fillRect(-5, 2, 10, 10);
      // Door
      win.fillStyle(0x5a3a1a, 1);
      win.fillRoundedRect(-6, 15, 12, 15, { tl: 6, tr: 6, bl: 0, br: 0 });
      container.add(win);
    } else {
      const win = this.add.graphics();
      win.fillStyle(0xffe4a0, 0.7);
      win.fillRect(-12 * scale, 2 * scale, 8 * scale, 8 * scale);
      win.fillRect(4 * scale, 2 * scale, 8 * scale, 8 * scale);
      // Door
      win.fillStyle(0x5a3a1a, 1);
      win.fillRoundedRect(-4 * scale, 14 * scale, 8 * scale, 14 * scale, { tl: 4, tr: 4, bl: 0, br: 0 });
      container.add(win);
    }

    // Building icon (emoji)
    const icon = BUILDING_ICONS[slot.type];
    const iconText = this.add.text(0, -52 * scale, icon, {
      fontSize: `${isTownHall ? 28 : 22}px`,
    }).setOrigin(0.5);
    container.add(iconText);

    // Level badge (white circle with number — Travian style)
    const badgeSize = isTownHall ? 16 : 14;
    const badge = this.add.graphics();
    badge.fillStyle(COLORS.badgeBg, 1);
    badge.fillCircle(22 * scale, -25 * scale, badgeSize);
    badge.lineStyle(2, COLORS.badgeBorder, 0.8);
    badge.strokeCircle(22 * scale, -25 * scale, badgeSize);
    container.add(badge);

    const levelText = this.add.text(22 * scale, -25 * scale, `${slot.level}`, {
      fontSize: `${isTownHall ? 16 : 14}px`,
      fontFamily: 'Arial, sans-serif',
      color: COLORS.badgeText,
      fontStyle: 'bold',
    }).setOrigin(0.5);
    container.add(levelText);

    // Name label below
    const nameLabel = this.add.text(0, 38 * scale, slot.name, {
      fontSize: '12px',
      fontFamily: 'Arial, sans-serif',
      color: COLORS.labelText,
      fontStyle: 'bold',
      stroke: COLORS.labelShadow,
      strokeThickness: 3,
    }).setOrigin(0.5, 0);
    container.add(nameLabel);

    // Make interactive
    const hitArea = new Phaser.Geom.Rectangle(-35 * scale, -45 * scale, 70 * scale, 90 * scale);
    container.setSize(70 * scale, 90 * scale);
    container.setInteractive(hitArea, Phaser.Geom.Rectangle.Contains);
    container.on('pointerover', () => {
      this.tweens.add({
        targets: container,
        scaleX: 1.08,
        scaleY: 1.08,
        duration: 150,
        ease: 'Back.easeOut',
      });
      this.input.setDefaultCursor('pointer');
    });
    container.on('pointerout', () => {
      this.tweens.add({
        targets: container,
        scaleX: 1,
        scaleY: 1,
        duration: 150,
        ease: 'Back.easeOut',
      });
      this.input.setDefaultCursor('default');
    });
    container.on('pointerup', (pointer: Phaser.Input.Pointer) => {
      // Ignore if was dragging
      if (this.isDragging) return;
      if (pointer.getDistance() > 10) return;
      this.showBuildingPopup(slot);
    });

    // Store reference
    this.buildingContainers.set(slot.id, container);

    // Idle animation — gentle bob
    this.tweens.add({
      targets: container,
      y: by - 3,
      duration: 2000 + Math.random() * 1000,
      ease: 'Sine.easeInOut',
      yoyo: true,
      repeat: -1,
    });
  }

  /* ─── Smoke Particles ─── */

  private createSmokeTexture() {
    const g = this.add.graphics();
    g.fillStyle(0xcccccc, 1);
    g.fillCircle(4, 4, 4);
    g.generateTexture('smoke', 8, 8);
    g.destroy();
  }

  private addSmoke(slot: BuildingSlot) {
    const bx = CENTER_X + slot.x;
    const by = CENTER_Y + slot.y;

    const emitter = this.add.particles(bx + 10, by - 35, 'smoke', {
      speed: { min: 5, max: 15 },
      angle: { min: 250, max: 290 },
      scale: { start: 0.5, end: 1.5 },
      alpha: { start: 0.4, end: 0 },
      lifespan: { min: 1500, max: 2500 },
      frequency: 800,
      quantity: 1,
      tint: 0xaaaaaa,
    });

    this.smokeEmitters.push(emitter);
  }

  /* ─── Building Popup ─── */

  private showBuildingPopup(slot: BuildingSlot) {
    // Close existing popup
    if (this.popup) {
      this.popup.destroy();
      this.popup = null;
    }

    const bx = CENTER_X + slot.x;
    const by = CENTER_Y + slot.y;

    this.popup = new BuildingPopup(this, bx, by - 80, slot, this.resources, (buildingId: number) => {
      this.handleUpgrade(buildingId);
    });
  }

  private handleUpgrade(buildingId: number) {
    const building = this.buildings.find(b => b.id === buildingId);
    if (!building) return;

    const cost = upgradeCostForLevel(building.upgradeCost, building.level);
    const wood = this.resources.find(r => r.type === ResourceType.Wood)!;
    const clay = this.resources.find(r => r.type === ResourceType.Clay)!;
    const iron = this.resources.find(r => r.type === ResourceType.Iron)!;
    const crop = this.resources.find(r => r.type === ResourceType.Crop)!;

    // Check if player can afford
    if (wood.amount < cost.wood || clay.amount < cost.clay ||
        iron.amount < cost.iron || crop.amount < cost.crop) {
      return; // Can't afford — button should already be disabled
    }

    // Deduct resources
    wood.amount -= cost.wood;
    clay.amount -= cost.clay;
    iron.amount -= cost.iron;
    crop.amount -= cost.crop;

    // Upgrade building
    building.level++;

    // Update production rate for resource buildings
    const resourceMap: Record<string, ResourceType> = {
      [BuildingType.LumberMill]: ResourceType.Wood,
      [BuildingType.ClayPit]: ResourceType.Clay,
      [BuildingType.IronMine]: ResourceType.Iron,
      [BuildingType.Farm]: ResourceType.Crop,
    };
    const resType = resourceMap[building.type];
    if (resType) {
      const res = this.resources.find(r => r.type === resType)!;
      res.production = productionPerSecond(building.level);
    }

    // If town hall, update all capacities
    if (building.type === BuildingType.TownHall) {
      const cap = storageCapacity(building.level);
      for (const r of this.resources) {
        r.capacity = cap;
      }
    }

    // Rebuild the building visual
    const container = this.buildingContainers.get(buildingId);
    if (container) {
      container.destroy();
      this.buildingContainers.delete(buildingId);
      this.createBuilding(building);
    }

    // Upgrade animation — flash effect
    const flash = this.add.graphics();
    flash.fillStyle(0xffff00, 0.4);
    const bx = CENTER_X + building.x;
    const by = CENTER_Y + building.y;
    flash.fillCircle(bx, by, 50);
    this.tweens.add({
      targets: flash,
      alpha: 0,
      duration: 600,
      onComplete: () => flash.destroy(),
    });

    // Floating "+1 Lv" text
    const lvText = this.add.text(bx, by - 60, `⬆ Nv. ${building.level}`, {
      fontSize: '16px',
      fontFamily: 'Arial, sans-serif',
      color: '#ffdd00',
      fontStyle: 'bold',
      stroke: '#000000',
      strokeThickness: 3,
    }).setOrigin(0.5);
    this.tweens.add({
      targets: lvText,
      y: by - 110,
      alpha: 0,
      duration: 1200,
      ease: 'Cubic.easeOut',
      onComplete: () => lvText.destroy(),
    });

    // Close popup and reopen with new data
    if (this.popup) {
      this.popup.destroy();
      this.popup = null;
    }
    this.showBuildingPopup(building);

    // Notify Blazor
    this.pushResourcesToBlazor();
  }

  /* ─── Resource Ticking ─── */

  private tickResources() {
    for (const r of this.resources) {
      r.amount = Math.min(r.amount + r.production, r.capacity);
    }

    // Push to Blazor every tick
    this.pushResourcesToBlazor();

    // Floating production text on a random production building
    this.showProductionFloat();
  }

  private pushResourcesToBlazor() {
    if (!this.netRef) return;
    const data: ResourceTickData = {
      wood: Math.floor(this.resources.find(r => r.type === ResourceType.Wood)!.amount),
      clay: Math.floor(this.resources.find(r => r.type === ResourceType.Clay)!.amount),
      iron: Math.floor(this.resources.find(r => r.type === ResourceType.Iron)!.amount),
      crop: Math.floor(this.resources.find(r => r.type === ResourceType.Crop)!.amount),
    };
    this.netRef.invokeMethodAsync('UpdateResources', data.wood, data.clay, data.iron, data.crop).catch(() => {});
  }

  private showProductionFloat() {
    // Pick a random resource building
    const prodBuildings = this.buildings.filter(b => b.type !== BuildingType.TownHall);
    const b = prodBuildings[Math.floor(Math.random() * prodBuildings.length)];
    if (!b) return;

    const resMap: Record<string, { icon: string; color: string }> = {
      [BuildingType.LumberMill]: { icon: '🪵', color: '#d4a843' },
      [BuildingType.ClayPit]: { icon: '🧱', color: '#c2703a' },
      [BuildingType.IronMine]: { icon: '⛏️', color: '#7a8b99' },
      [BuildingType.Farm]: { icon: '🌾', color: '#6b9e3a' },
    };
    const info = resMap[b.type];
    if (!info) return;

    const bx = CENTER_X + b.x;
    const by = CENTER_Y + b.y;
    const prod = productionPerSecond(b.level);

    const txt = this.add.text(bx + (Math.random() - 0.5) * 30, by - 20, `${info.icon}+${formatNumber(prod)}`, {
      fontSize: '13px',
      fontFamily: 'Arial, sans-serif',
      color: info.color,
      fontStyle: 'bold',
      stroke: '#000000',
      strokeThickness: 2,
    }).setOrigin(0.5);

    this.tweens.add({
      targets: txt,
      y: by - 65,
      alpha: 0,
      duration: 1500,
      ease: 'Cubic.easeOut',
      onComplete: () => txt.destroy(),
    });
  }

  /* ─── Camera Controls ─── */

  private setupCameraControls() {
    // Drag to pan
    this.input.on('pointerdown', (pointer: Phaser.Input.Pointer) => {
      this.isDragging = false;
      this.dragStartX = pointer.x;
      this.dragStartY = pointer.y;
    });

    this.input.on('pointermove', (pointer: Phaser.Input.Pointer) => {
      if (!pointer.isDown) return;
      const dx = pointer.x - this.dragStartX;
      const dy = pointer.y - this.dragStartY;
      if (Math.abs(dx) > 5 || Math.abs(dy) > 5) {
        this.isDragging = true;
      }
      if (this.isDragging) {
        this.cameras.main.scrollX -= (pointer.x - pointer.prevPosition.x) / this.cameras.main.zoom;
        this.cameras.main.scrollY -= (pointer.y - pointer.prevPosition.y) / this.cameras.main.zoom;
      }
    });

    // Zoom with mouse wheel
    this.input.on('wheel', (_pointer: Phaser.Input.Pointer, _gameObjects: unknown[], _deltaX: number, deltaY: number) => {
      const cam = this.cameras.main;
      const newZoom = Phaser.Math.Clamp(cam.zoom - deltaY * 0.001, 0.5, 2.0);
      cam.setZoom(newZoom);
    });

    // Pinch zoom for mobile
    this.input.addPointer(1); // Support 2 pointers
  }

  /* ─── Cleanup ─── */

  destroy() {
    if (this.resourceTimer) {
      this.resourceTimer.destroy();
      this.resourceTimer = null;
    }
    if (this.popup) {
      this.popup.destroy();
      this.popup = null;
    }
    for (const emitter of this.smokeEmitters) {
      emitter.destroy();
    }
    this.smokeEmitters = [];
    this.buildingContainers.clear();
    this.netRef = null;
  }
}
