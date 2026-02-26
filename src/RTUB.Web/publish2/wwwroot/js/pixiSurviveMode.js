var __defProp = Object.defineProperty;
var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
var __publicField = (obj, key, value) => __defNormalProp(obj, typeof key !== "symbol" ? key + "" : key, value);
(function(PIXI2) {
  "use strict";
  function _interopNamespaceDefault(e) {
    const n = Object.create(null, { [Symbol.toStringTag]: { value: "Module" } });
    if (e) {
      for (const k in e) {
        if (k !== "default") {
          const d = Object.getOwnPropertyDescriptor(e, k);
          Object.defineProperty(n, k, d.get ? d : {
            enumerable: true,
            get: () => e[k]
          });
        }
      }
    }
    n.default = e;
    return Object.freeze(n);
  }
  const PIXI__namespace = /* @__PURE__ */ _interopNamespaceDefault(PIXI2);
  const SESSION_CACHE_BUST = `?v=${Date.now()}`;
  const audioCacheBuster = `?v=${Date.now()}`;
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
  const ATTACK_RANGE = 180;
  const ATTACK_COOLDOWN = 0.45;
  const BASE_ATTACK_DAMAGE = 1;
  const PROJECTILE_SPEED = 350;
  const PROJECTILE_RADIUS = 4;
  const PROJECTILE_LIFETIME = 1.5;
  const BASE_ENEMY_HP = 2;
  const ELITE_HP_MULT = 3;
  const ENEMY_HP_PER_LEVEL = 1;
  const BOSS_HP_MULT = 80;
  const BOSS_SPEED_MULT = 0.75;
  const BOSS_DAMAGE = 50;
  const BOSS_HIT_RADIUS = 56;
  const UPGRADE_THRESHOLDS = [5, 10, 25, 65];
  const UPGRADE_THRESHOLD_STEP = 45;
  const UPGRADE_DEFS = [
    {
      id: "moveSpeed",
      icon: "🏃",
      title: "Pés Rápidos",
      desc: "+15% velocidade de movimento",
      apply: (s) => {
        s.playerSpeed *= 1.15;
      }
    },
    {
      id: "atkSpeed",
      icon: "⚡",
      title: "Fogo Rápido",
      desc: "+20% velocidade de ataque",
      apply: (s) => {
        s.attackCooldownMult *= 0.8;
      }
    },
    {
      id: "hp",
      icon: "❤️",
      title: "Vitalidade",
      desc: "+10 HP Máximo e cura 5",
      apply: (s) => {
        s.maxHP += 10;
        s.playerHP = Math.min(s.playerHP + 5, s.maxHP);
      }
    },
    {
      id: "damage",
      icon: "⚔️",
      title: "Tiro Potente",
      desc: "2x dano de ataque",
      apply: (s) => {
        s.attackDamage *= 2;
      }
    },
    {
      id: "coinRate",
      icon: "🪙",
      title: "Febre do Ouro",
      desc: "2x moedas por inimigo",
      maxPicks: 3,
      apply: (s) => {
        s.coinDropMult *= 2;
      }
    },
    {
      id: "atkRange",
      icon: "🎯",
      title: "Olho de Águia",
      desc: "+10% alcance de ataque",
      apply: (s) => {
        s.attackRange *= 1.1;
      }
    },
    {
      id: "magnet",
      icon: "🧲",
      title: "Íman de Moedas",
      desc: "Moedas voam para ti de longe",
      unique: true,
      apply: (s) => {
        s.magnetRadius += 150;
      }
    },
    {
      id: "companion",
      icon: "🐷",
      title: "Leitão",
      desc: "Um leitão pronto a ser praxado e atacar inimigos próximos",
      unique: true,
      apply: (s) => {
        s.spawnCompanion();
      }
    }
  ];
  const SFX_POOL_SIZE = 4;
  const SFX_MAP = {
    win: "/audio/games/my-tuno/survive/win.mp3",
    death: "/audio/games/my-tuno/survive/death.mp3",
    hit: "/audio/games/my-tuno/survive/hit.mp3"
  };
  const sfxPool = {};
  function getPooledAudio(type) {
    const src = SFX_MAP[type];
    if (!src) return null;
    if (!sfxPool[type]) {
      sfxPool[type] = [];
      for (let i = 0; i < SFX_POOL_SIZE; i++) {
        const a2 = new Audio(src + audioCacheBuster);
        a2.volume = 0.5;
        sfxPool[type].push(a2);
      }
    }
    for (const a2 of sfxPool[type]) {
      if (a2.paused || a2.ended) {
        a2.currentTime = 0;
        return a2;
      }
    }
    const a = sfxPool[type][0];
    a.currentTime = 0;
    return a;
  }
  function clamp(val, min, max) {
    return Math.max(min, Math.min(max, val));
  }
  function dist(a, b) {
    return Math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2);
  }
  function lerp(a, b, t) {
    return a + (b - a) * t;
  }
  function formatTime(s) {
    const m = Math.floor(s / 60);
    const sec = Math.floor(s % 60);
    return `${m}:${sec.toString().padStart(2, "0")}`;
  }
  class ObjectPool {
    constructor(factory, reset, initialSize = 0) {
      __publicField(this, "_factory");
      __publicField(this, "_reset");
      __publicField(this, "_pool");
      this._factory = factory;
      this._reset = reset;
      this._pool = [];
      for (let i = 0; i < initialSize; i++) {
        this._pool.push(this._factory());
      }
    }
    get() {
      if (this._pool.length > 0) return this._pool.pop();
      return this._factory();
    }
    release(obj) {
      this._reset(obj);
      this._pool.push(obj);
    }
    get size() {
      return this._pool.length;
    }
  }
  class SpatialGrid {
    constructor(cellSize) {
      __publicField(this, "cellSize");
      __publicField(this, "cells", /* @__PURE__ */ new Map());
      this.cellSize = cellSize;
    }
    _key(cx, cy) {
      return `${cx},${cy}`;
    }
    clear() {
      this.cells.clear();
    }
    insert(entity) {
      const cx = Math.floor(entity.x / this.cellSize);
      const cy = Math.floor(entity.y / this.cellSize);
      const key = this._key(cx, cy);
      let cell = this.cells.get(key);
      if (!cell) {
        cell = [];
        this.cells.set(key, cell);
      }
      cell.push(entity);
    }
    query(x, y) {
      const cx = Math.floor(x / this.cellSize);
      const cy = Math.floor(y / this.cellSize);
      const result = [];
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
  let app = null;
  let bgMusic = null;
  let bgMusicLoaded = false;
  let audioEnabled = true;
  class SurviveScene {
    // ──────────────────────────────────────────────────────────────
    constructor(containerId, levelData) {
      __publicField(this, "containerId");
      __publicField(this, "data");
      // Level config
      __publicField(this, "level");
      __publicField(this, "biomeName");
      __publicField(this, "timerDuration");
      __publicField(this, "baseEnemyCount");
      __publicField(this, "maxEnemyCount");
      __publicField(this, "spawnInterval");
      __publicField(this, "enemySpeed");
      __publicField(this, "maxEnemySpeed");
      __publicField(this, "playerSpeed");
      // public — upgrade mutates
      __publicField(this, "enemyScale");
      __publicField(this, "hasElites");
      __publicField(this, "eliteChance");
      __publicField(this, "mapWidth");
      __publicField(this, "mapHeight");
      __publicField(this, "vpWidth");
      __publicField(this, "vpHeight");
      __publicField(this, "backgroundPath");
      __publicField(this, "enemySprites");
      __publicField(this, "playerSpritePath");
      __publicField(this, "bossSprites");
      __publicField(this, "isFinalLevel");
      __publicField(this, "spawnRampPerMinute");
      __publicField(this, "speedRampPerMinute");
      // Runtime state
      __publicField(this, "timeRemaining");
      __publicField(this, "timeElapsed", 0);
      __publicField(this, "enemiesKilled", 0);
      __publicField(this, "xpOrbsCollected", 0);
      __publicField(this, "totalSpawned", 0);
      __publicField(this, "spawnTimer", 0);
      __publicField(this, "alive", true);
      __publicField(this, "won", false);
      __publicField(this, "speedRampTimer", 0);
      __publicField(this, "currentEnemySpeed");
      // Containers & entities
      __publicField(this, "worldContainer");
      __publicField(this, "uiContainer");
      __publicField(this, "enemies", []);
      __publicField(this, "_enemyContainerPool", []);
      __publicField(this, "_spatialGrid", new SpatialGrid(128));
      __publicField(this, "xpOrbs", []);
      __publicField(this, "particles", []);
      __publicField(this, "projectiles", []);
      // Object pools
      __publicField(this, "_projectilePool", null);
      __publicField(this, "_orbPool", null);
      __publicField(this, "_particlePool", null);
      // Auto-attack state
      __publicField(this, "attackCooldown", 0);
      __publicField(this, "attackDamage", BASE_ATTACK_DAMAGE);
      // public — upgrade mutates
      __publicField(this, "attackCooldownMult", 1);
      // public — upgrade mutates
      __publicField(this, "attackRange", ATTACK_RANGE);
      // public — upgrade mutates
      // Player HP
      __publicField(this, "playerHP", PLAYER_MAX_HP);
      // public — upgrade mutates
      __publicField(this, "maxHP", PLAYER_MAX_HP);
      // public — upgrade mutates
      __publicField(this, "invulnTimer", 0);
      // Upgrade system
      __publicField(this, "upgradeIndex", 0);
      __publicField(this, "nextUpgradeAt", UPGRADE_THRESHOLDS[0]);
      __publicField(this, "upgradesPicked", 0);
      __publicField(this, "maxPowerUps");
      __publicField(this, "pickedUpgradeIds", /* @__PURE__ */ new Set());
      __publicField(this, "upgradePickCounts", {});
      __publicField(this, "coinDropMult", 1);
      // public — upgrade mutates
      __publicField(this, "magnetRadius");
      // public — upgrade mutates
      __publicField(this, "companions", []);
      __publicField(this, "upgradePaused", false);
      __publicField(this, "upgradeOverlay", null);
      // Per-minute ramp
      __publicField(this, "lastMinuteRamp", 0);
      __publicField(this, "spawnRampBonus", 0);
      __publicField(this, "speedRampBonus", 0);
      // Boss state
      __publicField(this, "midBossSpawned", false);
      __publicField(this, "finalBossSpawned", false);
      __publicField(this, "activeBoss", null);
      __publicField(this, "bossHPBarBg", null);
      __publicField(this, "bossHPBarFill", null);
      __publicField(this, "bossHPBarText", null);
      __publicField(this, "_bossBarX", 0);
      __publicField(this, "_bossBarY", 0);
      __publicField(this, "_bossBarW", 0);
      __publicField(this, "_bossBarH", 0);
      __publicField(this, "_bossBarFinal", false);
      __publicField(this, "timerStopped", false);
      // Input
      __publicField(this, "keys", {});
      __publicField(this, "touchActive", false);
      __publicField(this, "touchTarget", { x: 0, y: 0 });
      __publicField(this, "joystick", null);
      __publicField(this, "joystickActive", false);
      __publicField(this, "joystickAngle", 0);
      __publicField(this, "joystickMagnitude", 0);
      __publicField(this, "_joystickPointerId", null);
      __publicField(this, "_isTouchDevice", false);
      // Event handler refs (for cleanup)
      __publicField(this, "_onKeyDown", null);
      __publicField(this, "_onKeyUp", null);
      __publicField(this, "_onPointerDown", null);
      __publicField(this, "_onPointerMove", null);
      __publicField(this, "_onPointerUp", null);
      __publicField(this, "_onPointerCancel", null);
      // Player display
      __publicField(this, "player", null);
      __publicField(this, "playerX");
      __publicField(this, "playerY");
      __publicField(this, "playerGlow", null);
      __publicField(this, "playerArrow", null);
      // Camera
      __publicField(this, "camX");
      __publicField(this, "camY");
      // Asset aliases
      __publicField(this, "_bgAlias", "");
      __publicField(this, "_playerAlias", "");
      __publicField(this, "enemySpriteAliases", []);
      __publicField(this, "bossSpriteAliases", []);
      // UI refs
      __publicField(this, "timerText");
      __publicField(this, "timerBar");
      __publicField(this, "timerBarWidth", 0);
      __publicField(this, "timerBarHeight", 0);
      __publicField(this, "timerBarX", 0);
      __publicField(this, "timerBarY", 0);
      __publicField(this, "hpBar");
      __publicField(this, "hpBarWidth", 0);
      __publicField(this, "hpBarHeight", 0);
      __publicField(this, "hpBarX", 0);
      __publicField(this, "hpBarY", 0);
      __publicField(this, "hpText", null);
      __publicField(this, "killText");
      __publicField(this, "orbText", null);
      __publicField(this, "statsText", null);
      __publicField(this, "minimapContainer", null);
      __publicField(this, "_mmPlayerDot", null);
      __publicField(this, "_mmEnemyDots", null);
      __publicField(this, "_mmVpRect", null);
      __publicField(this, "mmX", 0);
      __publicField(this, "mmY", 0);
      this.containerId = containerId;
      this.data = levelData;
      this.level = levelData.level ?? 1;
      this.biomeName = levelData.biomeName ?? "Forest";
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
      this.mapWidth = levelData.mapWidth ?? 2e3;
      this.mapHeight = levelData.mapHeight ?? 2e3;
      this.vpWidth = levelData.viewportWidth ?? 800;
      this.vpHeight = levelData.viewportHeight ?? 600;
      this.backgroundPath = levelData.backgroundPath ?? "";
      this.enemySprites = levelData.enemySprites ?? [];
      this.playerSpritePath = levelData.playerSpritePath ?? "";
      this.bossSprites = levelData.bossSprites ?? [];
      this.isFinalLevel = levelData.isFinalLevel ?? false;
      this.spawnRampPerMinute = levelData.spawnRampPerMinute ?? 0.2;
      this.speedRampPerMinute = levelData.speedRampPerMinute ?? 0.1;
      this.timeRemaining = this.timerDuration;
      this.currentEnemySpeed = this.enemySpeed;
      this.maxPowerUps = Math.floor(this.timerDuration / 60) + (this.level - 1) * 2;
      this.magnetRadius = XP_PICKUP_RADIUS;
      this.playerX = this.mapWidth / 2;
      this.playerY = this.mapHeight / 2;
      this.camX = clamp(this.playerX - this.vpWidth / 2, 0, this.mapWidth - this.vpWidth);
      this.camY = clamp(this.playerY - this.vpHeight / 2, 0, this.mapHeight - this.vpHeight);
    }
    // ─── Initialisation ─────────────────────────────────────────
    async init() {
      const container = document.getElementById(this.containerId);
      if (!container) throw new Error(`Container #${this.containerId} not found`);
      container.innerHTML = "";
      const containerW = container.clientWidth || this.vpWidth;
      const containerH = container.clientHeight || this.vpHeight;
      this.vpWidth = containerW;
      this.vpHeight = containerH;
      if (!app) {
        app = new PIXI__namespace.Application();
        await app.init({
          width: containerW,
          height: containerH,
          backgroundColor: 1710638,
          antialias: true,
          resolution: window.devicePixelRatio || 1,
          autoDensity: true,
          resizeTo: container
        });
      } else {
        app.renderer.resize(containerW, containerH);
      }
      container.appendChild(app.canvas);
      this.worldContainer = new PIXI__namespace.Container();
      app.stage.addChild(this.worldContainer);
      this.uiContainer = new PIXI__namespace.Container();
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
    async loadAssets() {
      const assets = [];
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
          await PIXI__namespace.Assets.load(assets);
        } catch (e) {
          console.warn("Some survive mode assets failed to load:", e);
        }
      }
    }
    // ─── Background ─────────────────────────────────────────────
    createBackground() {
      const biomeColors = {
        Forest: 2972199,
        Swamp: 3820074,
        Mountains: 7039851,
        Snowy: 13689072,
        Tropical: 3836506,
        Caverns: 2763322,
        Desert: 12886874,
        Volcanic: 4856346,
        Ruins: 4868666,
        Dark: 1710634,
        Light: 16115360,
        Void: 657946
      };
      const bgColor = biomeColors[this.biomeName] ?? 2972199;
      const pad = Math.max(this.vpWidth, this.vpHeight);
      const ground = new PIXI__namespace.Graphics();
      ground.rect(-pad, -pad, this.mapWidth + pad * 2, this.mapHeight + pad * 2);
      ground.fill(bgColor);
      this.worldContainer.addChild(ground);
      const grid = new PIXI__namespace.Graphics();
      grid.setStrokeStyle({ width: 1, color: 16777215, alpha: 0.05 });
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
      const border = new PIXI__namespace.Graphics();
      border.setStrokeStyle({ width: 4, color: 16729156, alpha: 0.6 });
      border.rect(0, 0, this.mapWidth, this.mapHeight);
      border.stroke();
      this.worldContainer.addChild(border);
      const decoColors = {
        Forest: 1722903,
        Swamp: 2767386,
        Mountains: 9079434,
        Snowy: 16777215,
        Tropical: 2779706,
        Caverns: 3816026,
        Desert: 13939562,
        Volcanic: 6957594,
        Ruins: 5921354,
        Dark: 2763338,
        Light: 13944944,
        Void: 1710650
      };
      const decoColor = decoColors[this.biomeName] ?? 1722903;
      const decorations = new PIXI__namespace.Graphics();
      for (let i = 0; i < 200; i++) {
        const x = Math.random() * this.mapWidth;
        const y = Math.random() * this.mapHeight;
        const r = 2 + Math.random() * 6;
        decorations.circle(x, y, r);
      }
      decorations.fill({ color: decoColor, alpha: 0.3 });
      this.worldContainer.addChild(decorations);
      try {
        const bgAlias = this._bgAlias || `surviveBg_${this.level}`;
        const bgTexture = PIXI__namespace.Assets.get(bgAlias);
        if (bgTexture) {
          const tileW = this.vpWidth;
          const tileH = this.vpHeight;
          for (let tx = 0; tx < this.mapWidth; tx += tileW) {
            for (let ty = 0; ty < this.mapHeight; ty += tileH) {
              const bgSprite = new PIXI__namespace.Sprite(bgTexture);
              bgSprite.width = tileW;
              bgSprite.height = tileH;
              bgSprite.alpha = 0.15;
              bgSprite.position.set(tx, ty);
              this.worldContainer.addChild(bgSprite);
            }
          }
        }
      } catch {
      }
    }
    // ─── Player ─────────────────────────────────────────────────
    createPlayer() {
      const playerContainer = new PIXI__namespace.Container();
      let playerSprite = null;
      try {
        const tex = PIXI__namespace.Assets.get(this._playerAlias || "survivePlayer");
        if (tex) {
          playerSprite = new PIXI__namespace.Sprite(tex);
          playerSprite.anchor.set(0.5, 0.5);
          const maxSize = PLAYER_RADIUS * 4;
          const s = Math.min(maxSize / playerSprite.width, maxSize / playerSprite.height);
          playerSprite.scale.set(s);
          playerContainer.addChild(playerSprite);
        }
      } catch {
      }
      if (!playerSprite) {
        const gfx = new PIXI__namespace.Graphics();
        gfx.circle(0, 0, PLAYER_RADIUS);
        gfx.fill(5227511);
        gfx.setStrokeStyle({ width: 2, color: 16777215 });
        gfx.stroke();
        playerContainer.addChild(gfx);
        const arrow = new PIXI__namespace.Graphics();
        arrow.moveTo(PLAYER_RADIUS, 0);
        arrow.lineTo(PLAYER_RADIUS - 6, -5);
        arrow.lineTo(PLAYER_RADIUS - 6, 5);
        arrow.closePath();
        arrow.fill(16777215);
        playerContainer.addChild(arrow);
        this.playerArrow = arrow;
      }
      const glow = new PIXI__namespace.Graphics();
      glow.circle(0, 0, PLAYER_RADIUS + 6);
      glow.fill({ color: 5227511, alpha: 0.15 });
      playerContainer.addChildAt(glow, 0);
      this.playerGlow = glow;
      playerContainer.position.set(this.playerX, this.playerY);
      this.worldContainer.addChild(playerContainer);
      this.player = playerContainer;
    }
    // ─── UI ─────────────────────────────────────────────────────
    createUI() {
      const timerBarW = 240;
      const timerBarH = 24;
      const timerBarX = this.vpWidth / 2 - timerBarW / 2;
      const timerBarY = 10;
      const timerBarBg = new PIXI__namespace.Graphics();
      timerBarBg.roundRect(timerBarX, timerBarY, timerBarW, timerBarH, 6);
      timerBarBg.fill({ color: 0, alpha: 0.7 });
      this.uiContainer.addChild(timerBarBg);
      this.timerBar = new PIXI__namespace.Graphics();
      this.timerBarWidth = timerBarW;
      this.timerBarHeight = timerBarH;
      this.timerBarX = timerBarX;
      this.timerBarY = timerBarY;
      this.updateTimerBar();
      this.uiContainer.addChild(this.timerBar);
      this.timerText = new PIXI__namespace.Text({
        text: formatTime(this.timerDuration),
        style: {
          fontFamily: "Arial",
          fontSize: 14,
          fontWeight: "bold",
          fill: 16777215,
          align: "center"
        }
      });
      this.timerText.anchor.set(0.5, 0.5);
      this.timerText.position.set(this.vpWidth / 2, timerBarY + timerBarH / 2);
      this.uiContainer.addChild(this.timerText);
      const hpBarW = 240;
      const hpBarH = 14;
      const hpBarX = this.vpWidth / 2 - hpBarW / 2;
      const hpBarY = timerBarY + timerBarH + 4;
      const hpBarBg = new PIXI__namespace.Graphics();
      hpBarBg.roundRect(hpBarX, hpBarY, hpBarW, hpBarH, 5);
      hpBarBg.fill({ color: 1703936, alpha: 0.8 });
      this.uiContainer.addChild(hpBarBg);
      this.hpBar = new PIXI__namespace.Graphics();
      this.hpBarWidth = hpBarW;
      this.hpBarHeight = hpBarH;
      this.hpBarX = hpBarX;
      this.hpBarY = hpBarY;
      this.updateHPBar();
      this.uiContainer.addChild(this.hpBar);
      this.hpText = new PIXI__namespace.Text({
        text: `${this.playerHP}/${this.maxHP}`,
        style: {
          fontFamily: "Arial",
          fontSize: 10,
          fontWeight: "bold",
          fill: 16777215,
          align: "center"
        }
      });
      this.hpText.anchor.set(0.5, 0.5);
      this.hpText.position.set(this.vpWidth / 2, hpBarY + hpBarH / 2);
      this.uiContainer.addChild(this.hpText);
      const levelBg = new PIXI__namespace.Graphics();
      levelBg.roundRect(8, 10, 160, 24, 5);
      levelBg.fill({ color: 0, alpha: 0.6 });
      this.uiContainer.addChild(levelBg);
      const levelText = new PIXI__namespace.Text({
        text: `Lv.${this.level} — ${this.biomeName}`,
        style: {
          fontFamily: "Arial",
          fontSize: 13,
          fontWeight: "bold",
          fill: 16763904
        }
      });
      levelText.position.set(14, 13);
      this.uiContainer.addChild(levelText);
      const statsBg = new PIXI__namespace.Graphics();
      statsBg.roundRect(8, 38, 160, 52, 5);
      statsBg.fill({ color: 0, alpha: 0.5 });
      this.uiContainer.addChild(statsBg);
      this.statsText = new PIXI__namespace.Text({
        text: this._buildStatsString(),
        style: { fontFamily: "Arial", fontSize: 10, fill: 13421772, lineHeight: 13 }
      });
      this.statsText.position.set(14, 41);
      this.uiContainer.addChild(this.statsText);
      const killBg = new PIXI__namespace.Graphics();
      killBg.roundRect(this.vpWidth - 120, 10, 112, 24, 5);
      killBg.fill({ color: 0, alpha: 0.6 });
      this.uiContainer.addChild(killBg);
      this.killText = new PIXI__namespace.Text({
        text: "☠ 0",
        style: { fontFamily: "Arial", fontSize: 13, fontWeight: "bold", fill: 16737894 }
      });
      this.killText.position.set(this.vpWidth - 114, 13);
      this.uiContainer.addChild(this.killText);
      const orbBg = new PIXI__namespace.Graphics();
      orbBg.roundRect(this.vpWidth - 120, 38, 112, 24, 5);
      orbBg.fill({ color: 0, alpha: 0.6 });
      this.uiContainer.addChild(orbBg);
      this.orbText = new PIXI__namespace.Text({
        text: "🪙 0",
        style: { fontFamily: "Arial", fontSize: 13, fontWeight: "bold", fill: 16766720 }
      });
      this.orbText.position.set(this.vpWidth - 114, 41);
      this.uiContainer.addChild(this.orbText);
      this.createMinimap();
      this.createJoystick();
    }
    createMinimap() {
      const x = this.vpWidth - MINIMAP_SIZE - MINIMAP_MARGIN;
      const y = this.vpHeight - MINIMAP_SIZE - MINIMAP_MARGIN;
      const mmBg = new PIXI__namespace.Graphics();
      mmBg.roundRect(x, y, MINIMAP_SIZE, MINIMAP_SIZE, 4);
      mmBg.fill({ color: 0, alpha: 0.5 });
      mmBg.setStrokeStyle({ width: 1, color: 16777215, alpha: 0.3 });
      mmBg.stroke();
      this.uiContainer.addChild(mmBg);
      this._mmPlayerDot = new PIXI__namespace.Graphics();
      this._mmEnemyDots = new PIXI__namespace.Graphics();
      this._mmVpRect = new PIXI__namespace.Graphics();
      this.minimapContainer = new PIXI__namespace.Container();
      this.minimapContainer.position.set(x, y);
      this.minimapContainer.addChild(this._mmEnemyDots);
      this.minimapContainer.addChild(this._mmPlayerDot);
      this.minimapContainer.addChild(this._mmVpRect);
      this.uiContainer.addChild(this.minimapContainer);
      this.mmX = x;
      this.mmY = y;
    }
    createJoystick() {
      const joyRadius = 64;
      const knobRadius = 26;
      const defaultX = 100;
      const defaultY = this.vpHeight - 100;
      const joyBg = new PIXI__namespace.Graphics();
      joyBg.circle(0, 0, joyRadius);
      joyBg.fill({ color: 16777215, alpha: 0.15 });
      joyBg.setStrokeStyle({ width: 2, color: 16777215, alpha: 0.3 });
      joyBg.stroke();
      joyBg.position.set(defaultX, defaultY);
      joyBg.alpha = 0;
      this.uiContainer.addChild(joyBg);
      const joyKnob = new PIXI__namespace.Graphics();
      joyKnob.circle(0, 0, knobRadius);
      joyKnob.fill({ color: 16777215, alpha: 0.5 });
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
        defaultY
      };
      this._joystickPointerId = null;
    }
    updateJoystick(localX, localY) {
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
        this.joystick.y + Math.sin(this.joystickAngle) * clamped
      );
    }
    resetJoystick() {
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
    setupInput() {
      this._isTouchDevice = "ontouchstart" in window || navigator.maxTouchPoints > 0;
      this._onKeyDown = (e) => {
        this.keys[e.key.toLowerCase()] = true;
        e.preventDefault();
      };
      this._onKeyUp = (e) => {
        this.keys[e.key.toLowerCase()] = false;
      };
      window.addEventListener("keydown", this._onKeyDown);
      window.addEventListener("keyup", this._onKeyUp);
      if (app == null ? void 0 : app.canvas) {
        app.canvas.style.touchAction = "none";
        this._onPointerDown = (e) => {
          const rect = app.canvas.getBoundingClientRect();
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
        this._onPointerMove = (e) => {
          const rect = app.canvas.getBoundingClientRect();
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
        this._onPointerUp = (e) => {
          if (this._isTouchDevice) {
            if (e.pointerId === this._joystickPointerId) this.resetJoystick();
          } else {
            this.touchActive = false;
          }
        };
        this._onPointerCancel = (e) => {
          if (e.pointerId === this._joystickPointerId) this.resetJoystick();
        };
        app.canvas.addEventListener("pointerdown", this._onPointerDown);
        app.canvas.addEventListener("pointermove", this._onPointerMove);
        app.canvas.addEventListener("pointerup", this._onPointerUp);
        app.canvas.addEventListener("pointercancel", this._onPointerCancel);
      }
    }
    // ─── Enemy Spawning ─────────────────────────────────────────
    spawnInitialEnemies() {
      for (let i = 0; i < this.baseEnemyCount; i++) this.spawnEnemy();
    }
    _getEnemyContainer() {
      if (this._enemyContainerPool.length > 0) {
        const c = this._enemyContainerPool.pop();
        c.visible = true;
        c.alpha = 1;
        return c;
      }
      return new PIXI__namespace.Container();
    }
    _releaseEnemyContainer(container) {
      if (!container) return;
      if (container.parent) container.parent.removeChild(container);
      while (container.children.length > 0) {
        const child = container.children[0];
        container.removeChild(child);
        if (child.destroy) {
          try {
            child.destroy({ children: false, texture: false });
          } catch {
          }
        }
      }
      container.visible = false;
      if (this._enemyContainerPool.length < 50) {
        this._enemyContainerPool.push(container);
      } else {
        try {
          container.destroy({ children: true });
        } catch {
        }
      }
    }
    spawnEnemy() {
      if (this.enemies.length >= this.maxEnemyCount) return;
      const side = Math.floor(Math.random() * 4);
      let ex = 0;
      let ey = 0;
      const margin = 80;
      switch (side) {
        case 0:
          ex = Math.random() * this.mapWidth;
          ey = Math.max(0, this.camY - margin);
          break;
        case 1:
          ex = Math.min(this.mapWidth, this.camX + this.vpWidth + margin);
          ey = Math.random() * this.mapHeight;
          break;
        case 2:
          ex = Math.random() * this.mapWidth;
          ey = Math.min(this.mapHeight, this.camY + this.vpHeight + margin);
          break;
        case 3:
          ex = Math.max(0, this.camX - margin);
          ey = Math.random() * this.mapHeight;
          break;
      }
      const isElite = this.hasElites && Math.random() < this.eliteChance;
      const scale = isElite ? this.enemyScale * ELITE_SCALE : this.enemyScale;
      const speed = isElite ? this.currentEnemySpeed * ELITE_SPEED_MULT : this.currentEnemySpeed * (0.8 + Math.random() * 0.4);
      const enemyContainer = this._getEnemyContainer();
      let sprite = null;
      if (this.enemySpriteAliases.length > 0) {
        const alias = this.enemySpriteAliases[Math.floor(Math.random() * this.enemySpriteAliases.length)];
        try {
          const tex = PIXI__namespace.Assets.get(alias);
          if (tex) {
            sprite = new PIXI__namespace.Sprite(tex);
            sprite.anchor.set(0.5, 0.5);
            const maxSize = ENEMY_HIT_RADIUS * 4 * scale;
            const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
            sprite.scale.set(s);
            enemyContainer.addChild(sprite);
          }
        } catch {
        }
      }
      if (!sprite) {
        const gfx = new PIXI__namespace.Graphics();
        const radius = ENEMY_HIT_RADIUS * scale;
        gfx.circle(0, 0, radius);
        gfx.fill(isElite ? 16729156 : 15022389);
        if (isElite) {
          gfx.setStrokeStyle({ width: 2, color: 16776960 });
          gfx.stroke();
        }
        enemyContainer.addChild(gfx);
      }
      if (isElite) {
        const glow = new PIXI__namespace.Graphics();
        glow.circle(0, 0, ENEMY_HIT_RADIUS * scale + 4);
        glow.fill({ color: 16729156, alpha: 0.2 });
        enemyContainer.addChildAt(glow, 0);
      }
      enemyContainer.position.set(ex, ey);
      this.worldContainer.addChild(enemyContainer);
      const computeHP = () => {
        const baseHP = BASE_ENEMY_HP + this.level * ENEMY_HP_PER_LEVEL;
        const minuteBonus = 1 + Math.floor(this.timeElapsed / 60) * 0.15;
        const hp2 = baseHP * minuteBonus;
        return Math.ceil(isElite ? hp2 * ELITE_HP_MULT : hp2);
      };
      const hp = computeHP();
      this.enemies.push({
        container: enemyContainer,
        x: ex,
        y: ey,
        speed,
        isElite,
        scale,
        hitRadius: ENEMY_HIT_RADIUS * scale,
        wobblePhase: Math.random() * Math.PI * 2,
        alive: true,
        hp,
        maxHp: hp
      });
      this.totalSpawned++;
    }
    // ─── Boss Spawning ──────────────────────────────────────────
    spawnBoss(isFinal) {
      for (const enemy of this.enemies) {
        if (enemy.container) this._releaseEnemyContainer(enemy.container);
      }
      this.enemies = [];
      const bossContainer = this._getEnemyContainer();
      let sprite = null;
      if (this.bossSpriteAliases.length > 0) {
        const alias = this.bossSpriteAliases[Math.floor(Math.random() * this.bossSpriteAliases.length)];
        try {
          const tex = PIXI__namespace.Assets.get(alias);
          if (tex) {
            sprite = new PIXI__namespace.Sprite(tex);
            sprite.anchor.set(0.5, 0.5);
            const maxSize = BOSS_HIT_RADIUS * 4;
            const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
            sprite.scale.set(s);
            bossContainer.addChild(sprite);
          }
        } catch {
        }
      }
      if (!sprite) {
        const gfx = new PIXI__namespace.Graphics();
        gfx.circle(0, 0, BOSS_HIT_RADIUS);
        gfx.fill(isFinal ? 11141120 : 13378082);
        gfx.setStrokeStyle({ width: 3, color: 16763904 });
        gfx.stroke();
        bossContainer.addChild(gfx);
      }
      const glow = new PIXI__namespace.Graphics();
      glow.circle(0, 0, BOSS_HIT_RADIUS + 10);
      glow.fill({ color: isFinal ? 16711680 : 16737792, alpha: 0.25 });
      bossContainer.addChildAt(glow, 0);
      if (isFinal) {
        const crown = new PIXI__namespace.Text({ text: "👑", style: { fontSize: 20 } });
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
      const minuteBonus = 1 + Math.floor(this.timeElapsed / 60) * 0.2;
      const bossHP = Math.ceil(
        (BASE_ENEMY_HP + this.level * ENEMY_HP_PER_LEVEL) * BOSS_HP_MULT * minuteBonus * (isFinal ? 2 : 1)
      );
      const boss = {
        container: bossContainer,
        x: bx,
        y: by,
        speed: this.currentEnemySpeed * BOSS_SPEED_MULT,
        isElite: false,
        isBoss: true,
        isFinalBoss: isFinal,
        scale: 2.5,
        // BOSS_SCALE
        hitRadius: BOSS_HIT_RADIUS,
        wobblePhase: Math.random() * Math.PI * 2,
        alive: true,
        hp: bossHP,
        maxHp: bossHP
      };
      this.enemies.push(boss);
      this.activeBoss = boss;
      this.totalSpawned++;
      this.showBossHPBar(isFinal);
      this.flashScreen(isFinal ? 16711680 : 16737792);
    }
    // ─── Boss HP Bar ────────────────────────────────────────────
    showBossHPBar(isFinal) {
      this.hideBossHPBar();
      const barWidth = 300;
      const barHeight = 16;
      const x = (this.vpWidth - barWidth) / 2;
      const y = 70;
      this.bossHPBarBg = new PIXI__namespace.Graphics();
      this.bossHPBarBg.roundRect(x - 2, y - 2, barWidth + 4, barHeight + 4, 4);
      this.bossHPBarBg.fill({ color: 0, alpha: 0.7 });
      this.uiContainer.addChild(this.bossHPBarBg);
      this.bossHPBarFill = new PIXI__namespace.Graphics();
      this.bossHPBarFill.roundRect(x, y, barWidth, barHeight, 3);
      this.bossHPBarFill.fill(isFinal ? 16720418 : 16737792);
      this.uiContainer.addChild(this.bossHPBarFill);
      this.bossHPBarText = new PIXI__namespace.Text({
        text: isFinal ? "💀 FINAL BOSS" : "⚔️ BOSS",
        style: {
          fontFamily: "Arial",
          fontSize: 12,
          fontWeight: "bold",
          fill: 16777215,
          stroke: { color: 0, width: 2 }
        }
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
    updateBossHPBar() {
      if (!this.activeBoss || !this.bossHPBarFill) return;
      const ratio = Math.max(0, this.activeBoss.hp / this.activeBoss.maxHp);
      const barWidth = this._bossBarW * ratio;
      this.bossHPBarFill.clear();
      if (barWidth > 0) {
        this.bossHPBarFill.roundRect(this._bossBarX, this._bossBarY, barWidth, this._bossBarH, 3);
        this.bossHPBarFill.fill(this._bossBarFinal ? 16720418 : 16737792);
      }
    }
    hideBossHPBar() {
      if (this.bossHPBarBg) {
        try {
          this.uiContainer.removeChild(this.bossHPBarBg);
        } catch {
        }
        this.bossHPBarBg = null;
      }
      if (this.bossHPBarFill) {
        try {
          this.uiContainer.removeChild(this.bossHPBarFill);
        } catch {
        }
        this.bossHPBarFill = null;
      }
      if (this.bossHPBarText) {
        try {
          this.uiContainer.removeChild(this.bossHPBarText);
        } catch {
        }
        this.bossHPBarText = null;
      }
    }
    flashScreen(color) {
      const flash = new PIXI__namespace.Graphics();
      flash.rect(0, 0, this.vpWidth, this.vpHeight);
      flash.fill({ color, alpha: 0.35 });
      this.uiContainer.addChild(flash);
      setTimeout(() => {
        try {
          this.uiContainer.removeChild(flash);
        } catch {
        }
      }, 300);
    }
    _buildStatsString() {
      const atkSpd = (1 / this.attackCooldownMult).toFixed(1);
      return `⚔ DMG ${this.attackDamage}  ⚡ SPD x${atkSpd}
🎯 RNG ${Math.round(this.attackRange)}  🏃 MOV ${Math.round(this.playerSpeed)}
🪙 DROP x${this.coinDropMult}  ❤ HP ${this.maxHP}`;
    }
    // ─── Object Pools ───────────────────────────────────────────
    _initPools() {
      const gfxFactory = () => new PIXI__namespace.Graphics();
      const gfxReset = (gfx) => {
        gfx.clear();
        gfx.alpha = 1;
        gfx.visible = false;
      };
      this._projectilePool = new ObjectPool(gfxFactory, gfxReset, 30);
      this._orbPool = new ObjectPool(gfxFactory, gfxReset, 40);
      this._particlePool = new ObjectPool(gfxFactory, gfxReset, 50);
    }
    _getProjectileGfx(color, radius) {
      const gfx = this._projectilePool.get();
      gfx.clear();
      gfx.circle(0, 0, radius);
      gfx.fill(color);
      gfx.circle(0, 0, radius + 2);
      gfx.fill({ color, alpha: 0.3 });
      gfx.visible = true;
      gfx.alpha = 1;
      return gfx;
    }
    _releaseProjectileGfx(gfx) {
      gfx.visible = false;
      if (gfx.parent) gfx.parent.removeChild(gfx);
      this._projectilePool.release(gfx);
    }
    _getOrbGfx() {
      const gfx = this._orbPool.get();
      gfx.clear();
      gfx.circle(0, 0, XP_ORB_RADIUS + 1);
      gfx.fill(16766720);
      gfx.circle(0, 0, XP_ORB_RADIUS - 1);
      gfx.fill(16757504);
      gfx.circle(0, 0, 2);
      gfx.fill(16766720);
      gfx.visible = true;
      gfx.alpha = 1;
      return gfx;
    }
    _releaseOrbGfx(gfx) {
      gfx.visible = false;
      if (gfx.parent) gfx.parent.removeChild(gfx);
      this._orbPool.release(gfx);
    }
    _getParticleGfx(color) {
      const gfx = this._particlePool.get();
      gfx.clear();
      gfx.circle(0, 0, 2 + Math.random() * 3);
      gfx.fill(color ?? 16729156);
      gfx.visible = true;
      gfx.alpha = 1;
      return gfx;
    }
    _releaseParticleGfx(gfx) {
      gfx.visible = false;
      if (gfx.parent) gfx.parent.removeChild(gfx);
      this._particlePool.release(gfx);
    }
    spawnXPOrb(x, y) {
      const gfx = this._getOrbGfx();
      gfx.position.set(x, y);
      this.worldContainer.addChild(gfx);
      this.xpOrbs.push({ gfx, x, y, lifetime: 8 });
    }
    spawnDeathParticles(x, y, color) {
      for (let i = 0; i < 6; i++) {
        const angle = Math.PI * 2 / 6 * i + Math.random() * 0.5;
        const speed = 40 + Math.random() * 60;
        const gfx = this._getParticleGfx(color);
        gfx.position.set(x, y);
        this.worldContainer.addChild(gfx);
        this.particles.push({
          gfx,
          vx: Math.cos(angle) * speed,
          vy: Math.sin(angle) * speed,
          lifetime: 0.5 + Math.random() * 0.3,
          age: 0
        });
      }
    }
    // ─── Main Update Loop ───────────────────────────────────────
    update(ticker) {
      var _a;
      if (!this.alive || this.won || gamePaused || this.upgradePaused) return;
      const dt = ticker.deltaMS / 1e3;
      this.timeElapsed += dt;
      if (!this.timerStopped) this.timeRemaining -= dt;
      this.spawnTimer += dt;
      const currentMinute = Math.floor(this.timeElapsed / 60);
      if (currentMinute > this.lastMinuteRamp) {
        const newMinutes = currentMinute - this.lastMinuteRamp;
        this.spawnRampBonus += newMinutes * this.spawnRampPerMinute;
        this.speedRampBonus += newMinutes * this.speedRampPerMinute;
        this.lastMinuteRamp = currentMinute;
      }
      this.speedRampTimer += dt;
      const rampFactor = 1 + this.speedRampTimer / this.timerDuration * 0.8 + this.speedRampBonus;
      this.currentEnemySpeed = Math.min(this.enemySpeed * rampFactor, this.maxEnemySpeed);
      const hasBosses = this.bossSpriteAliases.length > 0 && !this.isFinalLevel;
      if (hasBosses && !this.midBossSpawned && this.timeRemaining <= this.timerDuration / 2) {
        this.midBossSpawned = true;
        this.spawnBoss(false);
      }
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
      if (this.activeBoss) {
        this.updateBossHPBar();
        if ((_a = this.activeBoss.container) == null ? void 0 : _a.children[0]) {
          this.activeBoss.container.children[0].alpha = 0.15 + Math.sin(this.timeElapsed * 4) * 0.1;
        }
      }
      if (this.invulnTimer > 0) {
        this.invulnTimer -= dt;
        if (this.player) this.player.alpha = Math.floor(this.timeElapsed / 0.08) % 2 === 0 ? 0.4 : 1;
      } else if (this.player) {
        this.player.alpha = 1;
      }
      this.timerText.text = formatTime(this.timeRemaining);
      this.updateTimerBar();
      this.updateHPBar();
      if (this.hpText) this.hpText.text = `${Math.ceil(this.playerHP)}/${this.maxHP}`;
      this.killText.text = `☠ ${this.enemiesKilled}`;
      if (this.orbText) this.orbText.text = `🪙 ${this.xpOrbsCollected}`;
      if (this.statsText) this.statsText.text = this._buildStatsString();
      this.updatePlayer(dt);
      this.updateCamera();
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
      if (this.playerGlow) {
        this.playerGlow.alpha = 0.1 + Math.sin(this.timeElapsed * 3) * 0.08;
      }
    }
    // ─── Movement ───────────────────────────────────────────────
    updatePlayer(dt) {
      let dx = 0;
      let dy = 0;
      if (this.keys["w"] || this.keys["arrowup"]) dy -= 1;
      if (this.keys["s"] || this.keys["arrowdown"]) dy += 1;
      if (this.keys["a"] || this.keys["arrowleft"]) dx -= 1;
      if (this.keys["d"] || this.keys["arrowright"]) dx += 1;
      if (this.joystickActive && this.joystickMagnitude > 0.1) {
        dx = Math.cos(this.joystickAngle) * this.joystickMagnitude;
        dy = Math.sin(this.joystickAngle) * this.joystickMagnitude;
      }
      if (this.touchActive) {
        const tdx = this.touchTarget.x - this.playerX;
        const tdy = this.touchTarget.y - this.playerY;
        const td = Math.sqrt(tdx * tdx + tdy * tdy);
        if (td > 5) {
          dx = tdx / td;
          dy = tdy / td;
        }
      }
      const mag = Math.sqrt(dx * dx + dy * dy);
      if (mag > 0) {
        dx /= mag;
        dy /= mag;
      }
      this.playerX += dx * this.playerSpeed * dt;
      this.playerY += dy * this.playerSpeed * dt;
      this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
      this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
      this.player.position.set(this.playerX, this.playerY);
      if (this.playerArrow && mag > 0) {
        this.playerArrow.rotation = Math.atan2(dy, dx);
      }
    }
    updateCamera() {
      const targetCamX = this.playerX - this.vpWidth / 2;
      const targetCamY = this.playerY - this.vpHeight / 2;
      this.camX = lerp(this.camX, targetCamX, 0.1);
      this.camY = lerp(this.camY, targetCamY, 0.1);
      this.camX = clamp(this.camX, 0, this.mapWidth - this.vpWidth);
      this.camY = clamp(this.camY, 0, this.mapHeight - this.vpHeight);
      this.worldContainer.position.set(-this.camX, -this.camY);
    }
    // ─── Enemy Update ───────────────────────────────────────────
    updateEnemies(dt) {
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
        enemy.container.visible = enemy.x >= camLeft && enemy.x <= camRight && enemy.y >= camTop && enemy.y <= camBottom;
      }
    }
    // ─── XP Orbs ────────────────────────────────────────────────
    updateXPOrbs(dt) {
      for (let i = this.xpOrbs.length - 1; i >= 0; i--) {
        const orb = this.xpOrbs[i];
        orb.lifetime -= dt;
        const dx = this.playerX - orb.x;
        const dy = this.playerY - orb.y;
        const d = Math.sqrt(dx * dx + dy * dy);
        if (d < this.magnetRadius) {
          const speed = XP_ORB_SPEED * (1 - d / this.magnetRadius);
          orb.x += dx / d * speed * dt;
          orb.y += dy / d * speed * dt;
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
    updateParticles(dt) {
      for (let i = this.particles.length - 1; i >= 0; i--) {
        const p = this.particles[i];
        p.age += dt;
        p.gfx.position.x += p.vx * dt;
        p.gfx.position.y += p.vy * dt;
        p.gfx.alpha = 1 - p.age / p.lifetime;
        if (p.age >= p.lifetime) {
          this._releaseParticleGfx(p.gfx);
          this.particles.splice(i, 1);
        }
      }
    }
    // ─── Auto-Attack ────────────────────────────────────────────
    updateAutoAttack(dt) {
      this.attackCooldown -= dt;
      if (this.attackCooldown > 0) return;
      let nearest = null;
      let nearestDist = this.attackRange;
      for (const enemy of this.enemies) {
        if (!enemy.alive) continue;
        const d2 = dist({ x: this.playerX, y: this.playerY }, { x: enemy.x, y: enemy.y });
        if (d2 < nearestDist) {
          nearestDist = d2;
          nearest = enemy;
        }
      }
      if (!nearest) return;
      this.attackCooldown = ATTACK_COOLDOWN * this.attackCooldownMult;
      const dx = nearest.x - this.playerX;
      const dy = nearest.y - this.playerY;
      const d = Math.sqrt(dx * dx + dy * dy);
      const gfx = this._getProjectileGfx(5227511, PROJECTILE_RADIUS);
      gfx.position.set(this.playerX, this.playerY);
      this.worldContainer.addChild(gfx);
      this.projectiles.push({
        gfx,
        x: this.playerX,
        y: this.playerY,
        vx: dx / d * PROJECTILE_SPEED,
        vy: dy / d * PROJECTILE_SPEED,
        damage: this.attackDamage,
        lifetime: PROJECTILE_LIFETIME,
        age: 0
      });
      if (this.playerArrow) this.playerArrow.rotation = Math.atan2(dy, dx);
    }
    // ─── Projectiles ────────────────────────────────────────────
    updateProjectiles(dt) {
      var _a;
      for (let i = this.projectiles.length - 1; i >= 0; i--) {
        const proj = this.projectiles[i];
        proj.age += dt;
        proj.x += proj.vx * dt;
        proj.y += proj.vy * dt;
        proj.gfx.position.set(proj.x, proj.y);
        if (proj.age >= proj.lifetime || proj.x < -50 || proj.x > this.mapWidth + 50 || proj.y < -50 || proj.y > this.mapHeight + 50) {
          this._releaseProjectileGfx(proj.gfx);
          this.projectiles.splice(i, 1);
          continue;
        }
        for (let j = this.enemies.length - 1; j >= 0; j--) {
          const enemy = this.enemies[j];
          if (!enemy.alive) continue;
          if ((_a = proj.hitEnemies) == null ? void 0 : _a.has(enemy)) continue;
          const d = dist({ x: proj.x, y: proj.y }, { x: enemy.x, y: enemy.y });
          if (d < PROJECTILE_RADIUS + enemy.hitRadius) {
            enemy.hp -= proj.damage;
            enemy.container.alpha = 0.5;
            setTimeout(() => {
              if (enemy.container) enemy.container.alpha = 1;
            }, 80);
            if (enemy.hp <= 0) {
              enemy.alive = false;
              this.spawnDeathParticles(enemy.x, enemy.y, enemy.isBoss ? 16737792 : enemy.isElite ? 16776960 : 16729156);
              const coinCount = enemy.isBoss ? this.coinDropMult * 5 : this.coinDropMult;
              for (let c = 0; c < coinCount; c++) {
                const ox = c === 0 ? 0 : (Math.random() - 0.5) * 30;
                const oy = c === 0 ? 0 : (Math.random() - 0.5) * 30;
                this.spawnXPOrb(enemy.x + ox, enemy.y + oy);
              }
              this._releaseEnemyContainer(enemy.container);
              this.enemies.splice(j, 1);
              this.enemiesKilled++;
              this.playSFX("hit");
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
            if (!proj.hitEnemies) proj.hitEnemies = /* @__PURE__ */ new Set();
            proj.hitEnemies.add(enemy);
          }
        }
      }
    }
    // ─── Spatial Grid ───────────────────────────────────────────
    rebuildSpatialGrid() {
      this._spatialGrid.clear();
      for (const e of this.enemies) {
        if (e.alive) this._spatialGrid.insert(e);
      }
    }
    checkCollisions() {
      if (this.invulnTimer > 0) return;
      const nearby = this._spatialGrid.query(this.playerX, this.playerY);
      for (let i = 0; i < nearby.length; i++) {
        const enemy = nearby[i];
        if (!enemy.alive) continue;
        const d = dist({ x: this.playerX, y: this.playerY }, { x: enemy.x, y: enemy.y });
        if (d < PLAYER_RADIUS + enemy.hitRadius) {
          const dmg = enemy.isBoss ? BOSS_DAMAGE : enemy.isElite ? ELITE_DAMAGE : ENEMY_DAMAGE;
          this.playerHP -= dmg;
          this.invulnTimer = INVULN_DURATION;
          this.playSFX("hit");
          const kbDist = 40;
          const angle = Math.atan2(this.playerY - enemy.y, this.playerX - enemy.x);
          this.playerX += Math.cos(angle) * kbDist;
          this.playerY += Math.sin(angle) * kbDist;
          this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
          this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
          this.player.position.set(this.playerX, this.playerY);
          if (this.playerHP <= 0) {
            this.playerHP = 0;
            this.alive = false;
            this.onDeath();
            return;
          }
          return;
        }
      }
    }
    // ─── UI Bar Updates ─────────────────────────────────────────
    updateHPBar() {
      if (!this.hpBar) return;
      this.hpBar.clear();
      const progress = clamp(this.playerHP / this.maxHP, 0, 1);
      const barW = this.hpBarWidth * progress;
      let color;
      if (progress > 0.6) color = 15022389;
      else if (progress > 0.3) color = 12986408;
      else color = 12000284;
      this.hpBar.roundRect(this.hpBarX, this.hpBarY, barW, this.hpBarHeight, 5);
      this.hpBar.fill(color);
    }
    updateTimerBar() {
      if (!this.timerBar) return;
      this.timerBar.clear();
      const progress = clamp(this.timeRemaining / this.timerDuration, 0, 1);
      const barW = this.timerBarWidth * progress;
      let color;
      if (progress > 0.5) color = 4431943;
      else if (progress > 0.25) color = 16757504;
      else color = 15022389;
      this.timerBar.roundRect(this.timerBarX, this.timerBarY, barW, this.timerBarHeight, 6);
      this.timerBar.fill(color);
    }
    updateMinimap() {
      if (!this.minimapContainer || !this._mmPlayerDot || !this._mmEnemyDots || !this._mmVpRect) return;
      const scaleX = MINIMAP_SIZE / this.mapWidth;
      const scaleY = MINIMAP_SIZE / this.mapHeight;
      this._mmPlayerDot.clear();
      this._mmPlayerDot.circle(this.playerX * scaleX, this.playerY * scaleY, 3);
      this._mmPlayerDot.fill(5227511);
      this._mmEnemyDots.clear();
      for (const enemy of this.enemies) {
        if (!enemy.alive) continue;
        this._mmEnemyDots.circle(enemy.x * scaleX, enemy.y * scaleY, enemy.isElite ? 2 : 1);
      }
      this._mmEnemyDots.fill(15022389);
      this._mmVpRect.clear();
      this._mmVpRect.rect(this.camX * scaleX, this.camY * scaleY, this.vpWidth * scaleX, this.vpHeight * scaleY);
      this._mmVpRect.setStrokeStyle({ width: 1, color: 16777215, alpha: 0.5 });
      this._mmVpRect.stroke();
    }
    // ─── Companion PIG System ───────────────────────────────────
    spawnCompanion() {
      const companionIdx = this.companions.length;
      const container = new PIXI__namespace.Container();
      const body = new PIXI__namespace.Graphics();
      body.circle(0, 0, 12);
      body.fill(16758465);
      body.setStrokeStyle({ width: 1.5, color: 16738740 });
      body.stroke();
      container.addChild(body);
      const snout = new PIXI__namespace.Graphics();
      snout.ellipse(10, 0, 5, 4);
      snout.fill(16751001);
      snout.circle(12, -1.5, 1);
      snout.fill(13395558);
      snout.circle(12, 1.5, 1);
      snout.fill(13395558);
      container.addChild(snout);
      const ear = new PIXI__namespace.Graphics();
      ear.moveTo(-5, -10);
      ear.lineTo(0, -16);
      ear.lineTo(5, -10);
      ear.closePath();
      ear.fill(16747937);
      container.addChild(ear);
      const eyes = new PIXI__namespace.Graphics();
      eyes.circle(3, -4, 2);
      eyes.fill(2236962);
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
        orbitRadius: 50 + companionIdx * 20
      });
    }
    updateCompanions(dt) {
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
        comp.attackCooldown -= dt;
        if (comp.attackCooldown <= 0) {
          let nearest = null;
          let nearestDist = comp.attackRange;
          for (const enemy of this.enemies) {
            if (!enemy.alive) continue;
            const d = dist({ x: comp.x, y: comp.y }, { x: enemy.x, y: enemy.y });
            if (d < nearestDist) {
              nearestDist = d;
              nearest = enemy;
            }
          }
          if (nearest) {
            comp.attackCooldown = ATTACK_COOLDOWN * this.attackCooldownMult;
            const pdx = nearest.x - comp.x;
            const pdy = nearest.y - comp.y;
            const pd = Math.sqrt(pdx * pdx + pdy * pdy);
            const gfx = this._getProjectileGfx(16738740, 3);
            gfx.position.set(comp.x, comp.y);
            this.worldContainer.addChild(gfx);
            this.projectiles.push({
              gfx,
              x: comp.x,
              y: comp.y,
              vx: pdx / pd * PROJECTILE_SPEED,
              vy: pdy / pd * PROJECTILE_SPEED,
              damage: this.attackDamage,
              lifetime: PROJECTILE_LIFETIME,
              age: 0
            });
          }
        }
      }
    }
    // ─── Upgrade Popup ──────────────────────────────────────────
    showUpgradePopup() {
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
      if (choices.length === 0) {
        this.upgradePaused = false;
        return;
      }
      const overlay = new PIXI__namespace.Container();
      const bg = new PIXI__namespace.Graphics();
      bg.rect(0, 0, this.vpWidth, this.vpHeight);
      bg.fill({ color: 0, alpha: 0.75 });
      bg.eventMode = "static";
      overlay.addChild(bg);
      const title = new PIXI__namespace.Text({
        text: "ESCOLHE UM UPGRADE",
        style: {
          fontFamily: "Arial",
          fontSize: 22,
          fontWeight: "bold",
          fill: 16766720,
          align: "center",
          dropShadow: { color: 0, blur: 4, distance: 2 }
        }
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
        const card = new PIXI__namespace.Container();
        card.eventMode = "static";
        card.cursor = "pointer";
        const cardBg = new PIXI__namespace.Graphics();
        cardBg.roundRect(0, 0, cardW, cardH, 10);
        cardBg.fill({ color: 1710654, alpha: 0.95 });
        cardBg.setStrokeStyle({ width: 2, color: 16766720, alpha: 0.8 });
        cardBg.stroke();
        card.addChild(cardBg);
        const hoverBg = new PIXI__namespace.Graphics();
        hoverBg.roundRect(0, 0, cardW, cardH, 10);
        hoverBg.fill({ color: 2763358, alpha: 0.95 });
        hoverBg.setStrokeStyle({ width: 3, color: 16772693 });
        hoverBg.stroke();
        hoverBg.visible = false;
        card.addChild(hoverBg);
        const icon = new PIXI__namespace.Text({ text: upg.icon, style: { fontSize: 36 } });
        icon.anchor.set(0.5, 0.5);
        icon.position.set(cardW / 2, 35);
        card.addChild(icon);
        const tText = new PIXI__namespace.Text({
          text: upg.title,
          style: {
            fontFamily: "Arial",
            fontSize: 14,
            fontWeight: "bold",
            fill: 16777215,
            align: "center",
            wordWrap: true,
            wordWrapWidth: cardW - 16
          }
        });
        tText.anchor.set(0.5, 0);
        tText.position.set(cardW / 2, 62);
        card.addChild(tText);
        const dText = new PIXI__namespace.Text({
          text: upg.desc,
          style: {
            fontFamily: "Arial",
            fontSize: 11,
            fill: 12303291,
            align: "center",
            wordWrap: true,
            wordWrapWidth: cardW - 16
          }
        });
        dText.anchor.set(0.5, 0);
        dText.position.set(cardW / 2, 90);
        card.addChild(dText);
        card.on("pointerdown", () => {
          this.applyUpgrade(upg);
          this.uiContainer.removeChild(overlay);
          this.upgradeOverlay = null;
          this.upgradePaused = false;
        });
        card.on("pointerover", () => {
          hoverBg.visible = true;
          cardBg.visible = false;
        });
        card.on("pointerout", () => {
          hoverBg.visible = false;
          cardBg.visible = true;
        });
        card.position.set(cx, cardY);
        overlay.addChild(card);
      });
      this.upgradeOverlay = overlay;
      this.uiContainer.addChild(overlay);
    }
    applyUpgrade(upg) {
      upg.apply(this);
      this.upgradesPicked++;
      this.upgradePickCounts[upg.id] = (this.upgradePickCounts[upg.id] ?? 0) + 1;
      if (upg.unique) this.pickedUpgradeIds.add(upg.id);
      const flash = new PIXI__namespace.Graphics();
      flash.rect(0, 0, this.vpWidth, this.vpHeight);
      flash.fill({ color: 16766720, alpha: 0.2 });
      this.uiContainer.addChild(flash);
      setTimeout(() => {
        try {
          this.uiContainer.removeChild(flash);
        } catch {
        }
      }, 200);
    }
    // ─── End-Game ───────────────────────────────────────────────
    onWin() {
      gameActive = false;
      const title = this.finalBossSpawned ? "BOSS DEFEATED!" : "SURVIVED!";
      this.showMessage(title, 4431943, `Level ${this.level} Complete!`);
      this.playSFX("win");
      setTimeout(() => {
        if (dotNetRef) {
          try {
            dotNetRef.invokeMethodAsync("OnLevelComplete", this.enemiesKilled, this.timeElapsed, this.xpOrbsCollected);
          } catch (e) {
            console.error("Failed to invoke OnLevelComplete:", e);
          }
        }
      }, 2e3);
    }
    onDeath() {
      gameActive = false;
      this.spawnDeathParticles(this.playerX, this.playerY, 5227511);
      if (this.player) this.player.alpha = 0.3;
      this.showMessage("SURVIVAL ENDED", 15022389, `Survived ${formatTime(this.timeElapsed)}`);
      this.playSFX("death");
      setTimeout(() => {
        if (dotNetRef) {
          try {
            dotNetRef.invokeMethodAsync("OnPlayerDeath", this.enemiesKilled, this.timeElapsed, this.xpOrbsCollected);
          } catch (e) {
            console.error("Failed to invoke OnPlayerDeath:", e);
          }
        }
      }, 2e3);
    }
    showMessage(titleStr, color, subtitle) {
      const overlay = new PIXI__namespace.Graphics();
      overlay.rect(0, 0, this.vpWidth, this.vpHeight);
      overlay.fill({ color: 0, alpha: 0.6 });
      this.uiContainer.addChild(overlay);
      const text = new PIXI__namespace.Text({
        text: titleStr,
        style: {
          fontFamily: "Arial",
          fontSize: 48,
          fontWeight: "bold",
          fill: color,
          stroke: { color: 0, width: 4 },
          align: "center"
        }
      });
      text.anchor.set(0.5, 0.5);
      text.position.set(this.vpWidth / 2, this.vpHeight / 2 - 20);
      this.uiContainer.addChild(text);
      if (subtitle) {
        const sub = new PIXI__namespace.Text({
          text: subtitle,
          style: { fontFamily: "Arial", fontSize: 18, fill: 16777215, align: "center" }
        });
        sub.anchor.set(0.5, 0.5);
        sub.position.set(this.vpWidth / 2, this.vpHeight / 2 + 30);
        this.uiContainer.addChild(sub);
      }
    }
    // ─── Audio ──────────────────────────────────────────────────
    playMusic() {
      if (!audioEnabled) return;
      try {
        if (bgMusic) {
          bgMusic.currentTime = 0;
          bgMusic.play().catch(() => {
          });
          return;
        }
        bgMusic = new Audio("/sound/survival_battle.mp3" + audioCacheBuster);
        bgMusic.loop = true;
        bgMusic.volume = 0.3;
        bgMusic.play().catch(() => {
        });
        bgMusicLoaded = true;
      } catch {
      }
    }
    playSFX(type) {
      if (!audioEnabled) return;
      try {
        const audio = getPooledAudio(type);
        if (audio) audio.play().catch(() => {
        });
      } catch {
      }
    }
    // ─── Cleanup / Destroy ──────────────────────────────────────
    cleanup() {
      window.removeEventListener("keydown", this._onKeyDown);
      window.removeEventListener("keyup", this._onKeyUp);
      if (app == null ? void 0 : app.canvas) {
        const c = app.canvas;
        if (this._onPointerDown) c.removeEventListener("pointerdown", this._onPointerDown);
        if (this._onPointerMove) c.removeEventListener("pointermove", this._onPointerMove);
        if (this._onPointerUp) c.removeEventListener("pointerup", this._onPointerUp);
        if (this._onPointerCancel) c.removeEventListener("pointercancel", this._onPointerCancel);
      }
      if (app == null ? void 0 : app.ticker) {
        try {
          app.ticker.remove(this.update, this);
        } catch {
        }
      }
      for (const proj of this.projectiles) {
        if (proj.gfx) {
          proj.gfx.visible = false;
          if (proj.gfx.parent) proj.gfx.parent.removeChild(proj.gfx);
        }
      }
      for (const orb of this.xpOrbs) {
        if (orb.gfx) {
          orb.gfx.visible = false;
          if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx);
        }
      }
      for (const p of this.particles) {
        if (p.gfx) {
          p.gfx.visible = false;
          if (p.gfx.parent) p.gfx.parent.removeChild(p.gfx);
        }
      }
      if (this.worldContainer) {
        try {
          this.worldContainer.removeChildren();
        } catch {
        }
      }
      if (this.uiContainer) {
        try {
          this.uiContainer.removeChildren();
        } catch {
        }
      }
      this.enemies = [];
      for (const c of this._enemyContainerPool) {
        try {
          c.destroy({ children: true });
        } catch {
        }
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
    destroy() {
      this.cleanup();
      if (bgMusic) {
        try {
          bgMusic.pause();
          bgMusic.currentTime = 0;
        } catch {
        }
        bgMusic = null;
        bgMusicLoaded = false;
      }
      if (app) {
        try {
          app.stage.removeChildren();
          app.destroy(true, { children: true, texture: false });
        } catch {
        }
        app = null;
      }
      gameActive = false;
    }
  }
  let scene = null;
  let gameActive = false;
  let gamePaused = false;
  let dotNetRef = null;
  function createSurviveApi() {
    return {
      async start(containerId, levelData, netRef) {
        dotNetRef = netRef;
        if (scene) scene.destroy();
        scene = new SurviveScene(containerId, levelData);
        await scene.init();
      },
      async nextLevel(levelData) {
        if (scene) scene.cleanup();
        scene = new SurviveScene((scene == null ? void 0 : scene.containerId) ?? "surviveGameContainer", levelData);
        await scene.init();
      },
      pause() {
        gamePaused = true;
      },
      resume() {
        gamePaused = false;
      },
      setAudioEnabled(enabled) {
        audioEnabled = enabled;
        if (bgMusic) {
          if (enabled) bgMusic.play().catch(() => {
          });
          else bgMusic.pause();
        }
      },
      destroy() {
        if (scene) {
          scene.destroy();
          scene = null;
        }
        dotNetRef = null;
      },
      isActive() {
        return gameActive;
      }
    };
  }
  window.surviveModeGame = createSurviveApi();
})(PIXI);
//# sourceMappingURL=pixiSurviveMode.js.map
