/**
 * VillageScene — Main Phaser scene for the village mode.
 *
 * Polished medieval village with:
 *  - Layered terrain with grass texture, stone walls, moat
 *  - Cobblestone paths with edge detail
 *  - Detailed multi-layer buildings with stone foundations, timber frames, varied roofs
 *  - Varied tree species (oak, pine, bush), flowers, ambient details
 *  - Torches, flags, and atmospheric particles
 *  - Camera drag/pan and zoom
 *  - Resource production ticking
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

const SCENE_KEY = 'VillageScene';
const WORLD_SIZE = 1400;
const CENTER_X = WORLD_SIZE / 2;
const CENTER_Y = WORLD_SIZE / 2;
const VILLAGE_RADIUS = 500;
const RESOURCE_TICK_MS = 1000;

// Seeded random for consistent decoration placement
function seededRandom(seed: number): () => number {
  let s = seed;
  return () => {
    s = (s * 16807 + 0) % 2147483647;
    return (s - 1) / 2147483646;
  };
}

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
    this.cameras.main.setBounds(0, 0, WORLD_SIZE, WORLD_SIZE);
    this.cameras.main.centerOn(CENTER_X, CENTER_Y);

    this.createSmokeTexture();
    this.createGlowTexture();

    this.drawTerrain();
    this.drawWall();
    this.drawPaths();
    this.drawDecorations();

    for (const b of this.buildings) {
      this.createBuilding(b);
    }

    this.drawAmbientDetails();
    this.setupCameraControls();

    this.resourceTimer = this.time.addEvent({
      delay: RESOURCE_TICK_MS,
      callback: this.tickResources,
      callbackScope: this,
      loop: true,
    });
  }

  /* ═══════════════════════════════════════════
     Texture generation
     ═══════════════════════════════════════════ */

  private createSmokeTexture() {
    const g = this.add.graphics();
    g.fillStyle(0xdddddd, 1);
    g.fillCircle(8, 8, 8);
    g.fillStyle(0xcccccc, 0.6);
    g.fillCircle(6, 6, 5);
    g.generateTexture('smoke', 16, 16);
    g.destroy();
  }

  private createGlowTexture() {
    const g = this.add.graphics();
    g.fillStyle(0xffaa33, 1);
    g.fillCircle(6, 6, 6);
    g.fillStyle(0xffdd88, 0.5);
    g.fillCircle(6, 6, 3);
    g.generateTexture('glow', 12, 12);
    g.destroy();
  }

  /* ═══════════════════════════════════════════
     Terrain
     ═══════════════════════════════════════════ */

  private drawTerrain() {
    const g = this.add.graphics();

    // Deep forest background
    g.fillStyle(0x1e3a14, 1);
    g.fillRect(0, 0, WORLD_SIZE, WORLD_SIZE);

    // Outer terrain ring — darker grass around the village
    g.fillStyle(0x2d5420, 1);
    g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS + 120);

    // Moat / ditch
    g.fillStyle(0x3a6e55, 0.4);
    g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS + 30);
    g.lineStyle(8, 0x2a4a3a, 0.5);
    g.strokeCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS + 30);

    // Main village ground — rich grass
    g.fillStyle(0x4a8035, 1);
    g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS);

    // Inner grass variation rings
    g.fillStyle(0x52903a, 0.4);
    g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS - 60);
    g.fillStyle(0x5a9a40, 0.25);
    g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS - 150);

    // Scattered grass tufts for texture
    const rng = seededRandom(42);
    for (let i = 0; i < 120; i++) {
      const angle = rng() * Math.PI * 2;
      const dist = rng() * (VILLAGE_RADIUS - 30);
      const gx = CENTER_X + Math.cos(angle) * dist;
      const gy = CENTER_Y + Math.sin(angle) * dist;
      const shade = 0x3a7a2a + Math.floor(rng() * 0x202020);
      g.fillStyle(shade, 0.15 + rng() * 0.15);
      g.fillCircle(gx, gy, 4 + rng() * 8);
    }
  }

  /* ═══════════════════════════════════════════
     Stone & Timber Wall
     ═══════════════════════════════════════════ */

  private drawWall() {
    const g = this.add.graphics();
    const R = VILLAGE_RADIUS + 10;

    // Outer wall shadow
    g.lineStyle(14, 0x000000, 0.15);
    g.strokeCircle(CENTER_X + 3, CENTER_Y + 3, R);

    // Main stone wall — wide
    g.lineStyle(12, 0x6b5a3e, 1);
    g.strokeCircle(CENTER_X, CENTER_Y, R);

    // Wall highlight (inner edge)
    g.lineStyle(3, 0x8a7a5a, 0.6);
    g.strokeCircle(CENTER_X, CENTER_Y, R - 5);

    // Wall dark line (outer edge)
    g.lineStyle(2, 0x4a3a22, 0.7);
    g.strokeCircle(CENTER_X, CENTER_Y, R + 5);

    // Stone block pattern on wall
    const rng = seededRandom(99);
    for (let i = 0; i < 60; i++) {
      const angle = (i / 60) * Math.PI * 2;
      const wx = CENTER_X + Math.cos(angle) * R;
      const wy = CENTER_Y + Math.sin(angle) * R;
      g.lineStyle(1, 0x4a3a22, 0.4);
      g.beginPath();
      g.moveTo(wx - 5 + rng() * 3, wy - 6);
      g.lineTo(wx - 5 + rng() * 3, wy + 6);
      g.strokePath();
    }

    // Gate towers (4 cardinal points where paths exit)
    const gateAngles = [
      Math.atan2(-1, -1), // top-left (LumberMill)
      Math.atan2(-1, 1),  // top-right (ClayPit)
      Math.atan2(1, -1),  // bottom-left (IronMine)
      Math.atan2(1, 1),   // bottom-right (Farm)
    ];

    for (const angle of gateAngles) {
      const tx = CENTER_X + Math.cos(angle) * R;
      const ty = CENTER_Y + Math.sin(angle) * R;
      this.drawGateTower(tx, ty);
    }
  }

  private drawGateTower(x: number, y: number) {
    const g = this.add.graphics();
    // Tower shadow
    g.fillStyle(0x000000, 0.15);
    g.fillRoundedRect(x - 13, y - 13, 30, 30, 4);
    // Tower base
    g.fillStyle(0x7a6a4e, 1);
    g.fillRoundedRect(x - 15, y - 15, 30, 30, 4);
    // Tower highlight
    g.fillStyle(0x8a7a5e, 0.6);
    g.fillRoundedRect(x - 13, y - 13, 26, 14, 3);
    // Tower top / crenellation
    g.fillStyle(0x6a5a3e, 1);
    g.fillRect(x - 15, y - 18, 6, 5);
    g.fillRect(x - 3, y - 18, 6, 5);
    g.fillRect(x + 9, y - 18, 6, 5);
    // Outline
    g.lineStyle(1.5, 0x3a2a15, 0.6);
    g.strokeRoundedRect(x - 15, y - 15, 30, 30, 4);
  }

  /* ═══════════════════════════════════════════
     Paths
     ═══════════════════════════════════════════ */

  private drawPaths() {
    const g = this.add.graphics();

    for (const b of this.buildings) {
      if (b.type === BuildingType.TownHall) continue;
      const bx = CENTER_X + b.x;
      const by = CENTER_Y + b.y;

      // Path shadow
      g.lineStyle(22, 0x000000, 0.08);
      g.beginPath(); g.moveTo(CENTER_X + 2, CENTER_Y + 2); g.lineTo(bx + 2, by + 2); g.strokePath();

      // Path base (dirt underneath)
      g.lineStyle(20, 0x7a6840, 0.7);
      g.beginPath(); g.moveTo(CENTER_X, CENTER_Y); g.lineTo(bx, by); g.strokePath();

      // Path surface (cobblestone color)
      g.lineStyle(16, 0x9a8a66, 0.8);
      g.beginPath(); g.moveTo(CENTER_X, CENTER_Y); g.lineTo(bx, by); g.strokePath();

      // Path highlight (lighter center line)
      g.lineStyle(6, 0xb0a07a, 0.3);
      g.beginPath(); g.moveTo(CENTER_X, CENTER_Y); g.lineTo(bx, by); g.strokePath();

      // Cobblestone dots along path
      const dx = bx - CENTER_X;
      const dy = by - CENTER_Y;
      const len = Math.sqrt(dx * dx + dy * dy);
      const nx = dx / len;
      const ny = dy / len;
      const perpX = -ny;
      const perpY = nx;
      const rng = seededRandom(b.id * 777);
      for (let d = 20; d < len - 20; d += 14) {
        const px = CENTER_X + nx * d + perpX * (rng() - 0.5) * 10;
        const py = CENTER_Y + ny * d + perpY * (rng() - 0.5) * 10;
        g.lineStyle(1, 0x6a5a40, 0.2 + rng() * 0.15);
        g.strokeCircle(px, py, 2 + rng() * 2);
      }
    }

    // Central plaza — stone circle
    const plaza = this.add.graphics();
    plaza.fillStyle(0x000000, 0.1);
    plaza.fillCircle(CENTER_X + 2, CENTER_Y + 2, 58);
    plaza.fillStyle(0x8a7a5a, 0.8);
    plaza.fillCircle(CENTER_X, CENTER_Y, 56);
    plaza.fillStyle(0x9e8e6e, 0.6);
    plaza.fillCircle(CENTER_X, CENTER_Y, 48);
    plaza.fillStyle(0xb0a080, 0.3);
    plaza.fillCircle(CENTER_X, CENTER_Y, 30);
    plaza.lineStyle(2, 0x6a5a40, 0.4);
    plaza.strokeCircle(CENTER_X, CENTER_Y, 56);
    plaza.lineStyle(1, 0x7a6a50, 0.3);
    plaza.strokeCircle(CENTER_X, CENTER_Y, 40);
  }

  /* ═══════════════════════════════════════════
     Decorations
     ═══════════════════════════════════════════ */

  private drawDecorations() {
    const rng = seededRandom(123);

    // Dense forest outside the walls
    for (let i = 0; i < 80; i++) {
      const angle = rng() * Math.PI * 2;
      const dist = VILLAGE_RADIUS + 60 + rng() * 200;
      const tx = CENTER_X + Math.cos(angle) * dist;
      const ty = CENTER_Y + Math.sin(angle) * dist;
      if (tx < 30 || tx > WORLD_SIZE - 80 || ty < 30 || ty > WORLD_SIZE - 30) continue;
      if (rng() > 0.4) {
        this.drawPineTree(tx, ty, 0.7 + rng() * 0.5);
      } else {
        this.drawOakTree(tx, ty, 0.6 + rng() * 0.5);
      }
    }

    // Trees inside village (sparse, near walls)
    const innerTreeAngles = [0.3, 0.9, 1.5, 2.1, 2.7, 3.3, 3.9, 4.5, 5.1, 5.7];
    for (const a of innerTreeAngles) {
      const dist = VILLAGE_RADIUS - 40 - rng() * 60;
      const tx = CENTER_X + Math.cos(a) * dist;
      const ty = CENTER_Y + Math.sin(a) * dist;
      this.drawOakTree(tx, ty, 0.5 + rng() * 0.3);
    }

    // Bushes scattered inside
    for (let i = 0; i < 25; i++) {
      const angle = rng() * Math.PI * 2;
      const dist = 100 + rng() * (VILLAGE_RADIUS - 130);
      const bx = CENTER_X + Math.cos(angle) * dist;
      const by = CENTER_Y + Math.sin(angle) * dist;
      const tooClose = this.buildings.some(b => {
        const bdx = (CENTER_X + b.x) - bx;
        const bdy = (CENTER_Y + b.y) - by;
        return Math.sqrt(bdx * bdx + bdy * bdy) < 80;
      });
      if (!tooClose) {
        this.drawBush(bx, by, 0.5 + rng() * 0.6);
      }
    }

    // Flower patches
    for (let i = 0; i < 15; i++) {
      const angle = rng() * Math.PI * 2;
      const dist = 80 + rng() * (VILLAGE_RADIUS - 120);
      const fx = CENTER_X + Math.cos(angle) * dist;
      const fy = CENTER_Y + Math.sin(angle) * dist;
      const tooClose = this.buildings.some(b => {
        const bdx = (CENTER_X + b.x) - fx;
        const bdy = (CENTER_Y + b.y) - fy;
        return Math.sqrt(bdx * bdx + bdy * bdy) < 70;
      });
      if (!tooClose) {
        this.drawFlowers(fx, fy, rng);
      }
    }

    // Rocks
    const rockPositions = [
      { x: -100, y: -340 }, { x: 280, y: 320 },
      { x: -340, y: 100 }, { x: 100, y: 370 },
      { x: 350, y: -100 }, { x: -200, y: 280 },
    ];
    for (const pos of rockPositions) {
      this.drawRockCluster(CENTER_X + pos.x, CENTER_Y + pos.y, rng);
    }

    // River on the right
    this.drawRiver();
  }

  private drawPineTree(x: number, y: number, scale: number) {
    const g = this.add.graphics();
    g.fillStyle(0x000000, 0.12);
    g.fillEllipse(x + 3, y + 22 * scale, 18 * scale, 8 * scale);
    g.fillStyle(0x5a3a1a, 1);
    g.fillRect(x - 3 * scale, y, 6 * scale, 18 * scale);
    const layers = [
      { yOff: -8, w: 22, h: 24 },
      { yOff: -20, w: 18, h: 20 },
      { yOff: -30, w: 13, h: 16 },
    ];
    for (const layer of layers) {
      g.fillStyle(0x2a5a20, 1);
      g.beginPath();
      g.moveTo(x - layer.w * scale / 2, y + layer.yOff * scale + layer.h * scale);
      g.lineTo(x, y + layer.yOff * scale);
      g.lineTo(x + layer.w * scale / 2, y + layer.yOff * scale + layer.h * scale);
      g.closePath();
      g.fillPath();
      g.fillStyle(0x3a7a2e, 0.4);
      g.beginPath();
      g.moveTo(x - layer.w * scale / 4, y + layer.yOff * scale + layer.h * scale * 0.4);
      g.lineTo(x, y + layer.yOff * scale + 2);
      g.lineTo(x + layer.w * scale / 4, y + layer.yOff * scale + layer.h * scale * 0.6);
      g.closePath();
      g.fillPath();
    }
  }

  private drawOakTree(x: number, y: number, scale: number) {
    const g = this.add.graphics();
    g.fillStyle(0x000000, 0.12);
    g.fillEllipse(x + 3, y + 18 * scale, 26 * scale, 10 * scale);
    g.fillStyle(0x5c3d1e, 1);
    g.fillRect(x - 4 * scale, y - 4 * scale, 8 * scale, 22 * scale);
    g.fillStyle(0x6e4e28, 0.5);
    g.fillRect(x - 2 * scale, y - 4 * scale, 4 * scale, 22 * scale);
    const crownCircles = [
      { dx: 0, dy: -18, r: 16 },
      { dx: -10, dy: -14, r: 13 },
      { dx: 10, dy: -14, r: 13 },
      { dx: -6, dy: -24, r: 11 },
      { dx: 6, dy: -22, r: 12 },
    ];
    for (const c of crownCircles) {
      g.fillStyle(0x2e6e22, 1);
      g.fillCircle(x + c.dx * scale, y + c.dy * scale, c.r * scale);
    }
    g.fillStyle(0x3e8e32, 0.4);
    g.fillCircle(x - 3 * scale, y - 22 * scale, 8 * scale);
    g.fillCircle(x + 5 * scale, y - 16 * scale, 7 * scale);
  }

  private drawBush(x: number, y: number, scale: number) {
    const g = this.add.graphics();
    g.fillStyle(0x000000, 0.08);
    g.fillEllipse(x + 2, y + 5 * scale, 16 * scale, 6 * scale);
    g.fillStyle(0x3a6a2a, 1);
    g.fillCircle(x, y, 8 * scale);
    g.fillCircle(x - 5 * scale, y + 2 * scale, 6 * scale);
    g.fillCircle(x + 5 * scale, y + 1 * scale, 7 * scale);
    g.fillStyle(0x4a8a3a, 0.5);
    g.fillCircle(x + 2 * scale, y - 2 * scale, 5 * scale);
  }

  private drawFlowers(x: number, y: number, rng: () => number) {
    const g = this.add.graphics();
    const flowerColors = [0xff6688, 0xffaa44, 0xffdd55, 0xaa88ff, 0xff88aa];
    for (let i = 0; i < 4 + Math.floor(rng() * 4); i++) {
      const fx = x + (rng() - 0.5) * 20;
      const fy = y + (rng() - 0.5) * 14;
      const color = flowerColors[Math.floor(rng() * flowerColors.length)];
      g.fillStyle(0x4a8a3a, 0.6);
      g.fillRect(fx, fy, 1, 4);
      g.fillStyle(color, 0.8);
      g.fillCircle(fx, fy, 2.5);
      g.fillStyle(0xffee88, 0.9);
      g.fillCircle(fx, fy, 1);
    }
  }

  private drawRockCluster(x: number, y: number, rng: () => number) {
    const g = this.add.graphics();
    for (let i = 0; i < 2 + Math.floor(rng() * 3); i++) {
      const rx = x + (rng() - 0.5) * 16;
      const ry = y + (rng() - 0.5) * 10;
      const rw = 6 + rng() * 10;
      const rh = 4 + rng() * 7;
      g.fillStyle(0x000000, 0.1);
      g.fillEllipse(rx + 2, ry + rh / 2 + 2, rw + 2, rh / 2);
      g.fillStyle(0x777777, 1);
      g.fillRoundedRect(rx - rw / 2, ry - rh / 2, rw, rh, 3);
      g.fillStyle(0x999999, 0.5);
      g.fillRoundedRect(rx - rw / 2 + 2, ry - rh / 2 + 1, rw * 0.6, rh * 0.5, 2);
    }
  }

  private drawRiver() {
    const W = WORLD_SIZE;
    const points = [
      { x: W - 30, y: 0 },
      { x: W - 60, y: 150 },
      { x: W - 35, y: 300 },
      { x: W - 70, y: 450 },
      { x: W - 40, y: 600 },
      { x: W - 65, y: 750 },
      { x: W - 35, y: 900 },
      { x: W - 55, y: 1050 },
      { x: W - 30, y: W },
    ];

    const drawCurve = (gfx: Phaser.GameObjects.Graphics, pts: { x: number; y: number }[]) => {
      if (pts.length < 2) return;
      gfx.beginPath();
      gfx.moveTo(pts[0].x, pts[0].y);
      for (let i = 0; i < pts.length - 1; i++) {
        const mx = (pts[i].x + pts[i + 1].x) / 2;
        const my = (pts[i].y + pts[i + 1].y) / 2;
        if (i === pts.length - 2) {
          gfx.lineTo(pts[i + 1].x, pts[i + 1].y);
        } else {
          gfx.lineTo(mx, my);
        }
      }
      gfx.strokePath();
    };

    const river = this.add.graphics();
    river.lineStyle(28, 0x3a5a3a, 0.4);
    drawCurve(river, points);
    river.lineStyle(20, 0x3a7abb, 0.7);
    drawCurve(river, points);
    river.lineStyle(14, 0x5a9ad5, 0.6);
    drawCurve(river, points);
    const hl = points.map(p => ({ x: p.x + 3, y: p.y }));
    river.lineStyle(4, 0x8ec8f0, 0.35);
    drawCurve(river, hl);
  }

  /* ═══════════════════════════════════════════
     Buildings
     ═══════════════════════════════════════════ */

  private createBuilding(slot: BuildingSlot) {
    const bx = CENTER_X + slot.x;
    const by = CENTER_Y + slot.y;
    const isTownHall = slot.type === BuildingType.TownHall;
    const s = isTownHall ? 1.4 : 1.0;
    const colors = BUILDING_COLORS[slot.type];

    const container = this.add.container(bx, by);

    // Ground patch under building
    const ground = this.add.graphics();
    ground.fillStyle(0x6a5a3a, 0.25);
    ground.fillEllipse(0, 22 * s, 80 * s, 28 * s);
    container.add(ground);

    // Shadow
    const shadow = this.add.graphics();
    shadow.fillStyle(0x000000, 0.18);
    shadow.fillEllipse(5 * s, 28 * s, 72 * s, 16 * s);
    container.add(shadow);

    // Stone foundation
    const foundation = this.add.graphics();
    foundation.fillStyle(0x6a6a5e, 1);
    foundation.fillRoundedRect(-34 * s, 18 * s, 68 * s, 12 * s, 2);
    foundation.lineStyle(1, 0x4a4a3e, 0.5);
    foundation.strokeRoundedRect(-34 * s, 18 * s, 68 * s, 12 * s, 2);
    container.add(foundation);

    // Walls (main body)
    const walls = this.add.graphics();
    walls.fillStyle(colors.base, 1);
    walls.fillRoundedRect(-32 * s, -14 * s, 64 * s, 34 * s, 3);
    const lighterBase = Phaser.Display.Color.IntegerToColor(colors.base);
    const lighter = Phaser.Display.Color.GetColor(
      Math.min(255, lighterBase.red + 25),
      Math.min(255, lighterBase.green + 25),
      Math.min(255, lighterBase.blue + 25),
    );
    walls.fillStyle(lighter, 0.5);
    walls.fillRoundedRect(-30 * s, -10 * s, 60 * s, 28 * s, 2);
    walls.lineStyle(1.5, 0x333333, 0.35);
    walls.strokeRoundedRect(-32 * s, -14 * s, 64 * s, 34 * s, 3);
    container.add(walls);

    // Timber frame lines (non-town-hall)
    if (!isTownHall) {
      const timber = this.add.graphics();
      timber.lineStyle(2, 0x4a3018, 0.35);
      timber.beginPath(); timber.moveTo(-30 * s, 2 * s); timber.lineTo(30 * s, 2 * s); timber.strokePath();
      timber.beginPath(); timber.moveTo(-12 * s, -12 * s); timber.lineTo(-12 * s, 18 * s); timber.strokePath();
      timber.beginPath(); timber.moveTo(12 * s, -12 * s); timber.lineTo(12 * s, 18 * s); timber.strokePath();
      container.add(timber);
    }

    // Roof
    const roof = this.add.graphics();
    if (isTownHall) {
      roof.fillStyle(colors.roof, 1);
      roof.beginPath();
      roof.moveTo(-42 * s, -14 * s);
      roof.lineTo(-8 * s, -50 * s);
      roof.lineTo(8 * s, -50 * s);
      roof.lineTo(42 * s, -14 * s);
      roof.closePath();
      roof.fillPath();
      roof.fillStyle(0xffd700, 0.3);
      roof.fillRect(-6 * s, -50 * s, 12 * s, 4 * s);
      const darkerRoof = Phaser.Display.Color.IntegerToColor(colors.roof);
      const darker = Phaser.Display.Color.GetColor(
        Math.max(0, darkerRoof.red - 30), Math.max(0, darkerRoof.green - 30), Math.max(0, darkerRoof.blue - 30));
      roof.fillStyle(darker, 0.4);
      roof.beginPath();
      roof.moveTo(0, -50 * s); roof.lineTo(42 * s, -14 * s); roof.lineTo(8 * s, -50 * s);
      roof.closePath(); roof.fillPath();
      roof.lineStyle(1.5, 0x2a1a0a, 0.4);
      roof.beginPath();
      roof.moveTo(-42 * s, -14 * s); roof.lineTo(-8 * s, -50 * s);
      roof.lineTo(8 * s, -50 * s); roof.lineTo(42 * s, -14 * s);
      roof.closePath(); roof.strokePath();
    } else {
      roof.fillStyle(colors.roof, 1);
      roof.beginPath();
      roof.moveTo(-38 * s, -14 * s); roof.lineTo(0, -44 * s); roof.lineTo(38 * s, -14 * s);
      roof.closePath(); roof.fillPath();
      const darkerRoof = Phaser.Display.Color.IntegerToColor(colors.roof);
      const darker = Phaser.Display.Color.GetColor(
        Math.max(0, darkerRoof.red - 30), Math.max(0, darkerRoof.green - 30), Math.max(0, darkerRoof.blue - 30));
      roof.fillStyle(darker, 0.35);
      roof.beginPath();
      roof.moveTo(0, -44 * s); roof.lineTo(38 * s, -14 * s); roof.lineTo(0, -14 * s);
      roof.closePath(); roof.fillPath();
      roof.lineStyle(1.5, 0x2a1a0a, 0.4);
      roof.beginPath();
      roof.moveTo(-38 * s, -14 * s); roof.lineTo(0, -44 * s); roof.lineTo(38 * s, -14 * s);
      roof.closePath(); roof.strokePath();
    }
    container.add(roof);

    // Windows & door
    const win = this.add.graphics();
    if (isTownHall) {
      const winPositions = [-16, 0, 16];
      for (const wx of winPositions) {
        win.fillStyle(0x3a2a1a, 1);
        win.fillRoundedRect(wx * s - 5 * s, -6 * s, 10 * s, 14 * s, { tl: 5, tr: 5, bl: 0, br: 0 });
        win.fillStyle(0xffeebb, 0.85);
        win.fillRoundedRect(wx * s - 3.5 * s, -4.5 * s, 7 * s, 11 * s, { tl: 3.5, tr: 3.5, bl: 0, br: 0 });
        win.fillStyle(0xffdd88, 0.3);
        win.fillRoundedRect(wx * s - 2 * s, -3 * s, 4 * s, 8 * s, { tl: 2, tr: 2, bl: 0, br: 0 });
      }
      win.fillStyle(0x3a2010, 1);
      win.fillRoundedRect(-8 * s, 6 * s, 16 * s, 14 * s, { tl: 8, tr: 8, bl: 0, br: 0 });
      win.fillStyle(0x4a3020, 0.5);
      win.fillRoundedRect(-6 * s, 8 * s, 12 * s, 10 * s, { tl: 6, tr: 6, bl: 0, br: 0 });
      win.fillStyle(0xccaa44, 1);
      win.fillCircle(3 * s, 14 * s, 1.5 * s);
    } else {
      for (const wx of [-14, 8]) {
        win.fillStyle(0x3a2a1a, 1);
        win.fillRoundedRect(wx * s - 1, -6 * s, 9 * s, 11 * s, 2);
        win.fillStyle(0xffeebb, 0.75);
        win.fillRoundedRect(wx * s + 0.5, -4.5 * s, 6.5 * s, 8.5 * s, 1.5);
        win.lineStyle(1, 0x5a4a3a, 0.5);
        const wcx = wx * s + 3.75 * s;
        const wcy = -0.25 * s;
        win.beginPath(); win.moveTo(wcx, -4.5 * s); win.lineTo(wcx, 4 * s); win.strokePath();
        win.beginPath(); win.moveTo(wx * s + 0.5, wcy); win.lineTo(wx * s + 7, wcy); win.strokePath();
      }
      win.fillStyle(0x3a2010, 1);
      win.fillRoundedRect(-5 * s, 4 * s, 10 * s, 16 * s, { tl: 5, tr: 5, bl: 0, br: 0 });
      win.fillStyle(0x4a3020, 0.4);
      win.fillRoundedRect(-3.5 * s, 6 * s, 7 * s, 12 * s, { tl: 3.5, tr: 3.5, bl: 0, br: 0 });
      win.fillStyle(0xccaa44, 1);
      win.fillCircle(2.5 * s, 12 * s, 1.2 * s);
    }
    container.add(win);

    // Chimney (skip for farm)
    if (slot.type !== BuildingType.Farm) {
      const chimney = this.add.graphics();
      const cx = isTownHall ? 20 * s : 14 * s;
      const cy = isTownHall ? -42 * s : -34 * s;
      chimney.fillStyle(0x6a5a4a, 1);
      chimney.fillRect(cx, cy, 8 * s, 16 * s);
      chimney.fillStyle(0x5a4a3a, 0.5);
      chimney.fillRect(cx, cy, 3 * s, 16 * s);
      chimney.fillStyle(0x7a6a5a, 1);
      chimney.fillRect(cx - 1, cy - 2, 10 * s, 3);
      container.add(chimney);
    }

    // Building-specific accents
    this.addBuildingAccent(container, slot, s);

    // Level badge
    const badgeX = 28 * s;
    const badgeY = -30 * s;
    const badgeR = isTownHall ? 14 : 12;
    const badge = this.add.graphics();
    badge.fillStyle(0x000000, 0.2);
    badge.fillCircle(badgeX + 1, badgeY + 1, badgeR + 1);
    badge.fillStyle(0x2a5aaa, 1);
    badge.fillCircle(badgeX, badgeY, badgeR);
    badge.lineStyle(2, 0x4a8aee, 0.8);
    badge.strokeCircle(badgeX, badgeY, badgeR);
    badge.fillStyle(0x3a6acc, 0.5);
    badge.fillCircle(badgeX - 1, badgeY - 1, badgeR - 3);
    container.add(badge);

    const levelText = this.add.text(badgeX, badgeY, `${slot.level}`, {
      fontSize: `${isTownHall ? 15 : 13}px`,
      fontFamily: '"Segoe UI", Arial, sans-serif',
      color: '#ffffff',
      fontStyle: 'bold',
      stroke: '#1a3a6a',
      strokeThickness: 2,
    }).setOrigin(0.5);
    container.add(levelText);

    // Name label
    const nameLabel = this.add.text(0, 36 * s, slot.name, {
      fontSize: `${isTownHall ? 13 : 11}px`,
      fontFamily: '"Segoe UI", Arial, sans-serif',
      color: '#f0e8d0',
      fontStyle: 'bold',
      stroke: '#1a1a0a',
      strokeThickness: 3,
      shadow: { offsetX: 1, offsetY: 1, color: '#000000', blur: 3, fill: true, stroke: true },
    }).setOrigin(0.5, 0);
    container.add(nameLabel);

    // Interactivity
    const hitW = 80 * s;
    const hitH = 100 * s;
    const hitArea = new Phaser.Geom.Rectangle(-hitW / 2, -55 * s, hitW, hitH);
    container.setSize(hitW, hitH);
    container.setInteractive(hitArea, Phaser.Geom.Rectangle.Contains);

    container.on('pointerover', () => {
      this.tweens.add({ targets: container, scaleX: 1.06, scaleY: 1.06, duration: 120, ease: 'Back.easeOut' });
      this.input.setDefaultCursor('pointer');
    });
    container.on('pointerout', () => {
      this.tweens.add({ targets: container, scaleX: 1, scaleY: 1, duration: 120, ease: 'Back.easeOut' });
      this.input.setDefaultCursor('default');
    });
    container.on('pointerup', (pointer: Phaser.Input.Pointer) => {
      if (this.isDragging) return;
      if (pointer.getDistance() > 10) return;
      this.showBuildingPopup(slot);
    });

    this.buildingContainers.set(slot.id, container);

    // Subtle idle sway
    this.tweens.add({
      targets: container, y: by - 2,
      duration: 2500 + Math.random() * 1500, ease: 'Sine.easeInOut', yoyo: true, repeat: -1,
    });

    // Smoke for non-farm buildings
    if (slot.type !== BuildingType.Farm) {
      this.addSmoke(slot, s);
    }
  }

  private addBuildingAccent(container: Phaser.GameObjects.Container, slot: BuildingSlot, s: number) {
    const g = this.add.graphics();
    switch (slot.type) {
      case BuildingType.TownHall: {
        // Flag pole on top
        g.lineStyle(2, 0x5a4a3a, 1);
        g.beginPath(); g.moveTo(0, -50 * s); g.lineTo(0, -68 * s); g.strokePath();
        g.fillStyle(0xcc2222, 1);
        g.beginPath(); g.moveTo(0, -68 * s); g.lineTo(14 * s, -63 * s); g.lineTo(0, -58 * s); g.closePath(); g.fillPath();
        g.fillStyle(0xff4444, 0.4);
        g.beginPath(); g.moveTo(0, -68 * s); g.lineTo(10 * s, -65 * s); g.lineTo(0, -62 * s); g.closePath(); g.fillPath();
        break;
      }
      case BuildingType.LumberMill: {
        for (let i = 0; i < 4; i++) {
          g.fillStyle(0x7a5a2a, 1);
          g.fillRoundedRect(-42 * s + i * 4, 8 * s + i * 3, 12, 6, 2);
          g.fillStyle(0x8a6a3a, 0.5);
          g.fillRoundedRect(-41 * s + i * 4, 8 * s + i * 3, 6, 3, 1);
        }
        break;
      }
      case BuildingType.ClayPit: {
        g.fillStyle(0xb05a2a, 1);
        g.fillRoundedRect(34 * s, 10 * s, 10, 12, { tl: 3, tr: 3, bl: 1, br: 1 });
        g.fillRoundedRect(42 * s, 14 * s, 8, 8, { tl: 2, tr: 2, bl: 1, br: 1 });
        g.fillStyle(0xc06a3a, 0.4);
        g.fillRoundedRect(35 * s, 11 * s, 5, 6, 1);
        break;
      }
      case BuildingType.IronMine: {
        g.fillStyle(0x555555, 1);
        g.fillRect(-40 * s, 14 * s, 12, 6);
        g.fillRect(-37 * s, 10 * s, 6, 4);
        g.lineStyle(2, 0x6a5a4a, 1);
        g.beginPath(); g.moveTo(-44 * s, 6 * s); g.lineTo(-36 * s, 18 * s); g.strokePath();
        break;
      }
      case BuildingType.Farm: {
        g.fillStyle(0xccaa44, 1);
        g.fillRoundedRect(34 * s, 12 * s, 14, 10, 3);
        g.fillRoundedRect(38 * s, 6 * s, 12, 8, 3);
        g.fillStyle(0xddbb55, 0.4);
        g.fillRoundedRect(35 * s, 13 * s, 7, 5, 1);
        g.lineStyle(2, 0x7a6a4a, 0.7);
        g.beginPath(); g.moveTo(-44 * s, 20 * s); g.lineTo(-44 * s, 10 * s); g.strokePath();
        g.beginPath(); g.moveTo(-36 * s, 20 * s); g.lineTo(-36 * s, 10 * s); g.strokePath();
        g.beginPath(); g.moveTo(-46 * s, 14 * s); g.lineTo(-34 * s, 14 * s); g.strokePath();
        break;
      }
    }
    container.add(g);
  }

  /* ═══════════════════════════════════════════
     Ambient Details
     ═══════════════════════════════════════════ */

  private drawAmbientDetails() {
    // Well at plaza center
    const well = this.add.graphics();
    well.fillStyle(0x000000, 0.15);
    well.fillEllipse(CENTER_X + 2, CENTER_Y + 10, 24, 10);
    well.fillStyle(0x6a6a5e, 1);
    well.fillCircle(CENTER_X, CENTER_Y, 12);
    well.lineStyle(2, 0x4a4a3e, 0.7);
    well.strokeCircle(CENTER_X, CENTER_Y, 12);
    well.fillStyle(0x4a8abb, 0.6);
    well.fillCircle(CENTER_X, CENTER_Y, 8);
    well.fillStyle(0x5a4a3a, 1);
    well.fillRect(CENTER_X - 10, CENTER_Y - 18, 3, 20);
    well.fillRect(CENTER_X + 7, CENTER_Y - 18, 3, 20);
    well.fillStyle(0x6a5a4a, 1);
    well.fillRect(CENTER_X - 11, CENTER_Y - 20, 22, 3);
    well.lineStyle(1, 0x8a7a5a, 0.5);
    well.beginPath(); well.moveTo(CENTER_X, CENTER_Y - 18); well.lineTo(CENTER_X + 3, CENTER_Y - 5); well.strokePath();

    // Torch glow particles at gate towers
    const gateAngles = [
      Math.atan2(-1, -1), Math.atan2(-1, 1),
      Math.atan2(1, -1), Math.atan2(1, 1),
    ];
    const R = VILLAGE_RADIUS + 10;
    for (const angle of gateAngles) {
      const tx = CENTER_X + Math.cos(angle) * R;
      const ty = CENTER_Y + Math.sin(angle) * R;
      this.add.particles(tx, ty - 12, 'glow', {
        speed: { min: 3, max: 8 },
        angle: { min: 250, max: 290 },
        scale: { start: 0.6, end: 0 },
        alpha: { start: 0.5, end: 0 },
        lifespan: { min: 600, max: 1200 },
        frequency: 400,
        quantity: 1,
        tint: 0xff8833,
      });
    }
  }

  /* ═══════════════════════════════════════════
     Smoke
     ═══════════════════════════════════════════ */

  private addSmoke(slot: BuildingSlot, s: number) {
    const bx = CENTER_X + slot.x;
    const by = CENTER_Y + slot.y;
    const isTownHall = slot.type === BuildingType.TownHall;
    const cx = isTownHall ? 24 * s : 18 * s;
    const cy = isTownHall ? -46 * s : -38 * s;

    const emitter = this.add.particles(bx + cx, by + cy, 'smoke', {
      speed: { min: 3, max: 10 },
      angle: { min: 255, max: 285 },
      scale: { start: 0.3, end: 1.2 },
      alpha: { start: 0.3, end: 0 },
      lifespan: { min: 2000, max: 3500 },
      frequency: 1200,
      quantity: 1,
      tint: [0xbbbbbb, 0xaaaaaa, 0x999999],
    });
    this.smokeEmitters.push(emitter);
  }

  /* ═══════════════════════════════════════════
     Building Popup
     ═══════════════════════════════════════════ */

  private showBuildingPopup(slot: BuildingSlot) {
    if (this.popup) { this.popup.destroy(); this.popup = null; }
    const bx = CENTER_X + slot.x;
    const by = CENTER_Y + slot.y;
    this.popup = new BuildingPopup(this, bx, by - 90, slot, this.resources, (id: number) => this.handleUpgrade(id));
  }

  private handleUpgrade(buildingId: number) {
    const building = this.buildings.find(b => b.id === buildingId);
    if (!building) return;

    const cost = upgradeCostForLevel(building.upgradeCost, building.level);
    const wood = this.resources.find(r => r.type === ResourceType.Wood)!;
    const clay = this.resources.find(r => r.type === ResourceType.Clay)!;
    const iron = this.resources.find(r => r.type === ResourceType.Iron)!;
    const crop = this.resources.find(r => r.type === ResourceType.Crop)!;

    if (wood.amount < cost.wood || clay.amount < cost.clay ||
        iron.amount < cost.iron || crop.amount < cost.crop) return;

    wood.amount -= cost.wood;
    clay.amount -= cost.clay;
    iron.amount -= cost.iron;
    crop.amount -= cost.crop;
    building.level++;

    const resourceMap: Record<string, ResourceType> = {
      [BuildingType.LumberMill]: ResourceType.Wood,
      [BuildingType.ClayPit]: ResourceType.Clay,
      [BuildingType.IronMine]: ResourceType.Iron,
      [BuildingType.Farm]: ResourceType.Crop,
    };
    const resType = resourceMap[building.type];
    if (resType) {
      this.resources.find(r => r.type === resType)!.production = productionPerSecond(building.level);
    }
    if (building.type === BuildingType.TownHall) {
      const cap = storageCapacity(building.level);
      for (const r of this.resources) r.capacity = cap;
    }

    const container = this.buildingContainers.get(buildingId);
    if (container) { container.destroy(); this.buildingContainers.delete(buildingId); this.createBuilding(building); }

    const bx = CENTER_X + building.x;
    const by = CENTER_Y + building.y;

    const flash = this.add.graphics();
    flash.fillStyle(0xffffaa, 0.35);
    flash.fillCircle(bx, by, 60);
    this.tweens.add({ targets: flash, alpha: 0, duration: 700, onComplete: () => flash.destroy() });

    const lvText = this.add.text(bx, by - 70, `⬆ Nv. ${building.level}`, {
      fontSize: '15px', fontFamily: '"Segoe UI", Arial', color: '#ffd700', fontStyle: 'bold',
      stroke: '#1a1a0a', strokeThickness: 3,
    }).setOrigin(0.5);
    this.tweens.add({ targets: lvText, y: by - 120, alpha: 0, duration: 1200, ease: 'Cubic.easeOut', onComplete: () => lvText.destroy() });

    if (this.popup) { this.popup.destroy(); this.popup = null; }
    this.showBuildingPopup(building);
    this.pushResourcesToBlazor();
  }

  /* ═══════════════════════════════════════════
     Resource Ticking
     ═══════════════════════════════════════════ */

  private tickResources() {
    for (const r of this.resources) r.amount = Math.min(r.amount + r.production, r.capacity);
    this.pushResourcesToBlazor();
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
    const prodBuildings = this.buildings.filter(b => b.type !== BuildingType.TownHall);
    const b = prodBuildings[Math.floor(Math.random() * prodBuildings.length)];
    if (!b) return;

    const resMap: Record<string, { icon: string; color: string }> = {
      [BuildingType.LumberMill]: { icon: '🪵', color: '#d4a843' },
      [BuildingType.ClayPit]: { icon: '🧱', color: '#c2703a' },
      [BuildingType.IronMine]: { icon: '⛏️', color: '#99b3c4' },
      [BuildingType.Farm]: { icon: '🌾', color: '#88cc55' },
    };
    const info = resMap[b.type];
    if (!info) return;

    const bx = CENTER_X + b.x;
    const by = CENTER_Y + b.y;
    const prod = productionPerSecond(b.level);

    const txt = this.add.text(bx + (Math.random() - 0.5) * 25, by - 25, `${info.icon}+${formatNumber(prod)}`, {
      fontSize: '12px', fontFamily: '"Segoe UI", Arial', color: info.color, fontStyle: 'bold',
      stroke: '#000000', strokeThickness: 2,
    }).setOrigin(0.5);
    this.tweens.add({ targets: txt, y: by - 70, alpha: 0, duration: 1800, ease: 'Cubic.easeOut', onComplete: () => txt.destroy() });
  }

  /* ═══════════════════════════════════════════
     Camera Controls
     ═══════════════════════════════════════════ */

  private setupCameraControls() {
    this.input.on('pointerdown', (pointer: Phaser.Input.Pointer) => {
      this.isDragging = false;
      this.dragStartX = pointer.x;
      this.dragStartY = pointer.y;
    });

    this.input.on('pointermove', (pointer: Phaser.Input.Pointer) => {
      if (!pointer.isDown) return;
      if (Math.abs(pointer.x - this.dragStartX) > 5 || Math.abs(pointer.y - this.dragStartY) > 5) {
        this.isDragging = true;
      }
      if (this.isDragging) {
        this.cameras.main.scrollX -= (pointer.x - pointer.prevPosition.x) / this.cameras.main.zoom;
        this.cameras.main.scrollY -= (pointer.y - pointer.prevPosition.y) / this.cameras.main.zoom;
      }
    });

    this.input.on('wheel', (_p: Phaser.Input.Pointer, _go: unknown[], _dx: number, dy: number) => {
      const cam = this.cameras.main;
      cam.setZoom(Phaser.Math.Clamp(cam.zoom - dy * 0.001, 0.4, 2.5));
    });

    this.input.addPointer(1);
  }

  /* ═══════════════════════════════════════════
     Cleanup
     ═══════════════════════════════════════════ */

  destroy() {
    if (this.resourceTimer) { this.resourceTimer.destroy(); this.resourceTimer = null; }
    if (this.popup) { this.popup.destroy(); this.popup = null; }
    for (const emitter of this.smokeEmitters) emitter.destroy();
    this.smokeEmitters = [];
    this.buildingContainers.clear();
    this.netRef = null;
  }
}
