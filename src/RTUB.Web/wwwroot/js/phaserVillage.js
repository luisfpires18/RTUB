var __defProp = Object.defineProperty;
var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
var __publicField = (obj, key, value) => __defNormalProp(obj, typeof key !== "symbol" ? key + "" : key, value);
(function(Phaser2) {
  "use strict";
  var ResourceType = /* @__PURE__ */ ((ResourceType2) => {
    ResourceType2["Wood"] = "wood";
    ResourceType2["Clay"] = "clay";
    ResourceType2["Iron"] = "iron";
    ResourceType2["Crop"] = "crop";
    return ResourceType2;
  })(ResourceType || {});
  var BuildingType = /* @__PURE__ */ ((BuildingType2) => {
    BuildingType2["TownHall"] = "townHall";
    BuildingType2["LumberMill"] = "lumberMill";
    BuildingType2["ClayPit"] = "clayPit";
    BuildingType2["IronMine"] = "ironMine";
    BuildingType2["Farm"] = "farm";
    return BuildingType2;
  })(BuildingType || {});
  const COLORS = {
    // Building bases
    townHall: 13936707,
    lumberMill: 9136404,
    clayPit: 12742714,
    ironMine: 8031129,
    farm: 7052858,
    // Building roofs
    townHallRoof: 12096046,
    lumberMillRoof: 7032848,
    clayPitRoof: 10377774,
    ironMineRoof: 5925495,
    farmRoof: 5602862,
    // Popup
    popupBg: 1710638,
    popupBorder: 13936707,
    upgradeBtn: 5025616,
    upgradeBtnHover: 6737002,
    disabledBtn: 6710886
  };
  function cost(wood, clay, iron, crop) {
    return { wood, clay, iron, crop };
  }
  function createDefaultBuildings() {
    return [
      {
        id: 0,
        type: BuildingType.TownHall,
        level: 1,
        x: 0,
        y: 0,
        name: "Centro da Vila",
        description: "Coração da vila. Sobe o nível máximo dos outros edifícios.",
        upgradeCost: cost(100, 100, 80, 60)
      },
      {
        id: 1,
        type: BuildingType.LumberMill,
        level: 1,
        x: -200,
        y: -140,
        name: "Serralharia",
        description: "Produz madeira para construção.",
        upgradeCost: cost(50, 30, 20, 10)
      },
      {
        id: 2,
        type: BuildingType.ClayPit,
        level: 1,
        x: 200,
        y: -140,
        name: "Cova de Barro",
        description: "Extrai barro para materiais de construção.",
        upgradeCost: cost(30, 50, 20, 10)
      },
      {
        id: 3,
        type: BuildingType.IronMine,
        level: 1,
        x: -200,
        y: 140,
        name: "Mina de Ferro",
        description: "Extrai ferro para ferramentas e armas.",
        upgradeCost: cost(30, 20, 50, 10)
      },
      {
        id: 4,
        type: BuildingType.Farm,
        level: 1,
        x: 200,
        y: 140,
        name: "Quinta",
        description: "Produz alimento para alimentar a vila.",
        upgradeCost: cost(20, 20, 10, 50)
      }
    ];
  }
  function createDefaultResources() {
    return [
      { type: ResourceType.Wood, amount: 500, production: 5, capacity: 2e3 },
      { type: ResourceType.Clay, amount: 500, production: 5, capacity: 2e3 },
      { type: ResourceType.Iron, amount: 500, production: 5, capacity: 2e3 },
      { type: ResourceType.Crop, amount: 500, production: 5, capacity: 2e3 }
    ];
  }
  const BUILDING_COLORS = {
    [BuildingType.TownHall]: { base: COLORS.townHall, roof: COLORS.townHallRoof },
    [BuildingType.LumberMill]: { base: COLORS.lumberMill, roof: COLORS.lumberMillRoof },
    [BuildingType.ClayPit]: { base: COLORS.clayPit, roof: COLORS.clayPitRoof },
    [BuildingType.IronMine]: { base: COLORS.ironMine, roof: COLORS.ironMineRoof },
    [BuildingType.Farm]: { base: COLORS.farm, roof: COLORS.farmRoof }
  };
  const BUILDING_ICONS = {
    [BuildingType.TownHall]: "🏛️",
    [BuildingType.LumberMill]: "🪵",
    [BuildingType.ClayPit]: "🧱",
    [BuildingType.IronMine]: "⛏️",
    [BuildingType.Farm]: "🌾"
  };
  const RESOURCE_ICONS = {
    [ResourceType.Wood]: "🪵",
    [ResourceType.Clay]: "🧱",
    [ResourceType.Iron]: "⛏️",
    [ResourceType.Crop]: "🌾"
  };
  ({
    [ResourceType.Wood]: "Madeira",
    [ResourceType.Clay]: "Barro",
    [ResourceType.Iron]: "Ferro",
    [ResourceType.Crop]: "Alimento"
  });
  function productionPerSecond(level) {
    return Math.round(5 * Math.pow(1.4, level - 1) * 100) / 100;
  }
  function storageCapacity(townHallLevel) {
    return 2e3 + (townHallLevel - 1) * 1e3;
  }
  function upgradeCostForLevel(baseCost, level) {
    const mult = Math.pow(1.5, level - 1);
    return {
      wood: Math.round(baseCost.wood * mult),
      clay: Math.round(baseCost.clay * mult),
      iron: Math.round(baseCost.iron * mult),
      crop: Math.round(baseCost.crop * mult)
    };
  }
  function formatNumber(n) {
    if (n >= 1e6) return (n / 1e6).toFixed(1) + "M";
    if (n >= 1e4) return (n / 1e3).toFixed(1) + "K";
    return Math.floor(n).toLocaleString("pt-PT");
  }
  function formatProduction(perSecond) {
    const perHour = perSecond * 3600;
    return `+${formatNumber(perHour)}/h`;
  }
  const POPUP_W = 260;
  const POPUP_H = 280;
  class BuildingPopup {
    constructor(scene, x, y, building, resources, onUpgrade) {
      __publicField(this, "container");
      __publicField(this, "scene");
      this.scene = scene;
      const cam = scene.cameras.main;
      const halfW = POPUP_W / 2;
      const halfH = POPUP_H / 2;
      const clampedX = Phaser2.Math.Clamp(x, cam.scrollX + halfW + 10, cam.scrollX + cam.width / cam.zoom - halfW - 10);
      const clampedY = Phaser2.Math.Clamp(y, cam.scrollY + halfH + 10, cam.scrollY + cam.height / cam.zoom - halfH - 10);
      this.container = scene.add.container(clampedX, clampedY);
      this.container.setDepth(100);
      const bg = scene.add.graphics();
      bg.fillStyle(COLORS.popupBg, 0.95);
      bg.fillRoundedRect(-POPUP_W / 2, -POPUP_H / 2, POPUP_W, POPUP_H, 12);
      bg.lineStyle(3, COLORS.popupBorder, 1);
      bg.strokeRoundedRect(-POPUP_W / 2, -POPUP_H / 2, POPUP_W, POPUP_H, 12);
      this.container.add(bg);
      const closeBtn = scene.add.text(POPUP_W / 2 - 20, -POPUP_H / 2 + 8, "✕", {
        fontSize: "18px",
        color: "#ff6666",
        fontStyle: "bold"
      }).setOrigin(0.5).setInteractive({ useHandCursor: true });
      closeBtn.on("pointerup", () => this.destroy());
      this.container.add(closeBtn);
      let yOff = -POPUP_H / 2 + 22;
      const icon = BUILDING_ICONS[building.type];
      const title = scene.add.text(0, yOff, `${icon} ${building.name}`, {
        fontSize: "16px",
        fontFamily: "Arial, sans-serif",
        color: "#ffd700",
        fontStyle: "bold",
        align: "center"
      }).setOrigin(0.5, 0);
      this.container.add(title);
      yOff += 26;
      const levelTxt = scene.add.text(0, yOff, `Nível ${building.level}`, {
        fontSize: "14px",
        fontFamily: "Arial, sans-serif",
        color: "#ffffff",
        align: "center"
      }).setOrigin(0.5, 0);
      this.container.add(levelTxt);
      yOff += 22;
      const desc = scene.add.text(0, yOff, building.description, {
        fontSize: "11px",
        fontFamily: "Arial, sans-serif",
        color: "#aaaaaa",
        align: "center",
        wordWrap: { width: POPUP_W - 30 }
      }).setOrigin(0.5, 0);
      this.container.add(desc);
      yOff += desc.height + 10;
      const resMap = {
        [BuildingType.LumberMill]: ResourceType.Wood,
        [BuildingType.ClayPit]: ResourceType.Clay,
        [BuildingType.IronMine]: ResourceType.Iron,
        [BuildingType.Farm]: ResourceType.Crop
      };
      const resType = resMap[building.type];
      if (resType) {
        const prodNow = productionPerSecond(building.level);
        const prodNext = productionPerSecond(building.level + 1);
        const prodLabel = scene.add.text(
          0,
          yOff,
          `Produção: ${formatProduction(prodNow)} → ${formatProduction(prodNext)}`,
          {
            fontSize: "12px",
            fontFamily: "Arial, sans-serif",
            color: "#88cc88",
            align: "center"
          }
        ).setOrigin(0.5, 0);
        this.container.add(prodLabel);
        yOff += 20;
      }
      const sep = scene.add.graphics();
      sep.lineStyle(1, 5592405, 0.6);
      sep.beginPath();
      sep.moveTo(-POPUP_W / 2 + 15, yOff + 4);
      sep.lineTo(POPUP_W / 2 - 15, yOff + 4);
      sep.strokePath();
      this.container.add(sep);
      yOff += 12;
      const cost2 = upgradeCostForLevel(building.upgradeCost, building.level);
      const costEntries = [
        [RESOURCE_ICONS[ResourceType.Wood], cost2.wood, resources.find((r) => r.type === ResourceType.Wood).amount],
        [RESOURCE_ICONS[ResourceType.Clay], cost2.clay, resources.find((r) => r.type === ResourceType.Clay).amount],
        [RESOURCE_ICONS[ResourceType.Iron], cost2.iron, resources.find((r) => r.type === ResourceType.Iron).amount],
        [RESOURCE_ICONS[ResourceType.Crop], cost2.crop, resources.find((r) => r.type === ResourceType.Crop).amount]
      ];
      const costLabel = scene.add.text(0, yOff, "Custo de melhoria:", {
        fontSize: "11px",
        fontFamily: "Arial, sans-serif",
        color: "#cccccc",
        align: "center"
      }).setOrigin(0.5, 0);
      this.container.add(costLabel);
      yOff += 16;
      let canAfford = true;
      const costStartX = -POPUP_W / 2 + 25;
      const costColW = (POPUP_W - 50) / 4;
      for (let i = 0; i < costEntries.length; i++) {
        const [ico, needed, have] = costEntries[i];
        const enough = have >= needed;
        if (!enough) canAfford = false;
        const color = enough ? "#88ff88" : "#ff6666";
        const cx = costStartX + i * costColW + costColW / 2;
        const costTxt = scene.add.text(cx, yOff, `${ico}
${formatNumber(needed)}`, {
          fontSize: "11px",
          fontFamily: "Arial, sans-serif",
          color,
          align: "center"
        }).setOrigin(0.5, 0);
        this.container.add(costTxt);
      }
      yOff += 35;
      const btnW = 140;
      const btnH = 32;
      const btnColor = canAfford ? COLORS.upgradeBtn : COLORS.disabledBtn;
      const btn = scene.add.graphics();
      btn.fillStyle(btnColor, 1);
      btn.fillRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
      if (canAfford) {
        btn.lineStyle(2, 6741350, 0.6);
        btn.strokeRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
      }
      this.container.add(btn);
      const btnLabel = scene.add.text(0, yOff + btnH / 2, canAfford ? "⬆ Melhorar" : "🔒 Sem recursos", {
        fontSize: "13px",
        fontFamily: "Arial, sans-serif",
        color: canAfford ? "#ffffff" : "#999999",
        fontStyle: "bold",
        align: "center"
      }).setOrigin(0.5);
      this.container.add(btnLabel);
      if (canAfford) {
        const hitZone = scene.add.zone(0, yOff + btnH / 2, btnW, btnH).setInteractive({ useHandCursor: true });
        hitZone.on("pointerup", () => {
          onUpgrade(building.id);
        });
        hitZone.on("pointerover", () => {
          btn.clear();
          btn.fillStyle(COLORS.upgradeBtnHover, 1);
          btn.fillRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
          btn.lineStyle(2, 8978312, 0.8);
          btn.strokeRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
        });
        hitZone.on("pointerout", () => {
          btn.clear();
          btn.fillStyle(COLORS.upgradeBtn, 1);
          btn.fillRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
          btn.lineStyle(2, 6741350, 0.6);
          btn.strokeRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
        });
        this.container.add(hitZone);
      }
      this.container.setScale(0.5);
      this.container.setAlpha(0);
      scene.tweens.add({
        targets: this.container,
        scaleX: 1,
        scaleY: 1,
        alpha: 1,
        duration: 200,
        ease: "Back.easeOut"
      });
    }
    destroy() {
      if (this.container) {
        this.scene.tweens.add({
          targets: this.container,
          scaleX: 0.5,
          scaleY: 0.5,
          alpha: 0,
          duration: 150,
          ease: "Cubic.easeIn",
          onComplete: () => {
            this.container.destroy();
          }
        });
      }
    }
  }
  const SCENE_KEY = "VillageScene";
  const WORLD_SIZE = 1400;
  const CENTER_X = WORLD_SIZE / 2;
  const CENTER_Y = WORLD_SIZE / 2;
  const VILLAGE_RADIUS = 500;
  const RESOURCE_TICK_MS = 1e3;
  function seededRandom(seed) {
    let s = seed;
    return () => {
      s = (s * 16807 + 0) % 2147483647;
      return (s - 1) / 2147483646;
    };
  }
  class VillageScene extends Phaser2.Scene {
    constructor() {
      super({ key: SCENE_KEY });
      __publicField(this, "netRef", null);
      __publicField(this, "buildings", []);
      __publicField(this, "resources", []);
      __publicField(this, "buildingContainers", /* @__PURE__ */ new Map());
      __publicField(this, "resourceTimer", null);
      __publicField(this, "popup", null);
      __publicField(this, "isDragging", false);
      __publicField(this, "dragStartX", 0);
      __publicField(this, "dragStartY", 0);
      __publicField(this, "smokeEmitters", []);
    }
    init(data) {
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
        loop: true
      });
    }
    /* ═══════════════════════════════════════════
       Texture generation
       ═══════════════════════════════════════════ */
    createSmokeTexture() {
      const g = this.add.graphics();
      g.fillStyle(14540253, 1);
      g.fillCircle(8, 8, 8);
      g.fillStyle(13421772, 0.6);
      g.fillCircle(6, 6, 5);
      g.generateTexture("smoke", 16, 16);
      g.destroy();
    }
    createGlowTexture() {
      const g = this.add.graphics();
      g.fillStyle(16755251, 1);
      g.fillCircle(6, 6, 6);
      g.fillStyle(16768392, 0.5);
      g.fillCircle(6, 6, 3);
      g.generateTexture("glow", 12, 12);
      g.destroy();
    }
    /* ═══════════════════════════════════════════
       Terrain
       ═══════════════════════════════════════════ */
    drawTerrain() {
      const g = this.add.graphics();
      g.fillStyle(1980948, 1);
      g.fillRect(0, 0, WORLD_SIZE, WORLD_SIZE);
      g.fillStyle(2970656, 1);
      g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS + 120);
      g.fillStyle(3829333, 0.4);
      g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS + 30);
      g.lineStyle(8, 2771514, 0.5);
      g.strokeCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS + 30);
      g.fillStyle(4882485, 1);
      g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS);
      g.fillStyle(5410874, 0.4);
      g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS - 60);
      g.fillStyle(5937728, 0.25);
      g.fillCircle(CENTER_X, CENTER_Y, VILLAGE_RADIUS - 150);
      const rng = seededRandom(42);
      for (let i = 0; i < 120; i++) {
        const angle = rng() * Math.PI * 2;
        const dist = rng() * (VILLAGE_RADIUS - 30);
        const gx = CENTER_X + Math.cos(angle) * dist;
        const gy = CENTER_Y + Math.sin(angle) * dist;
        const shade = 3832362 + Math.floor(rng() * 2105376);
        g.fillStyle(shade, 0.15 + rng() * 0.15);
        g.fillCircle(gx, gy, 4 + rng() * 8);
      }
    }
    /* ═══════════════════════════════════════════
       Stone & Timber Wall
       ═══════════════════════════════════════════ */
    drawWall() {
      const g = this.add.graphics();
      const R = VILLAGE_RADIUS + 10;
      g.lineStyle(14, 0, 0.15);
      g.strokeCircle(CENTER_X + 3, CENTER_Y + 3, R);
      g.lineStyle(12, 7035454, 1);
      g.strokeCircle(CENTER_X, CENTER_Y, R);
      g.lineStyle(3, 9075290, 0.6);
      g.strokeCircle(CENTER_X, CENTER_Y, R - 5);
      g.lineStyle(2, 4864546, 0.7);
      g.strokeCircle(CENTER_X, CENTER_Y, R + 5);
      const rng = seededRandom(99);
      for (let i = 0; i < 60; i++) {
        const angle = i / 60 * Math.PI * 2;
        const wx = CENTER_X + Math.cos(angle) * R;
        const wy = CENTER_Y + Math.sin(angle) * R;
        g.lineStyle(1, 4864546, 0.4);
        g.beginPath();
        g.moveTo(wx - 5 + rng() * 3, wy - 6);
        g.lineTo(wx - 5 + rng() * 3, wy + 6);
        g.strokePath();
      }
      const gateAngles = [
        Math.atan2(-1, -1),
        // top-left (LumberMill)
        Math.atan2(-1, 1),
        // top-right (ClayPit)
        Math.atan2(1, -1),
        // bottom-left (IronMine)
        Math.atan2(1, 1)
        // bottom-right (Farm)
      ];
      for (const angle of gateAngles) {
        const tx = CENTER_X + Math.cos(angle) * R;
        const ty = CENTER_Y + Math.sin(angle) * R;
        this.drawGateTower(tx, ty);
      }
    }
    drawGateTower(x, y) {
      const g = this.add.graphics();
      g.fillStyle(0, 0.15);
      g.fillRoundedRect(x - 13, y - 13, 30, 30, 4);
      g.fillStyle(8022606, 1);
      g.fillRoundedRect(x - 15, y - 15, 30, 30, 4);
      g.fillStyle(9075294, 0.6);
      g.fillRoundedRect(x - 13, y - 13, 26, 14, 3);
      g.fillStyle(6969918, 1);
      g.fillRect(x - 15, y - 18, 6, 5);
      g.fillRect(x - 3, y - 18, 6, 5);
      g.fillRect(x + 9, y - 18, 6, 5);
      g.lineStyle(1.5, 3811861, 0.6);
      g.strokeRoundedRect(x - 15, y - 15, 30, 30, 4);
    }
    /* ═══════════════════════════════════════════
       Paths
       ═══════════════════════════════════════════ */
    drawPaths() {
      const g = this.add.graphics();
      for (const b of this.buildings) {
        if (b.type === BuildingType.TownHall) continue;
        const bx = CENTER_X + b.x;
        const by = CENTER_Y + b.y;
        g.lineStyle(22, 0, 0.08);
        g.beginPath();
        g.moveTo(CENTER_X + 2, CENTER_Y + 2);
        g.lineTo(bx + 2, by + 2);
        g.strokePath();
        g.lineStyle(20, 8022080, 0.7);
        g.beginPath();
        g.moveTo(CENTER_X, CENTER_Y);
        g.lineTo(bx, by);
        g.strokePath();
        g.lineStyle(16, 10127974, 0.8);
        g.beginPath();
        g.moveTo(CENTER_X, CENTER_Y);
        g.lineTo(bx, by);
        g.strokePath();
        g.lineStyle(6, 11575418, 0.3);
        g.beginPath();
        g.moveTo(CENTER_X, CENTER_Y);
        g.lineTo(bx, by);
        g.strokePath();
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
          g.lineStyle(1, 6969920, 0.2 + rng() * 0.15);
          g.strokeCircle(px, py, 2 + rng() * 2);
        }
      }
      const plaza = this.add.graphics();
      plaza.fillStyle(0, 0.1);
      plaza.fillCircle(CENTER_X + 2, CENTER_Y + 2, 58);
      plaza.fillStyle(9075290, 0.8);
      plaza.fillCircle(CENTER_X, CENTER_Y, 56);
      plaza.fillStyle(10391150, 0.6);
      plaza.fillCircle(CENTER_X, CENTER_Y, 48);
      plaza.fillStyle(11575424, 0.3);
      plaza.fillCircle(CENTER_X, CENTER_Y, 30);
      plaza.lineStyle(2, 6969920, 0.4);
      plaza.strokeCircle(CENTER_X, CENTER_Y, 56);
      plaza.lineStyle(1, 8022608, 0.3);
      plaza.strokeCircle(CENTER_X, CENTER_Y, 40);
    }
    /* ═══════════════════════════════════════════
       Decorations
       ═══════════════════════════════════════════ */
    drawDecorations() {
      const rng = seededRandom(123);
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
      const innerTreeAngles = [0.3, 0.9, 1.5, 2.1, 2.7, 3.3, 3.9, 4.5, 5.1, 5.7];
      for (const a of innerTreeAngles) {
        const dist = VILLAGE_RADIUS - 40 - rng() * 60;
        const tx = CENTER_X + Math.cos(a) * dist;
        const ty = CENTER_Y + Math.sin(a) * dist;
        this.drawOakTree(tx, ty, 0.5 + rng() * 0.3);
      }
      for (let i = 0; i < 25; i++) {
        const angle = rng() * Math.PI * 2;
        const dist = 100 + rng() * (VILLAGE_RADIUS - 130);
        const bx = CENTER_X + Math.cos(angle) * dist;
        const by = CENTER_Y + Math.sin(angle) * dist;
        const tooClose = this.buildings.some((b) => {
          const bdx = CENTER_X + b.x - bx;
          const bdy = CENTER_Y + b.y - by;
          return Math.sqrt(bdx * bdx + bdy * bdy) < 80;
        });
        if (!tooClose) {
          this.drawBush(bx, by, 0.5 + rng() * 0.6);
        }
      }
      for (let i = 0; i < 15; i++) {
        const angle = rng() * Math.PI * 2;
        const dist = 80 + rng() * (VILLAGE_RADIUS - 120);
        const fx = CENTER_X + Math.cos(angle) * dist;
        const fy = CENTER_Y + Math.sin(angle) * dist;
        const tooClose = this.buildings.some((b) => {
          const bdx = CENTER_X + b.x - fx;
          const bdy = CENTER_Y + b.y - fy;
          return Math.sqrt(bdx * bdx + bdy * bdy) < 70;
        });
        if (!tooClose) {
          this.drawFlowers(fx, fy, rng);
        }
      }
      const rockPositions = [
        { x: -100, y: -340 },
        { x: 280, y: 320 },
        { x: -340, y: 100 },
        { x: 100, y: 370 },
        { x: 350, y: -100 },
        { x: -200, y: 280 }
      ];
      for (const pos of rockPositions) {
        this.drawRockCluster(CENTER_X + pos.x, CENTER_Y + pos.y, rng);
      }
      this.drawRiver();
    }
    drawPineTree(x, y, scale) {
      const g = this.add.graphics();
      g.fillStyle(0, 0.12);
      g.fillEllipse(x + 3, y + 22 * scale, 18 * scale, 8 * scale);
      g.fillStyle(5913114, 1);
      g.fillRect(x - 3 * scale, y, 6 * scale, 18 * scale);
      const layers = [
        { yOff: -8, w: 22, h: 24 },
        { yOff: -20, w: 18, h: 20 },
        { yOff: -30, w: 13, h: 16 }
      ];
      for (const layer of layers) {
        g.fillStyle(2775584, 1);
        g.beginPath();
        g.moveTo(x - layer.w * scale / 2, y + layer.yOff * scale + layer.h * scale);
        g.lineTo(x, y + layer.yOff * scale);
        g.lineTo(x + layer.w * scale / 2, y + layer.yOff * scale + layer.h * scale);
        g.closePath();
        g.fillPath();
        g.fillStyle(3832366, 0.4);
        g.beginPath();
        g.moveTo(x - layer.w * scale / 4, y + layer.yOff * scale + layer.h * scale * 0.4);
        g.lineTo(x, y + layer.yOff * scale + 2);
        g.lineTo(x + layer.w * scale / 4, y + layer.yOff * scale + layer.h * scale * 0.6);
        g.closePath();
        g.fillPath();
      }
    }
    drawOakTree(x, y, scale) {
      const g = this.add.graphics();
      g.fillStyle(0, 0.12);
      g.fillEllipse(x + 3, y + 18 * scale, 26 * scale, 10 * scale);
      g.fillStyle(6044958, 1);
      g.fillRect(x - 4 * scale, y - 4 * scale, 8 * scale, 22 * scale);
      g.fillStyle(7228968, 0.5);
      g.fillRect(x - 2 * scale, y - 4 * scale, 4 * scale, 22 * scale);
      const crownCircles = [
        { dx: 0, dy: -18, r: 16 },
        { dx: -10, dy: -14, r: 13 },
        { dx: 10, dy: -14, r: 13 },
        { dx: -6, dy: -24, r: 11 },
        { dx: 6, dy: -22, r: 12 }
      ];
      for (const c of crownCircles) {
        g.fillStyle(3042850, 1);
        g.fillCircle(x + c.dx * scale, y + c.dy * scale, c.r * scale);
      }
      g.fillStyle(4099634, 0.4);
      g.fillCircle(x - 3 * scale, y - 22 * scale, 8 * scale);
      g.fillCircle(x + 5 * scale, y - 16 * scale, 7 * scale);
    }
    drawBush(x, y, scale) {
      const g = this.add.graphics();
      g.fillStyle(0, 0.08);
      g.fillEllipse(x + 2, y + 5 * scale, 16 * scale, 6 * scale);
      g.fillStyle(3828266, 1);
      g.fillCircle(x, y, 8 * scale);
      g.fillCircle(x - 5 * scale, y + 2 * scale, 6 * scale);
      g.fillCircle(x + 5 * scale, y + 1 * scale, 7 * scale);
      g.fillStyle(4885050, 0.5);
      g.fillCircle(x + 2 * scale, y - 2 * scale, 5 * scale);
    }
    drawFlowers(x, y, rng) {
      const g = this.add.graphics();
      const flowerColors = [16737928, 16755268, 16768341, 11176191, 16746666];
      for (let i = 0; i < 4 + Math.floor(rng() * 4); i++) {
        const fx = x + (rng() - 0.5) * 20;
        const fy = y + (rng() - 0.5) * 14;
        const color = flowerColors[Math.floor(rng() * flowerColors.length)];
        g.fillStyle(4885050, 0.6);
        g.fillRect(fx, fy, 1, 4);
        g.fillStyle(color, 0.8);
        g.fillCircle(fx, fy, 2.5);
        g.fillStyle(16772744, 0.9);
        g.fillCircle(fx, fy, 1);
      }
    }
    drawRockCluster(x, y, rng) {
      const g = this.add.graphics();
      for (let i = 0; i < 2 + Math.floor(rng() * 3); i++) {
        const rx = x + (rng() - 0.5) * 16;
        const ry = y + (rng() - 0.5) * 10;
        const rw = 6 + rng() * 10;
        const rh = 4 + rng() * 7;
        g.fillStyle(0, 0.1);
        g.fillEllipse(rx + 2, ry + rh / 2 + 2, rw + 2, rh / 2);
        g.fillStyle(7829367, 1);
        g.fillRoundedRect(rx - rw / 2, ry - rh / 2, rw, rh, 3);
        g.fillStyle(10066329, 0.5);
        g.fillRoundedRect(rx - rw / 2 + 2, ry - rh / 2 + 1, rw * 0.6, rh * 0.5, 2);
      }
    }
    drawRiver() {
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
        { x: W - 30, y: W }
      ];
      const drawCurve = (gfx, pts) => {
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
      river.lineStyle(28, 3824186, 0.4);
      drawCurve(river, points);
      river.lineStyle(20, 3832507, 0.7);
      drawCurve(river, points);
      river.lineStyle(14, 5937877, 0.6);
      drawCurve(river, points);
      const hl = points.map((p) => ({ x: p.x + 3, y: p.y }));
      river.lineStyle(4, 9357552, 0.35);
      drawCurve(river, hl);
    }
    /* ═══════════════════════════════════════════
       Buildings
       ═══════════════════════════════════════════ */
    createBuilding(slot) {
      const bx = CENTER_X + slot.x;
      const by = CENTER_Y + slot.y;
      const isTownHall = slot.type === BuildingType.TownHall;
      const s = isTownHall ? 1.4 : 1;
      const colors = BUILDING_COLORS[slot.type];
      const container = this.add.container(bx, by);
      const ground = this.add.graphics();
      ground.fillStyle(6969914, 0.25);
      ground.fillEllipse(0, 22 * s, 80 * s, 28 * s);
      container.add(ground);
      const shadow = this.add.graphics();
      shadow.fillStyle(0, 0.18);
      shadow.fillEllipse(5 * s, 28 * s, 72 * s, 16 * s);
      container.add(shadow);
      const foundation = this.add.graphics();
      foundation.fillStyle(6974046, 1);
      foundation.fillRoundedRect(-34 * s, 18 * s, 68 * s, 12 * s, 2);
      foundation.lineStyle(1, 4868670, 0.5);
      foundation.strokeRoundedRect(-34 * s, 18 * s, 68 * s, 12 * s, 2);
      container.add(foundation);
      const walls = this.add.graphics();
      walls.fillStyle(colors.base, 1);
      walls.fillRoundedRect(-32 * s, -14 * s, 64 * s, 34 * s, 3);
      const lighterBase = Phaser2.Display.Color.IntegerToColor(colors.base);
      const lighter = Phaser2.Display.Color.GetColor(
        Math.min(255, lighterBase.red + 25),
        Math.min(255, lighterBase.green + 25),
        Math.min(255, lighterBase.blue + 25)
      );
      walls.fillStyle(lighter, 0.5);
      walls.fillRoundedRect(-30 * s, -10 * s, 60 * s, 28 * s, 2);
      walls.lineStyle(1.5, 3355443, 0.35);
      walls.strokeRoundedRect(-32 * s, -14 * s, 64 * s, 34 * s, 3);
      container.add(walls);
      if (!isTownHall) {
        const timber = this.add.graphics();
        timber.lineStyle(2, 4861976, 0.35);
        timber.beginPath();
        timber.moveTo(-30 * s, 2 * s);
        timber.lineTo(30 * s, 2 * s);
        timber.strokePath();
        timber.beginPath();
        timber.moveTo(-12 * s, -12 * s);
        timber.lineTo(-12 * s, 18 * s);
        timber.strokePath();
        timber.beginPath();
        timber.moveTo(12 * s, -12 * s);
        timber.lineTo(12 * s, 18 * s);
        timber.strokePath();
        container.add(timber);
      }
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
        roof.fillStyle(16766720, 0.3);
        roof.fillRect(-6 * s, -50 * s, 12 * s, 4 * s);
        const darkerRoof = Phaser2.Display.Color.IntegerToColor(colors.roof);
        const darker = Phaser2.Display.Color.GetColor(
          Math.max(0, darkerRoof.red - 30),
          Math.max(0, darkerRoof.green - 30),
          Math.max(0, darkerRoof.blue - 30)
        );
        roof.fillStyle(darker, 0.4);
        roof.beginPath();
        roof.moveTo(0, -50 * s);
        roof.lineTo(42 * s, -14 * s);
        roof.lineTo(8 * s, -50 * s);
        roof.closePath();
        roof.fillPath();
        roof.lineStyle(1.5, 2759178, 0.4);
        roof.beginPath();
        roof.moveTo(-42 * s, -14 * s);
        roof.lineTo(-8 * s, -50 * s);
        roof.lineTo(8 * s, -50 * s);
        roof.lineTo(42 * s, -14 * s);
        roof.closePath();
        roof.strokePath();
      } else {
        roof.fillStyle(colors.roof, 1);
        roof.beginPath();
        roof.moveTo(-38 * s, -14 * s);
        roof.lineTo(0, -44 * s);
        roof.lineTo(38 * s, -14 * s);
        roof.closePath();
        roof.fillPath();
        const darkerRoof = Phaser2.Display.Color.IntegerToColor(colors.roof);
        const darker = Phaser2.Display.Color.GetColor(
          Math.max(0, darkerRoof.red - 30),
          Math.max(0, darkerRoof.green - 30),
          Math.max(0, darkerRoof.blue - 30)
        );
        roof.fillStyle(darker, 0.35);
        roof.beginPath();
        roof.moveTo(0, -44 * s);
        roof.lineTo(38 * s, -14 * s);
        roof.lineTo(0, -14 * s);
        roof.closePath();
        roof.fillPath();
        roof.lineStyle(1.5, 2759178, 0.4);
        roof.beginPath();
        roof.moveTo(-38 * s, -14 * s);
        roof.lineTo(0, -44 * s);
        roof.lineTo(38 * s, -14 * s);
        roof.closePath();
        roof.strokePath();
      }
      container.add(roof);
      const win = this.add.graphics();
      if (isTownHall) {
        const winPositions = [-16, 0, 16];
        for (const wx of winPositions) {
          win.fillStyle(3811866, 1);
          win.fillRoundedRect(wx * s - 5 * s, -6 * s, 10 * s, 14 * s, { tl: 5, tr: 5, bl: 0, br: 0 });
          win.fillStyle(16772795, 0.85);
          win.fillRoundedRect(wx * s - 3.5 * s, -4.5 * s, 7 * s, 11 * s, { tl: 3.5, tr: 3.5, bl: 0, br: 0 });
          win.fillStyle(16768392, 0.3);
          win.fillRoundedRect(wx * s - 2 * s, -3 * s, 4 * s, 8 * s, { tl: 2, tr: 2, bl: 0, br: 0 });
        }
        win.fillStyle(3809296, 1);
        win.fillRoundedRect(-8 * s, 6 * s, 16 * s, 14 * s, { tl: 8, tr: 8, bl: 0, br: 0 });
        win.fillStyle(4861984, 0.5);
        win.fillRoundedRect(-6 * s, 8 * s, 12 * s, 10 * s, { tl: 6, tr: 6, bl: 0, br: 0 });
        win.fillStyle(13412932, 1);
        win.fillCircle(3 * s, 14 * s, 1.5 * s);
      } else {
        for (const wx of [-14, 8]) {
          win.fillStyle(3811866, 1);
          win.fillRoundedRect(wx * s - 1, -6 * s, 9 * s, 11 * s, 2);
          win.fillStyle(16772795, 0.75);
          win.fillRoundedRect(wx * s + 0.5, -4.5 * s, 6.5 * s, 8.5 * s, 1.5);
          win.lineStyle(1, 5917242, 0.5);
          const wcx = wx * s + 3.75 * s;
          const wcy = -0.25 * s;
          win.beginPath();
          win.moveTo(wcx, -4.5 * s);
          win.lineTo(wcx, 4 * s);
          win.strokePath();
          win.beginPath();
          win.moveTo(wx * s + 0.5, wcy);
          win.lineTo(wx * s + 7, wcy);
          win.strokePath();
        }
        win.fillStyle(3809296, 1);
        win.fillRoundedRect(-5 * s, 4 * s, 10 * s, 16 * s, { tl: 5, tr: 5, bl: 0, br: 0 });
        win.fillStyle(4861984, 0.4);
        win.fillRoundedRect(-3.5 * s, 6 * s, 7 * s, 12 * s, { tl: 3.5, tr: 3.5, bl: 0, br: 0 });
        win.fillStyle(13412932, 1);
        win.fillCircle(2.5 * s, 12 * s, 1.2 * s);
      }
      container.add(win);
      if (slot.type !== BuildingType.Farm) {
        const chimney = this.add.graphics();
        const cx = isTownHall ? 20 * s : 14 * s;
        const cy = isTownHall ? -42 * s : -34 * s;
        chimney.fillStyle(6969930, 1);
        chimney.fillRect(cx, cy, 8 * s, 16 * s);
        chimney.fillStyle(5917242, 0.5);
        chimney.fillRect(cx, cy, 3 * s, 16 * s);
        chimney.fillStyle(8022618, 1);
        chimney.fillRect(cx - 1, cy - 2, 10 * s, 3);
        container.add(chimney);
      }
      this.addBuildingAccent(container, slot, s);
      const badgeX = 28 * s;
      const badgeY = -30 * s;
      const badgeR = isTownHall ? 14 : 12;
      const badge = this.add.graphics();
      badge.fillStyle(0, 0.2);
      badge.fillCircle(badgeX + 1, badgeY + 1, badgeR + 1);
      badge.fillStyle(2775722, 1);
      badge.fillCircle(badgeX, badgeY, badgeR);
      badge.lineStyle(2, 4885230, 0.8);
      badge.strokeCircle(badgeX, badgeY, badgeR);
      badge.fillStyle(3828428, 0.5);
      badge.fillCircle(badgeX - 1, badgeY - 1, badgeR - 3);
      container.add(badge);
      const levelText = this.add.text(badgeX, badgeY, `${slot.level}`, {
        fontSize: `${isTownHall ? 15 : 13}px`,
        fontFamily: '"Segoe UI", Arial, sans-serif',
        color: "#ffffff",
        fontStyle: "bold",
        stroke: "#1a3a6a",
        strokeThickness: 2
      }).setOrigin(0.5);
      container.add(levelText);
      const nameLabel = this.add.text(0, 36 * s, slot.name, {
        fontSize: `${isTownHall ? 13 : 11}px`,
        fontFamily: '"Segoe UI", Arial, sans-serif',
        color: "#f0e8d0",
        fontStyle: "bold",
        stroke: "#1a1a0a",
        strokeThickness: 3,
        shadow: { offsetX: 1, offsetY: 1, color: "#000000", blur: 3, fill: true, stroke: true }
      }).setOrigin(0.5, 0);
      container.add(nameLabel);
      const hitW = 80 * s;
      const hitH = 100 * s;
      const hitArea = new Phaser2.Geom.Rectangle(-hitW / 2, -55 * s, hitW, hitH);
      container.setSize(hitW, hitH);
      container.setInteractive(hitArea, Phaser2.Geom.Rectangle.Contains);
      container.on("pointerover", () => {
        this.tweens.add({ targets: container, scaleX: 1.06, scaleY: 1.06, duration: 120, ease: "Back.easeOut" });
        this.input.setDefaultCursor("pointer");
      });
      container.on("pointerout", () => {
        this.tweens.add({ targets: container, scaleX: 1, scaleY: 1, duration: 120, ease: "Back.easeOut" });
        this.input.setDefaultCursor("default");
      });
      container.on("pointerup", (pointer) => {
        if (this.isDragging) return;
        if (pointer.getDistance() > 10) return;
        this.showBuildingPopup(slot);
      });
      this.buildingContainers.set(slot.id, container);
      this.tweens.add({
        targets: container,
        y: by - 2,
        duration: 2500 + Math.random() * 1500,
        ease: "Sine.easeInOut",
        yoyo: true,
        repeat: -1
      });
      if (slot.type !== BuildingType.Farm) {
        this.addSmoke(slot, s);
      }
    }
    addBuildingAccent(container, slot, s) {
      const g = this.add.graphics();
      switch (slot.type) {
        case BuildingType.TownHall: {
          g.lineStyle(2, 5917242, 1);
          g.beginPath();
          g.moveTo(0, -50 * s);
          g.lineTo(0, -68 * s);
          g.strokePath();
          g.fillStyle(13378082, 1);
          g.beginPath();
          g.moveTo(0, -68 * s);
          g.lineTo(14 * s, -63 * s);
          g.lineTo(0, -58 * s);
          g.closePath();
          g.fillPath();
          g.fillStyle(16729156, 0.4);
          g.beginPath();
          g.moveTo(0, -68 * s);
          g.lineTo(10 * s, -65 * s);
          g.lineTo(0, -62 * s);
          g.closePath();
          g.fillPath();
          break;
        }
        case BuildingType.LumberMill: {
          for (let i = 0; i < 4; i++) {
            g.fillStyle(8018474, 1);
            g.fillRoundedRect(-42 * s + i * 4, 8 * s + i * 3, 12, 6, 2);
            g.fillStyle(9071162, 0.5);
            g.fillRoundedRect(-41 * s + i * 4, 8 * s + i * 3, 6, 3, 1);
          }
          break;
        }
        case BuildingType.ClayPit: {
          g.fillStyle(11557418, 1);
          g.fillRoundedRect(34 * s, 10 * s, 10, 12, { tl: 3, tr: 3, bl: 1, br: 1 });
          g.fillRoundedRect(42 * s, 14 * s, 8, 8, { tl: 2, tr: 2, bl: 1, br: 1 });
          g.fillStyle(12610106, 0.4);
          g.fillRoundedRect(35 * s, 11 * s, 5, 6, 1);
          break;
        }
        case BuildingType.IronMine: {
          g.fillStyle(5592405, 1);
          g.fillRect(-40 * s, 14 * s, 12, 6);
          g.fillRect(-37 * s, 10 * s, 6, 4);
          g.lineStyle(2, 6969930, 1);
          g.beginPath();
          g.moveTo(-44 * s, 6 * s);
          g.lineTo(-36 * s, 18 * s);
          g.strokePath();
          break;
        }
        case BuildingType.Farm: {
          g.fillStyle(13412932, 1);
          g.fillRoundedRect(34 * s, 12 * s, 14, 10, 3);
          g.fillRoundedRect(38 * s, 6 * s, 12, 8, 3);
          g.fillStyle(14531413, 0.4);
          g.fillRoundedRect(35 * s, 13 * s, 7, 5, 1);
          g.lineStyle(2, 8022602, 0.7);
          g.beginPath();
          g.moveTo(-44 * s, 20 * s);
          g.lineTo(-44 * s, 10 * s);
          g.strokePath();
          g.beginPath();
          g.moveTo(-36 * s, 20 * s);
          g.lineTo(-36 * s, 10 * s);
          g.strokePath();
          g.beginPath();
          g.moveTo(-46 * s, 14 * s);
          g.lineTo(-34 * s, 14 * s);
          g.strokePath();
          break;
        }
      }
      container.add(g);
    }
    /* ═══════════════════════════════════════════
       Ambient Details
       ═══════════════════════════════════════════ */
    drawAmbientDetails() {
      const well = this.add.graphics();
      well.fillStyle(0, 0.15);
      well.fillEllipse(CENTER_X + 2, CENTER_Y + 10, 24, 10);
      well.fillStyle(6974046, 1);
      well.fillCircle(CENTER_X, CENTER_Y, 12);
      well.lineStyle(2, 4868670, 0.7);
      well.strokeCircle(CENTER_X, CENTER_Y, 12);
      well.fillStyle(4885179, 0.6);
      well.fillCircle(CENTER_X, CENTER_Y, 8);
      well.fillStyle(5917242, 1);
      well.fillRect(CENTER_X - 10, CENTER_Y - 18, 3, 20);
      well.fillRect(CENTER_X + 7, CENTER_Y - 18, 3, 20);
      well.fillStyle(6969930, 1);
      well.fillRect(CENTER_X - 11, CENTER_Y - 20, 22, 3);
      well.lineStyle(1, 9075290, 0.5);
      well.beginPath();
      well.moveTo(CENTER_X, CENTER_Y - 18);
      well.lineTo(CENTER_X + 3, CENTER_Y - 5);
      well.strokePath();
      const gateAngles = [
        Math.atan2(-1, -1),
        Math.atan2(-1, 1),
        Math.atan2(1, -1),
        Math.atan2(1, 1)
      ];
      const R = VILLAGE_RADIUS + 10;
      for (const angle of gateAngles) {
        const tx = CENTER_X + Math.cos(angle) * R;
        const ty = CENTER_Y + Math.sin(angle) * R;
        this.add.particles(tx, ty - 12, "glow", {
          speed: { min: 3, max: 8 },
          angle: { min: 250, max: 290 },
          scale: { start: 0.6, end: 0 },
          alpha: { start: 0.5, end: 0 },
          lifespan: { min: 600, max: 1200 },
          frequency: 400,
          quantity: 1,
          tint: 16746547
        });
      }
    }
    /* ═══════════════════════════════════════════
       Smoke
       ═══════════════════════════════════════════ */
    addSmoke(slot, s) {
      const bx = CENTER_X + slot.x;
      const by = CENTER_Y + slot.y;
      const isTownHall = slot.type === BuildingType.TownHall;
      const cx = isTownHall ? 24 * s : 18 * s;
      const cy = isTownHall ? -46 * s : -38 * s;
      const emitter = this.add.particles(bx + cx, by + cy, "smoke", {
        speed: { min: 3, max: 10 },
        angle: { min: 255, max: 285 },
        scale: { start: 0.3, end: 1.2 },
        alpha: { start: 0.3, end: 0 },
        lifespan: { min: 2e3, max: 3500 },
        frequency: 1200,
        quantity: 1,
        tint: [12303291, 11184810, 10066329]
      });
      this.smokeEmitters.push(emitter);
    }
    /* ═══════════════════════════════════════════
       Building Popup
       ═══════════════════════════════════════════ */
    showBuildingPopup(slot) {
      if (this.popup) {
        this.popup.destroy();
        this.popup = null;
      }
      const bx = CENTER_X + slot.x;
      const by = CENTER_Y + slot.y;
      this.popup = new BuildingPopup(this, bx, by - 90, slot, this.resources, (id) => this.handleUpgrade(id));
    }
    handleUpgrade(buildingId) {
      const building = this.buildings.find((b) => b.id === buildingId);
      if (!building) return;
      const cost2 = upgradeCostForLevel(building.upgradeCost, building.level);
      const wood = this.resources.find((r) => r.type === ResourceType.Wood);
      const clay = this.resources.find((r) => r.type === ResourceType.Clay);
      const iron = this.resources.find((r) => r.type === ResourceType.Iron);
      const crop = this.resources.find((r) => r.type === ResourceType.Crop);
      if (wood.amount < cost2.wood || clay.amount < cost2.clay || iron.amount < cost2.iron || crop.amount < cost2.crop) return;
      wood.amount -= cost2.wood;
      clay.amount -= cost2.clay;
      iron.amount -= cost2.iron;
      crop.amount -= cost2.crop;
      building.level++;
      const resourceMap = {
        [BuildingType.LumberMill]: ResourceType.Wood,
        [BuildingType.ClayPit]: ResourceType.Clay,
        [BuildingType.IronMine]: ResourceType.Iron,
        [BuildingType.Farm]: ResourceType.Crop
      };
      const resType = resourceMap[building.type];
      if (resType) {
        this.resources.find((r) => r.type === resType).production = productionPerSecond(building.level);
      }
      if (building.type === BuildingType.TownHall) {
        const cap = storageCapacity(building.level);
        for (const r of this.resources) r.capacity = cap;
      }
      const container = this.buildingContainers.get(buildingId);
      if (container) {
        container.destroy();
        this.buildingContainers.delete(buildingId);
        this.createBuilding(building);
      }
      const bx = CENTER_X + building.x;
      const by = CENTER_Y + building.y;
      const flash = this.add.graphics();
      flash.fillStyle(16777130, 0.35);
      flash.fillCircle(bx, by, 60);
      this.tweens.add({ targets: flash, alpha: 0, duration: 700, onComplete: () => flash.destroy() });
      const lvText = this.add.text(bx, by - 70, `⬆ Nv. ${building.level}`, {
        fontSize: "15px",
        fontFamily: '"Segoe UI", Arial',
        color: "#ffd700",
        fontStyle: "bold",
        stroke: "#1a1a0a",
        strokeThickness: 3
      }).setOrigin(0.5);
      this.tweens.add({ targets: lvText, y: by - 120, alpha: 0, duration: 1200, ease: "Cubic.easeOut", onComplete: () => lvText.destroy() });
      if (this.popup) {
        this.popup.destroy();
        this.popup = null;
      }
      this.showBuildingPopup(building);
      this.pushResourcesToBlazor();
    }
    /* ═══════════════════════════════════════════
       Resource Ticking
       ═══════════════════════════════════════════ */
    tickResources() {
      for (const r of this.resources) r.amount = Math.min(r.amount + r.production, r.capacity);
      this.pushResourcesToBlazor();
      this.showProductionFloat();
    }
    pushResourcesToBlazor() {
      if (!this.netRef) return;
      const data = {
        wood: Math.floor(this.resources.find((r) => r.type === ResourceType.Wood).amount),
        clay: Math.floor(this.resources.find((r) => r.type === ResourceType.Clay).amount),
        iron: Math.floor(this.resources.find((r) => r.type === ResourceType.Iron).amount),
        crop: Math.floor(this.resources.find((r) => r.type === ResourceType.Crop).amount)
      };
      this.netRef.invokeMethodAsync("UpdateResources", data.wood, data.clay, data.iron, data.crop).catch(() => {
      });
    }
    showProductionFloat() {
      const prodBuildings = this.buildings.filter((b2) => b2.type !== BuildingType.TownHall);
      const b = prodBuildings[Math.floor(Math.random() * prodBuildings.length)];
      if (!b) return;
      const resMap = {
        [BuildingType.LumberMill]: { icon: "🪵", color: "#d4a843" },
        [BuildingType.ClayPit]: { icon: "🧱", color: "#c2703a" },
        [BuildingType.IronMine]: { icon: "⛏️", color: "#99b3c4" },
        [BuildingType.Farm]: { icon: "🌾", color: "#88cc55" }
      };
      const info = resMap[b.type];
      if (!info) return;
      const bx = CENTER_X + b.x;
      const by = CENTER_Y + b.y;
      const prod = productionPerSecond(b.level);
      const txt = this.add.text(bx + (Math.random() - 0.5) * 25, by - 25, `${info.icon}+${formatNumber(prod)}`, {
        fontSize: "12px",
        fontFamily: '"Segoe UI", Arial',
        color: info.color,
        fontStyle: "bold",
        stroke: "#000000",
        strokeThickness: 2
      }).setOrigin(0.5);
      this.tweens.add({ targets: txt, y: by - 70, alpha: 0, duration: 1800, ease: "Cubic.easeOut", onComplete: () => txt.destroy() });
    }
    /* ═══════════════════════════════════════════
       Camera Controls
       ═══════════════════════════════════════════ */
    setupCameraControls() {
      this.input.on("pointerdown", (pointer) => {
        this.isDragging = false;
        this.dragStartX = pointer.x;
        this.dragStartY = pointer.y;
      });
      this.input.on("pointermove", (pointer) => {
        if (!pointer.isDown) return;
        if (Math.abs(pointer.x - this.dragStartX) > 5 || Math.abs(pointer.y - this.dragStartY) > 5) {
          this.isDragging = true;
        }
        if (this.isDragging) {
          this.cameras.main.scrollX -= (pointer.x - pointer.prevPosition.x) / this.cameras.main.zoom;
          this.cameras.main.scrollY -= (pointer.y - pointer.prevPosition.y) / this.cameras.main.zoom;
        }
      });
      this.input.on("wheel", (_p, _go, _dx, dy) => {
        const cam = this.cameras.main;
        cam.setZoom(Phaser2.Math.Clamp(cam.zoom - dy * 1e-3, 0.4, 2.5));
      });
      this.input.addPointer(1);
    }
    /* ═══════════════════════════════════════════
       Cleanup
       ═══════════════════════════════════════════ */
    destroy() {
      if (this.resourceTimer) {
        this.resourceTimer.destroy();
        this.resourceTimer = null;
      }
      if (this.popup) {
        this.popup.destroy();
        this.popup = null;
      }
      for (const emitter of this.smokeEmitters) emitter.destroy();
      this.smokeEmitters = [];
      this.buildingContainers.clear();
      this.netRef = null;
    }
  }
  let game = null;
  function createVillageApi() {
    return {
      async start(containerId, netRef) {
        if (game) {
          game.destroy(true);
          game = null;
        }
        const parent = document.getElementById(containerId);
        if (!parent) {
          console.error(`[VillageMode] Container #${containerId} not found`);
          return;
        }
        game = new Phaser2.Game({
          type: Phaser2.AUTO,
          parent: containerId,
          width: parent.clientWidth,
          height: parent.clientHeight,
          backgroundColor: "#2a4a20",
          scene: [VillageScene],
          scale: {
            mode: Phaser2.Scale.RESIZE,
            autoCenter: Phaser2.Scale.CENTER_BOTH
          },
          render: {
            antialias: true,
            pixelArt: false
          },
          input: {
            mouse: {
              preventDefaultWheel: true
            }
          }
        });
        game.scene.start("VillageScene", { netRef });
      },
      destroy() {
        if (game) {
          game.destroy(true);
          game = null;
        }
      },
      isActive() {
        return game !== null;
      }
    };
  }
  window.villageModeGame = createVillageApi();
})(Phaser);
//# sourceMappingURL=phaserVillage.js.map
