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
    // Ground & environment
    groundGreen: 4881471,
    groundDark: 3826480,
    pathTan: 12888426,
    pathBorder: 10389588,
    water: 6003669,
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
    // UI
    badgeBg: 16777215,
    badgeText: "#333333",
    badgeBorder: 6710886,
    labelText: "#ffffff",
    labelShadow: "#000000",
    shadowColor: 0,
    // Tree decorations
    treeTrunk: 7029286,
    treeLeaves: 3832362,
    treeLeavesLight: 4889146,
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
  const WORLD_SIZE = 1200;
  const GROUND_RADIUS = 480;
  const CENTER_X = WORLD_SIZE / 2;
  const CENTER_Y = WORLD_SIZE / 2;
  const RESOURCE_TICK_MS = 1e3;
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
      this.drawGround();
      this.drawPaths();
      this.drawDecorations();
      for (const b of this.buildings) {
        this.createBuilding(b);
      }
      this.setupCameraControls();
      this.resourceTimer = this.time.addEvent({
        delay: RESOURCE_TICK_MS,
        callback: this.tickResources,
        callbackScope: this,
        loop: true
      });
      this.createSmokeTexture();
      for (const b of this.buildings) {
        if (b.type !== BuildingType.TownHall) {
          this.addSmoke(b);
        }
      }
    }
    /* ─── Ground ─── */
    drawGround() {
      const outerBg = this.add.graphics();
      outerBg.fillStyle(2771488, 1);
      outerBg.fillRect(0, 0, WORLD_SIZE, WORLD_SIZE);
      const ground = this.add.graphics();
      ground.fillStyle(COLORS.groundGreen, 1);
      ground.fillCircle(CENTER_X, CENTER_Y, GROUND_RADIUS);
      const inner = this.add.graphics();
      inner.fillStyle(COLORS.groundDark, 0.3);
      inner.fillCircle(CENTER_X, CENTER_Y, GROUND_RADIUS - 40);
      const ring = this.add.graphics();
      ring.lineStyle(6, 8018480, 0.8);
      ring.strokeCircle(CENTER_X, CENTER_Y, GROUND_RADIUS);
      ring.lineStyle(2, 10385988, 0.4);
      ring.strokeCircle(CENTER_X, CENTER_Y, GROUND_RADIUS - 15);
    }
    /* ─── Paths ─── */
    drawPaths() {
      const paths = this.add.graphics();
      paths.lineStyle(16, COLORS.pathTan, 0.7);
      for (const b of this.buildings) {
        if (b.type === BuildingType.TownHall) continue;
        const bx = CENTER_X + b.x;
        const by = CENTER_Y + b.y;
        paths.beginPath();
        paths.moveTo(CENTER_X, CENTER_Y);
        paths.lineTo(bx, by);
        paths.strokePath();
      }
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
      const plaza = this.add.graphics();
      plaza.fillStyle(COLORS.pathTan, 0.5);
      plaza.fillCircle(CENTER_X, CENTER_Y, 50);
      plaza.lineStyle(3, COLORS.pathBorder, 0.6);
      plaza.strokeCircle(CENTER_X, CENTER_Y, 50);
    }
    /* ─── Decorations (trees, rocks, river) ─── */
    drawDecorations() {
      const treePositions = [
        { x: -350, y: -350 },
        { x: 350, y: -350 },
        { x: -350, y: 350 },
        { x: 350, y: 350 },
        { x: -420, y: 0 },
        { x: 420, y: 0 },
        { x: 0, y: -420 },
        { x: 0, y: 420 },
        { x: -300, y: -380 },
        { x: 300, y: -380 },
        { x: -380, y: -200 },
        { x: 380, y: -200 },
        { x: -380, y: 200 },
        { x: 380, y: 200 },
        { x: -300, y: 380 },
        { x: 300, y: 380 },
        // Additional scattered trees
        { x: -450, y: -150 },
        { x: 450, y: -150 },
        { x: -450, y: 150 },
        { x: 450, y: 150 },
        { x: -150, y: -450 },
        { x: 150, y: -450 },
        { x: -150, y: 450 },
        { x: 150, y: 450 }
      ];
      for (const pos of treePositions) {
        this.drawTree(CENTER_X + pos.x, CENTER_Y + pos.y, 0.6 + Math.random() * 0.6);
      }
      const rockPositions = [
        { x: -100, y: -320 },
        { x: 280, y: 300 },
        { x: -320, y: 100 },
        { x: 100, y: 350 }
      ];
      for (const pos of rockPositions) {
        this.drawRock(CENTER_X + pos.x, CENTER_Y + pos.y);
      }
      this.drawRiver();
    }
    drawTree(x, y, scale) {
      const g = this.add.graphics();
      g.fillStyle(COLORS.shadowColor, 0.15);
      g.fillEllipse(x + 3, y + 20 * scale, 24 * scale, 10 * scale);
      g.fillStyle(COLORS.treeTrunk, 1);
      g.fillRect(x - 3 * scale, y - 5 * scale, 6 * scale, 20 * scale);
      g.fillStyle(COLORS.treeLeaves, 1);
      g.fillCircle(x, y - 15 * scale, 14 * scale);
      g.fillStyle(COLORS.treeLeavesLight, 0.7);
      g.fillCircle(x - 4 * scale, y - 20 * scale, 10 * scale);
      g.fillCircle(x + 5 * scale, y - 12 * scale, 10 * scale);
    }
    drawRock(x, y) {
      const g = this.add.graphics();
      g.fillStyle(8947848, 1);
      g.fillRoundedRect(x - 8, y - 5, 16, 10, 4);
      g.fillStyle(10066329, 0.7);
      g.fillRoundedRect(x - 5, y - 8, 10, 8, 3);
    }
    drawRiver() {
      const W = WORLD_SIZE;
      const points = [
        { x: W - 50, y: 100 },
        { x: W - 80, y: 250 },
        { x: W - 40, y: 400 },
        { x: W - 90, y: 550 },
        { x: W - 50, y: 700 },
        { x: W - 80, y: 850 },
        { x: W - 40, y: 1e3 },
        { x: W - 70, y: W - 50 }
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
      river.lineStyle(18, COLORS.water, 0.6);
      drawCurve(river, points);
      const highlight = points.map((p) => ({ x: p.x + 4, y: p.y }));
      river.lineStyle(6, 9356776, 0.3);
      drawCurve(river, highlight);
    }
    /* ─── Buildings ─── */
    createBuilding(slot) {
      const bx = CENTER_X + slot.x;
      const by = CENTER_Y + slot.y;
      const isTownHall = slot.type === BuildingType.TownHall;
      const scale = isTownHall ? 1.3 : 1;
      const colors = BUILDING_COLORS[slot.type];
      const container = this.add.container(bx, by);
      const shadow = this.add.graphics();
      shadow.fillStyle(COLORS.shadowColor, 0.2);
      shadow.fillEllipse(4, 30 * scale, 70 * scale, 18 * scale);
      container.add(shadow);
      const base = this.add.graphics();
      base.fillStyle(colors.base, 1);
      base.fillRoundedRect(-30 * scale, -10 * scale, 60 * scale, 40 * scale, 4);
      base.lineStyle(2, 3355443, 0.4);
      base.strokeRoundedRect(-30 * scale, -10 * scale, 60 * scale, 40 * scale, 4);
      container.add(base);
      const roof = this.add.graphics();
      roof.fillStyle(colors.roof, 1);
      roof.beginPath();
      roof.moveTo(-35 * scale, -10 * scale);
      roof.lineTo(0, -40 * scale);
      roof.lineTo(35 * scale, -10 * scale);
      roof.closePath();
      roof.fillPath();
      roof.lineStyle(2, 3355443, 0.3);
      roof.beginPath();
      roof.moveTo(-35 * scale, -10 * scale);
      roof.lineTo(0, -40 * scale);
      roof.lineTo(35 * scale, -10 * scale);
      roof.closePath();
      roof.strokePath();
      container.add(roof);
      if (isTownHall) {
        const win = this.add.graphics();
        win.fillStyle(16770208, 0.8);
        win.fillRect(-15, 2, 10, 10);
        win.fillRect(5, 2, 10, 10);
        win.fillRect(-5, 2, 10, 10);
        win.fillStyle(5913114, 1);
        win.fillRoundedRect(-6, 15, 12, 15, { tl: 6, tr: 6, bl: 0, br: 0 });
        container.add(win);
      } else {
        const win = this.add.graphics();
        win.fillStyle(16770208, 0.7);
        win.fillRect(-12 * scale, 2 * scale, 8 * scale, 8 * scale);
        win.fillRect(4 * scale, 2 * scale, 8 * scale, 8 * scale);
        win.fillStyle(5913114, 1);
        win.fillRoundedRect(-4 * scale, 14 * scale, 8 * scale, 14 * scale, { tl: 4, tr: 4, bl: 0, br: 0 });
        container.add(win);
      }
      const icon = BUILDING_ICONS[slot.type];
      const iconText = this.add.text(0, -52 * scale, icon, {
        fontSize: `${isTownHall ? 28 : 22}px`
      }).setOrigin(0.5);
      container.add(iconText);
      const badgeSize = isTownHall ? 16 : 14;
      const badge = this.add.graphics();
      badge.fillStyle(COLORS.badgeBg, 1);
      badge.fillCircle(22 * scale, -25 * scale, badgeSize);
      badge.lineStyle(2, COLORS.badgeBorder, 0.8);
      badge.strokeCircle(22 * scale, -25 * scale, badgeSize);
      container.add(badge);
      const levelText = this.add.text(22 * scale, -25 * scale, `${slot.level}`, {
        fontSize: `${isTownHall ? 16 : 14}px`,
        fontFamily: "Arial, sans-serif",
        color: COLORS.badgeText,
        fontStyle: "bold"
      }).setOrigin(0.5);
      container.add(levelText);
      const nameLabel = this.add.text(0, 38 * scale, slot.name, {
        fontSize: "12px",
        fontFamily: "Arial, sans-serif",
        color: COLORS.labelText,
        fontStyle: "bold",
        stroke: COLORS.labelShadow,
        strokeThickness: 3
      }).setOrigin(0.5, 0);
      container.add(nameLabel);
      const hitArea = new Phaser2.Geom.Rectangle(-35 * scale, -45 * scale, 70 * scale, 90 * scale);
      container.setSize(70 * scale, 90 * scale);
      container.setInteractive(hitArea, Phaser2.Geom.Rectangle.Contains);
      container.on("pointerover", () => {
        this.tweens.add({
          targets: container,
          scaleX: 1.08,
          scaleY: 1.08,
          duration: 150,
          ease: "Back.easeOut"
        });
        this.input.setDefaultCursor("pointer");
      });
      container.on("pointerout", () => {
        this.tweens.add({
          targets: container,
          scaleX: 1,
          scaleY: 1,
          duration: 150,
          ease: "Back.easeOut"
        });
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
        y: by - 3,
        duration: 2e3 + Math.random() * 1e3,
        ease: "Sine.easeInOut",
        yoyo: true,
        repeat: -1
      });
    }
    /* ─── Smoke Particles ─── */
    createSmokeTexture() {
      const g = this.add.graphics();
      g.fillStyle(13421772, 1);
      g.fillCircle(4, 4, 4);
      g.generateTexture("smoke", 8, 8);
      g.destroy();
    }
    addSmoke(slot) {
      const bx = CENTER_X + slot.x;
      const by = CENTER_Y + slot.y;
      const emitter = this.add.particles(bx + 10, by - 35, "smoke", {
        speed: { min: 5, max: 15 },
        angle: { min: 250, max: 290 },
        scale: { start: 0.5, end: 1.5 },
        alpha: { start: 0.4, end: 0 },
        lifespan: { min: 1500, max: 2500 },
        frequency: 800,
        quantity: 1,
        tint: 11184810
      });
      this.smokeEmitters.push(emitter);
    }
    /* ─── Building Popup ─── */
    showBuildingPopup(slot) {
      if (this.popup) {
        this.popup.destroy();
        this.popup = null;
      }
      const bx = CENTER_X + slot.x;
      const by = CENTER_Y + slot.y;
      this.popup = new BuildingPopup(this, bx, by - 80, slot, this.resources, (buildingId) => {
        this.handleUpgrade(buildingId);
      });
    }
    handleUpgrade(buildingId) {
      const building = this.buildings.find((b) => b.id === buildingId);
      if (!building) return;
      const cost2 = upgradeCostForLevel(building.upgradeCost, building.level);
      const wood = this.resources.find((r) => r.type === ResourceType.Wood);
      const clay = this.resources.find((r) => r.type === ResourceType.Clay);
      const iron = this.resources.find((r) => r.type === ResourceType.Iron);
      const crop = this.resources.find((r) => r.type === ResourceType.Crop);
      if (wood.amount < cost2.wood || clay.amount < cost2.clay || iron.amount < cost2.iron || crop.amount < cost2.crop) {
        return;
      }
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
        const res = this.resources.find((r) => r.type === resType);
        res.production = productionPerSecond(building.level);
      }
      if (building.type === BuildingType.TownHall) {
        const cap = storageCapacity(building.level);
        for (const r of this.resources) {
          r.capacity = cap;
        }
      }
      const container = this.buildingContainers.get(buildingId);
      if (container) {
        container.destroy();
        this.buildingContainers.delete(buildingId);
        this.createBuilding(building);
      }
      const flash = this.add.graphics();
      flash.fillStyle(16776960, 0.4);
      const bx = CENTER_X + building.x;
      const by = CENTER_Y + building.y;
      flash.fillCircle(bx, by, 50);
      this.tweens.add({
        targets: flash,
        alpha: 0,
        duration: 600,
        onComplete: () => flash.destroy()
      });
      const lvText = this.add.text(bx, by - 60, `⬆ Nv. ${building.level}`, {
        fontSize: "16px",
        fontFamily: "Arial, sans-serif",
        color: "#ffdd00",
        fontStyle: "bold",
        stroke: "#000000",
        strokeThickness: 3
      }).setOrigin(0.5);
      this.tweens.add({
        targets: lvText,
        y: by - 110,
        alpha: 0,
        duration: 1200,
        ease: "Cubic.easeOut",
        onComplete: () => lvText.destroy()
      });
      if (this.popup) {
        this.popup.destroy();
        this.popup = null;
      }
      this.showBuildingPopup(building);
      this.pushResourcesToBlazor();
    }
    /* ─── Resource Ticking ─── */
    tickResources() {
      for (const r of this.resources) {
        r.amount = Math.min(r.amount + r.production, r.capacity);
      }
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
        [BuildingType.IronMine]: { icon: "⛏️", color: "#7a8b99" },
        [BuildingType.Farm]: { icon: "🌾", color: "#6b9e3a" }
      };
      const info = resMap[b.type];
      if (!info) return;
      const bx = CENTER_X + b.x;
      const by = CENTER_Y + b.y;
      const prod = productionPerSecond(b.level);
      const txt = this.add.text(bx + (Math.random() - 0.5) * 30, by - 20, `${info.icon}+${formatNumber(prod)}`, {
        fontSize: "13px",
        fontFamily: "Arial, sans-serif",
        color: info.color,
        fontStyle: "bold",
        stroke: "#000000",
        strokeThickness: 2
      }).setOrigin(0.5);
      this.tweens.add({
        targets: txt,
        y: by - 65,
        alpha: 0,
        duration: 1500,
        ease: "Cubic.easeOut",
        onComplete: () => txt.destroy()
      });
    }
    /* ─── Camera Controls ─── */
    setupCameraControls() {
      this.input.on("pointerdown", (pointer) => {
        this.isDragging = false;
        this.dragStartX = pointer.x;
        this.dragStartY = pointer.y;
      });
      this.input.on("pointermove", (pointer) => {
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
      this.input.on("wheel", (_pointer, _gameObjects, _deltaX, deltaY) => {
        const cam = this.cameras.main;
        const newZoom = Phaser2.Math.Clamp(cam.zoom - deltaY * 1e-3, 0.5, 2);
        cam.setZoom(newZoom);
      });
      this.input.addPointer(1);
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
