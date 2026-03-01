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
  var AttackPattern = /* @__PURE__ */ ((AttackPattern2) => {
    AttackPattern2["Projectile"] = "projectile";
    AttackPattern2["Orbital"] = "orbital";
    AttackPattern2["AoE"] = "aoe";
    AttackPattern2["Boomerang"] = "boomerang";
    AttackPattern2["Lightning"] = "lightning";
    AttackPattern2["ForceField"] = "forcefield";
    AttackPattern2["Companion"] = "companion";
    return AttackPattern2;
  })(AttackPattern || {});
  var EnemyBehaviour = /* @__PURE__ */ ((EnemyBehaviour2) => {
    EnemyBehaviour2["Chaser"] = "chaser";
    EnemyBehaviour2["Flanker"] = "flanker";
    EnemyBehaviour2["Tank"] = "tank";
    EnemyBehaviour2["Sprinter"] = "sprinter";
    return EnemyBehaviour2;
  })(EnemyBehaviour || {});
  var UpgradeChoiceType = /* @__PURE__ */ ((UpgradeChoiceType2) => {
    UpgradeChoiceType2["NewWeapon"] = "new_weapon";
    UpgradeChoiceType2["WeaponLevelUp"] = "weapon_levelup";
    UpgradeChoiceType2["Passive"] = "passive";
    UpgradeChoiceType2["Evolution"] = "evolution";
    return UpgradeChoiceType2;
  })(UpgradeChoiceType || {});
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
    query(x, y, radius = 1) {
      const cx = Math.floor(x / this.cellSize);
      const cy = Math.floor(y / this.cellSize);
      const result = [];
      for (let dx = -radius; dx <= radius; dx++) {
        for (let dy = -radius; dy <= radius; dy++) {
          const cell = this.cells.get(this._key(cx + dx, cy + dy));
          if (cell) {
            for (let i = 0; i < cell.length; i++) result.push(cell[i]);
          }
        }
      }
      return result;
    }
  }
  function levels(base, scale) {
    const result = [{ ...base }];
    for (let i = 1; i < 5; i++) {
      const prev = result[i - 1];
      const next = { ...prev };
      for (const [key, vals] of Object.entries(scale)) {
        if (vals && vals[i - 1] !== void 0) {
          next[key] = prev[key] + vals[i - 1];
        }
      }
      result.push(next);
    }
    return result;
  }
  const WEAPON_SHARPSHOT = {
    id: "sharpshot",
    name: "Tiro Certeiro",
    icon: "🎯",
    description: "Dispara projéteis no inimigo mais próximo.",
    pattern: AttackPattern.Projectile,
    color: 5227511,
    projectileRadius: 4,
    levels: levels(
      { damage: 1, cooldown: 0.45, range: 180, count: 1, pierce: 0, aoeRadius: 0, speedMult: 1 },
      { damage: [1, 1, 2, 3], count: [1, 0, 1, 0], pierce: [0, 1, 0, 1], range: [0, 20, 0, 20] }
    )
  };
  const WEAPON_SPINBLADE = {
    id: "spinblade",
    name: "Lâmina Giratória",
    icon: "🔪",
    description: "Lâminas orbitam à volta do jogador.",
    pattern: AttackPattern.Orbital,
    color: 11583173,
    levels: levels(
      { damage: 2, cooldown: 0, range: 60, count: 2, pierce: -1, aoeRadius: 0, speedMult: 1 },
      { damage: [1, 1, 2, 2], count: [1, 0, 1, 1], range: [10, 10, 15, 15] }
    )
  };
  const WEAPON_SHOCKWAVE = {
    id: "shockwave",
    name: "Onda de Choque",
    icon: "💥",
    description: "Explosão periódica à volta do jogador.",
    pattern: AttackPattern.AoE,
    color: 16747109,
    levels: levels(
      { damage: 3, cooldown: 3, range: 0, count: 1, pierce: 0, aoeRadius: 100, speedMult: 1 },
      { damage: [2, 2, 3, 4], cooldown: [-0.3, -0.3, -0.3, -0.2], aoeRadius: [15, 20, 20, 25] }
    )
  };
  const WEAPON_BOOMERANG = {
    id: "boomerang",
    name: "Bumerangue",
    icon: "🪃",
    description: "Projétil que volta ao jogador.",
    pattern: AttackPattern.Boomerang,
    color: 9268835,
    projectileRadius: 6,
    levels: levels(
      { damage: 2, cooldown: 1.2, range: 250, count: 1, pierce: 3, aoeRadius: 0, speedMult: 1 },
      { damage: [1, 2, 2, 3], count: [1, 0, 1, 0], range: [30, 0, 30, 40], pierce: [1, 1, 2, 2] }
    )
  };
  const WEAPON_LIGHTNING = {
    id: "lightning",
    name: "Raio",
    icon: "⚡",
    description: "Relâmpago em cadeia que salta entre inimigos.",
    pattern: AttackPattern.Lightning,
    color: 16772696,
    levels: levels(
      { damage: 4, cooldown: 1.8, range: 200, count: 1, pierce: 2, aoeRadius: 0, speedMult: 1 },
      { damage: [2, 3, 3, 4], count: [0, 1, 0, 1], pierce: [1, 1, 1, 2], range: [20, 0, 30, 0] }
    )
  };
  const WEAPON_FORCEFIELD = {
    id: "forcefield",
    name: "Barreira",
    icon: "🛡️",
    description: "Aura de dano à volta do jogador.",
    pattern: AttackPattern.ForceField,
    color: 8440772,
    levels: levels(
      { damage: 1, cooldown: 0.5, range: 0, count: 1, pierce: 0, aoeRadius: 70, speedMult: 1 },
      { damage: [1, 1, 1, 2], aoeRadius: [10, 15, 15, 20], cooldown: [-0.05, -0.05, -0.05, -0.05] }
    )
  };
  const WEAPON_COMPANION = {
    id: "companion",
    name: "Leitão Aliado",
    icon: "🐷",
    description: "Um leitão que orbita e ataca inimigos.",
    pattern: AttackPattern.Companion,
    color: 16738740,
    projectileRadius: 3,
    levels: levels(
      { damage: 1, cooldown: 0.5, range: 150, count: 1, pierce: 0, aoeRadius: 0, speedMult: 1 },
      { damage: [1, 1, 2, 2], cooldown: [-0.05, -0.05, -0.05, -0.05], range: [15, 15, 20, 20], count: [0, 0, 0, 1] }
    )
  };
  const WEAPON_FIRERAIN = {
    id: "firerain",
    name: "Chuva de Fogo",
    icon: "🔥",
    description: "Bolas de fogo caem em posições aleatórias.",
    pattern: AttackPattern.Projectile,
    color: 16733986,
    projectileRadius: 8,
    levels: levels(
      { damage: 3, cooldown: 2, range: 300, count: 1, pierce: 0, aoeRadius: 50, speedMult: 0.6 },
      { damage: [2, 2, 3, 4], count: [1, 0, 1, 1], aoeRadius: [10, 10, 15, 15], cooldown: [-0.15, -0.15, -0.1, -0.1] }
    )
  };
  const WEAPON_EVO_THUNDER_SHOT = {
    id: "evo_thundershot",
    name: "Trovão Certeiro",
    icon: "🌩️",
    description: "Projéteis elétricos que encadeiam entre inimigos.",
    pattern: AttackPattern.Projectile,
    color: 4244735,
    projectileRadius: 5,
    levels: [{
      damage: 14,
      cooldown: 0.35,
      range: 240,
      count: 4,
      pierce: 3,
      aoeRadius: 0,
      speedMult: 1.3,
      extra: { chain: 3 }
    }]
  };
  const WEAPON_EVO_GUARDIAN_BLADE = {
    id: "evo_guardianblade",
    name: "Lâmina Protetora",
    icon: "⚔️",
    description: "Lâminas orbitais com aura de dano contínuo.",
    pattern: AttackPattern.Orbital,
    color: 6942894,
    levels: [{
      damage: 12,
      cooldown: 0,
      range: 100,
      count: 6,
      pierce: -1,
      aoeRadius: 120,
      speedMult: 1
    }]
  };
  const WEAPON_EVO_APOCALYPSE = {
    id: "evo_apocalypse",
    name: "Apocalipse",
    icon: "☄️",
    description: "Meteoros caem do céu com ondas de choque.",
    pattern: AttackPattern.AoE,
    color: 16739904,
    levels: [{
      damage: 22,
      cooldown: 1.6,
      range: 350,
      count: 3,
      pierce: 0,
      aoeRadius: 160,
      speedMult: 1
    }]
  };
  const WEAPON_EVO_PIG_RANG = {
    id: "evo_pigrang",
    name: "Leitão Bumerangue",
    icon: "🐗",
    description: "Leitões arremessam bumerangues devastadores.",
    pattern: AttackPattern.Companion,
    color: 16744619,
    projectileRadius: 7,
    levels: [{
      damage: 12,
      cooldown: 0.35,
      range: 280,
      count: 3,
      pierce: 5,
      aoeRadius: 0,
      speedMult: 1.2
    }]
  };
  const EVOLUTION_RECIPES = [
    {
      id: "evo_thundershot",
      ingredientA: "sharpshot",
      ingredientB: "lightning",
      result: WEAPON_EVO_THUNDER_SHOT,
      description: "🎯 Tiro Certeiro + ⚡ Raio → 🌩️ Trovão Certeiro"
    },
    {
      id: "evo_guardianblade",
      ingredientA: "spinblade",
      ingredientB: "forcefield",
      result: WEAPON_EVO_GUARDIAN_BLADE,
      description: "🔪 Lâmina + 🛡️ Barreira → ⚔️ Lâmina Protetora"
    },
    {
      id: "evo_apocalypse",
      ingredientA: "shockwave",
      ingredientB: "firerain",
      result: WEAPON_EVO_APOCALYPSE,
      description: "💥 Onda de Choque + 🔥 Chuva de Fogo → ☄️ Apocalipse"
    },
    {
      id: "evo_pigrang",
      ingredientA: "boomerang",
      ingredientB: "companion",
      result: WEAPON_EVO_PIG_RANG,
      description: "🪃 Bumerangue + 🐷 Leitão → 🐗 Leitão Bumerangue"
    }
  ];
  const ALL_WEAPONS = [
    WEAPON_SHARPSHOT,
    WEAPON_SPINBLADE,
    WEAPON_SHOCKWAVE,
    WEAPON_BOOMERANG,
    WEAPON_LIGHTNING,
    WEAPON_FORCEFIELD,
    WEAPON_COMPANION,
    WEAPON_FIRERAIN
  ];
  const ALL_EVOLVED_WEAPONS = [
    WEAPON_EVO_THUNDER_SHOT,
    WEAPON_EVO_GUARDIAN_BLADE,
    WEAPON_EVO_APOCALYPSE,
    WEAPON_EVO_PIG_RANG
  ];
  new Map(
    [...ALL_WEAPONS, ...ALL_EVOLVED_WEAPONS].map((w) => [w.id, w])
  );
  const MAX_WEAPON_SLOTS = 6;
  const MAX_WEAPON_LEVEL = 5;
  const SFX_POOL_SIZE = 4;
  const SFX_MAP = {
    win: "/audio/games/my-tuno/survive/win.mp3",
    death: "/audio/games/my-tuno/survive/death.mp3",
    hit: "/audio/games/my-tuno/survive/hit.mp3",
    levelup: "/audio/games/my-tuno/survive/levelup.mp3",
    lightning: "/audio/games/my-tuno/survive/lightning.mp3",
    explosion: "/audio/games/my-tuno/survive/explosion.mp3"
  };
  class AudioManager {
    constructor() {
      __publicField(this, "sfxPools", {});
      __publicField(this, "bgMusic", null);
      __publicField(this, "bgMusicLoaded", false);
      __publicField(this, "_enabled", true);
    }
    get enabled() {
      return this._enabled;
    }
    setEnabled(enabled) {
      this._enabled = enabled;
      if (this.bgMusic) {
        if (enabled) this.bgMusic.play().catch(() => {
        });
        else this.bgMusic.pause();
      }
    }
    playMusic() {
      if (!this._enabled) return;
      try {
        if (this.bgMusic) {
          this.bgMusic.currentTime = 0;
          this.bgMusic.play().catch(() => {
          });
          return;
        }
        this.bgMusic = new Audio("/sound/survival_battle.mp3" + audioCacheBuster);
        this.bgMusic.loop = true;
        this.bgMusic.volume = 0.3;
        this.bgMusic.play().catch(() => {
        });
        this.bgMusicLoaded = true;
      } catch {
      }
    }
    playSFX(type) {
      if (!this._enabled) return;
      try {
        const audio = this._getPooledAudio(type);
        if (audio) audio.play().catch(() => {
        });
      } catch {
      }
    }
    stopMusic() {
      if (this.bgMusic) {
        try {
          this.bgMusic.pause();
          this.bgMusic.currentTime = 0;
        } catch {
        }
      }
    }
    destroy() {
      this.stopMusic();
      this.bgMusic = null;
      this.bgMusicLoaded = false;
      this.sfxPools = {};
    }
    _getPooledAudio(type) {
      const src = SFX_MAP[type];
      if (!src) return null;
      if (!this.sfxPools[type]) {
        this.sfxPools[type] = [];
        for (let i = 0; i < SFX_POOL_SIZE; i++) {
          const a2 = new Audio(src + audioCacheBuster);
          a2.volume = 0.5;
          this.sfxPools[type].push(a2);
        }
      }
      for (const a2 of this.sfxPools[type]) {
        if (a2.paused || a2.ended) {
          a2.currentTime = 0;
          return a2;
        }
      }
      const a = this.sfxPools[type][0];
      a.currentTime = 0;
      return a;
    }
  }
  class InputManager {
    constructor(vpWidth, vpHeight) {
      // Keyboard
      __publicField(this, "keys", {});
      // Mouse
      __publicField(this, "touchActive", false);
      __publicField(this, "touchTarget", { x: 0, y: 0 });
      // Smoothed output (touch devices)
      __publicField(this, "_smoothDx", 0);
      __publicField(this, "_smoothDy", 0);
      // Joystick
      __publicField(this, "joystick", null);
      __publicField(this, "joystickActive", false);
      __publicField(this, "joystickAngle", 0);
      __publicField(this, "joystickMagnitude", 0);
      __publicField(this, "_joystickPointerId", null);
      __publicField(this, "_isTouchDevice", false);
      // Viewport info (updated by scene)
      __publicField(this, "vpWidth");
      __publicField(this, "vpHeight");
      __publicField(this, "camX", 0);
      __publicField(this, "camY", 0);
      // Event handler refs
      __publicField(this, "_onKeyDown", null);
      __publicField(this, "_onKeyUp", null);
      __publicField(this, "_onPointerDown", null);
      __publicField(this, "_onPointerMove", null);
      __publicField(this, "_onPointerUp", null);
      __publicField(this, "_onPointerCancel", null);
      __publicField(this, "canvas", null);
      this.vpWidth = vpWidth;
      this.vpHeight = vpHeight;
    }
    /** Update camera position (call each frame before getMovement). */
    setCamera(camX, camY) {
      this.camX = camX;
      this.camY = camY;
    }
    setViewport(w, h) {
      this.vpWidth = w;
      this.vpHeight = h;
    }
    setup(app2, uiContainer) {
      this._isTouchDevice = "ontouchstart" in window || navigator.maxTouchPoints > 0;
      this.canvas = app2.canvas;
      this._onKeyDown = (e) => {
        this.keys[e.key.toLowerCase()] = true;
        e.preventDefault();
      };
      this._onKeyUp = (e) => {
        this.keys[e.key.toLowerCase()] = false;
      };
      window.addEventListener("keydown", this._onKeyDown);
      window.addEventListener("keyup", this._onKeyUp);
      if (this.canvas) {
        this.canvas.style.touchAction = "none";
        this._onPointerDown = (e) => {
          const rect = this.canvas.getBoundingClientRect();
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
          const rect = this.canvas.getBoundingClientRect();
          const scaleX = this.vpWidth / rect.width;
          const scaleY = this.vpHeight / rect.height;
          const localX = (e.clientX - rect.left) * scaleX;
          const localY = (e.clientY - rect.top) * scaleY;
          if (this._isTouchDevice) {
            if (this.joystickActive && e.pointerId === this._joystickPointerId) {
              this._updateJoystick(localX, localY);
            }
          } else {
            if (!this.touchActive) return;
            this.touchTarget.x = localX + this.camX;
            this.touchTarget.y = localY + this.camY;
          }
        };
        this._onPointerUp = (e) => {
          if (this._isTouchDevice) {
            if (e.pointerId === this._joystickPointerId) this._resetJoystick();
          } else {
            this.touchActive = false;
          }
        };
        this._onPointerCancel = (e) => {
          if (e.pointerId === this._joystickPointerId) this._resetJoystick();
        };
        this.canvas.addEventListener("pointerdown", this._onPointerDown);
        this.canvas.addEventListener("pointermove", this._onPointerMove);
        this.canvas.addEventListener("pointerup", this._onPointerUp);
        this.canvas.addEventListener("pointercancel", this._onPointerCancel);
      }
      this._createJoystickVisuals(uiContainer);
    }
    /** Get smoothed movement direction (frame-rate independent). */
    getMovement(dt) {
      let dx = 0;
      let dy = 0;
      if (this.keys["w"] || this.keys["arrowup"]) dy -= 1;
      if (this.keys["s"] || this.keys["arrowdown"]) dy += 1;
      if (this.keys["a"] || this.keys["arrowleft"]) dx -= 1;
      if (this.keys["d"] || this.keys["arrowright"]) dx += 1;
      if (this.joystickActive && this.joystickMagnitude > 0.1) {
        const curved = this.joystickMagnitude * this.joystickMagnitude;
        dx = Math.cos(this.joystickAngle) * curved;
        dy = Math.sin(this.joystickAngle) * curved;
      }
      const mag = Math.sqrt(dx * dx + dy * dy);
      if (mag > 1) {
        dx /= mag;
        dy /= mag;
      }
      if (this._isTouchDevice) {
        const hasInput = dx !== 0 || dy !== 0;
        const speed = hasInput ? 14 : 8;
        const f = 1 - Math.exp(-speed * dt);
        this._smoothDx += (dx - this._smoothDx) * f;
        this._smoothDy += (dy - this._smoothDy) * f;
        if (!hasInput && Math.abs(this._smoothDx) < 0.01 && Math.abs(this._smoothDy) < 0.01) {
          this._smoothDx = 0;
          this._smoothDy = 0;
        }
        return { dx: this._smoothDx, dy: this._smoothDy };
      }
      return { dx, dy };
    }
    cleanup() {
      if (this._onKeyDown) window.removeEventListener("keydown", this._onKeyDown);
      if (this._onKeyUp) window.removeEventListener("keyup", this._onKeyUp);
      if (this.canvas) {
        if (this._onPointerDown) this.canvas.removeEventListener("pointerdown", this._onPointerDown);
        if (this._onPointerMove) this.canvas.removeEventListener("pointermove", this._onPointerMove);
        if (this._onPointerUp) this.canvas.removeEventListener("pointerup", this._onPointerUp);
        if (this._onPointerCancel) this.canvas.removeEventListener("pointercancel", this._onPointerCancel);
      }
      this.keys = {};
      this._smoothDx = 0;
      this._smoothDy = 0;
    }
    // ─── Joystick Internals ─────────────────────────────────────
    _createJoystickVisuals(uiContainer) {
      const joyRadius = 64;
      const knobRadius = 26;
      const defaultX = 100;
      const defaultY = this.vpHeight - 100;
      const bg = new PIXI__namespace.Graphics();
      bg.circle(0, 0, joyRadius);
      bg.fill({ color: 16777215, alpha: 0.15 });
      bg.setStrokeStyle({ width: 2, color: 16777215, alpha: 0.3 });
      bg.stroke();
      bg.position.set(defaultX, defaultY);
      bg.alpha = 0;
      uiContainer.addChild(bg);
      const knob = new PIXI__namespace.Graphics();
      knob.circle(0, 0, knobRadius);
      knob.fill({ color: 16777215, alpha: 0.5 });
      knob.position.set(defaultX, defaultY);
      knob.alpha = 0;
      uiContainer.addChild(knob);
      this.joystick = { bg, knob, x: defaultX, y: defaultY, radius: joyRadius, defaultX, defaultY };
      this._joystickPointerId = null;
    }
    _updateJoystick(localX, localY) {
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
    _resetJoystick() {
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
  }
  class ParticleSystem {
    constructor(worldContainer) {
      __publicField(this, "particles", []);
      __publicField(this, "pool");
      __publicField(this, "worldContainer");
      this.worldContainer = worldContainer;
      this.pool = new ObjectPool(
        () => new PIXI__namespace.Graphics(),
        (gfx) => {
          gfx.clear();
          gfx.alpha = 1;
          gfx.visible = false;
        },
        50
      );
    }
    /** Spawn a radial burst of particles (e.g. on enemy death). */
    spawnBurst(x, y, color, count = 6) {
      for (let i = 0; i < count; i++) {
        const angle = Math.PI * 2 / count * i + Math.random() * 0.5;
        const speed = 40 + Math.random() * 60;
        const gfx = this._getGfx(color);
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
    /** Spawn a screen flash overlay (in UI space). */
    flashScreen(uiContainer, vpWidth, vpHeight, color, alpha = 0.35, durationMs = 300) {
      const flash = new PIXI__namespace.Graphics();
      flash.rect(0, 0, vpWidth, vpHeight);
      flash.fill({ color, alpha });
      uiContainer.addChild(flash);
      setTimeout(() => {
        try {
          uiContainer.removeChild(flash);
        } catch {
        }
      }, durationMs);
    }
    /** Spawn a floating damage number in world space. */
    spawnDamageNumber(x, y, damage, color = 16777215) {
      const text = new PIXI__namespace.Text({
        text: `-${damage}`,
        style: {
          fontFamily: "Arial",
          fontSize: 14,
          fontWeight: "bold",
          fill: color,
          stroke: { color: 0, width: 2 }
        }
      });
      text.anchor.set(0.5, 0.5);
      text.position.set(x + (Math.random() - 0.5) * 20, y - 10);
      this.worldContainer.addChild(text);
      const lifetime = 0.8;
      const startY = text.position.y;
      const startTime = performance.now();
      const animate = () => {
        const elapsed = (performance.now() - startTime) / 1e3;
        if (elapsed >= lifetime) {
          try {
            this.worldContainer.removeChild(text);
            text.destroy();
          } catch {
          }
          return;
        }
        const t = elapsed / lifetime;
        text.position.y = startY - t * 30;
        text.alpha = 1 - t;
        requestAnimationFrame(animate);
      };
      requestAnimationFrame(animate);
    }
    update(dt) {
      for (let i = this.particles.length - 1; i >= 0; i--) {
        const p = this.particles[i];
        p.age += dt;
        p.gfx.position.x += p.vx * dt;
        p.gfx.position.y += p.vy * dt;
        p.gfx.alpha = 1 - p.age / p.lifetime;
        if (p.age >= p.lifetime) {
          this._releaseGfx(p.gfx);
          this.particles.splice(i, 1);
        }
      }
    }
    cleanup() {
      for (const p of this.particles) {
        p.gfx.visible = false;
        if (p.gfx.parent) p.gfx.parent.removeChild(p.gfx);
      }
      this.particles = [];
    }
    _getGfx(color) {
      const gfx = this.pool.get();
      gfx.clear();
      gfx.circle(0, 0, 2 + Math.random() * 3);
      gfx.fill(color);
      gfx.visible = true;
      gfx.alpha = 1;
      return gfx;
    }
    _releaseGfx(gfx) {
      gfx.visible = false;
      if (gfx.parent) gfx.parent.removeChild(gfx);
      this.pool.release(gfx);
    }
  }
  class EnemyManager {
    constructor(cfg, worldContainer, particles, audio) {
      // ─── Public state (read by other subsystems) ────────────────
      __publicField(this, "enemies", []);
      __publicField(this, "activeBoss", null);
      __publicField(this, "spatialGrid", new SpatialGrid(128));
      __publicField(this, "enemiesKilled", 0);
      __publicField(this, "totalSpawned", 0);
      __publicField(this, "midBossSpawned", false);
      __publicField(this, "finalBossSpawned", false);
      // ─── Config ─────────────────────────────────────────────────
      __publicField(this, "cfg");
      __publicField(this, "worldContainer");
      // Runtime ramp state 
      __publicField(this, "currentEnemySpeed");
      __publicField(this, "spawnTimer", 0);
      __publicField(this, "speedRampTimer", 0);
      __publicField(this, "lastMinuteRamp", 0);
      __publicField(this, "spawnRampBonus", 0);
      __publicField(this, "speedRampBonus", 0);
      // Pools
      __publicField(this, "_containerPool", []);
      // Callbacks 
      __publicField(this, "particles");
      __publicField(this, "audio");
      /** Callback when an XP orb should be spawned at (x, y). */
      __publicField(this, "onSpawnOrb", null);
      /** Callback when the boss HP bar should update. */
      __publicField(this, "onBossHPChanged", null);
      /** Callback for boss spawn (for UI bar). */
      __publicField(this, "onBossSpawn", null);
      /** Callback for boss death. */
      __publicField(this, "onBossDeath", null);
      this.cfg = cfg;
      this.worldContainer = worldContainer;
      this.particles = particles;
      this.audio = audio;
      this.currentEnemySpeed = cfg.enemySpeed;
    }
    // ─── Spawning ───────────────────────────────────────────────
    spawnInitial() {
      for (let i = 0; i < this.cfg.baseEnemyCount; i++) this.spawnEnemy();
    }
    /** Called each frame. Handles timed wave spawning. */
    updateSpawning(dt, timeElapsed, camX, camY) {
      this.spawnTimer += dt;
      this.speedRampTimer += dt;
      const currentMinute = Math.floor(timeElapsed / 60);
      if (currentMinute > this.lastMinuteRamp) {
        const newMinutes = currentMinute - this.lastMinuteRamp;
        this.spawnRampBonus += newMinutes * this.cfg.spawnRampPerMinute;
        this.speedRampBonus += newMinutes * this.cfg.speedRampPerMinute;
        this.lastMinuteRamp = currentMinute;
      }
      const rampFactor = 1 + this.speedRampTimer / this.cfg.timerDuration * 0.8 + this.speedRampBonus;
      this.currentEnemySpeed = Math.min(this.cfg.enemySpeed * rampFactor, this.cfg.maxEnemySpeed);
      if (this.spawnTimer >= this.cfg.spawnInterval) {
        this.spawnTimer = 0;
        const timeScale = Math.floor(timeElapsed / 5);
        const rampMult = 1 + this.spawnRampBonus;
        const baseSpawn = Math.ceil((3 + timeScale) * rampMult);
        const burstBonus = Math.floor(timeElapsed / 35) * 5;
        const toSpawn = Math.min(baseSpawn + burstBonus, 30);
        for (let i = 0; i < toSpawn; i++) this.spawnEnemy(camX, camY);
      }
    }
    spawnEnemy(camX = 0, camY = 0) {
      if (this.enemies.length >= this.cfg.maxEnemyCount) return;
      const side = Math.floor(Math.random() * 4);
      let ex = 0, ey = 0;
      const margin = 80;
      switch (side) {
        case 0:
          ex = Math.random() * this.cfg.mapWidth;
          ey = Math.max(0, camY - margin);
          break;
        case 1:
          ex = Math.min(this.cfg.mapWidth, camX + this.cfg.vpWidth + margin);
          ey = Math.random() * this.cfg.mapHeight;
          break;
        case 2:
          ex = Math.random() * this.cfg.mapWidth;
          ey = Math.min(this.cfg.mapHeight, camY + this.cfg.vpHeight + margin);
          break;
        case 3:
          ex = Math.max(0, camX - margin);
          ey = Math.random() * this.cfg.mapHeight;
          break;
      }
      const isElite = this.cfg.hasElites && Math.random() < this.cfg.eliteChance;
      const scale = isElite ? this.cfg.enemyScale * ELITE_SCALE : this.cfg.enemyScale;
      const speed = isElite ? this.currentEnemySpeed * ELITE_SPEED_MULT : this.currentEnemySpeed * (0.8 + Math.random() * 0.4);
      const container = this._getContainer();
      let sprite = null;
      if (this.cfg.enemySpriteAliases.length > 0) {
        const alias = this.cfg.enemySpriteAliases[Math.floor(Math.random() * this.cfg.enemySpriteAliases.length)];
        try {
          const tex = PIXI__namespace.Assets.get(alias);
          if (tex) {
            sprite = new PIXI__namespace.Sprite(tex);
            sprite.anchor.set(0.5, 0.5);
            const maxSize = ENEMY_HIT_RADIUS * 4 * scale;
            const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
            sprite.scale.set(s);
            container.addChild(sprite);
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
        container.addChild(gfx);
      }
      if (isElite) {
        const glow = new PIXI__namespace.Graphics();
        glow.circle(0, 0, ENEMY_HIT_RADIUS * scale + 4);
        glow.fill({ color: 16729156, alpha: 0.2 });
        container.addChildAt(glow, 0);
      }
      container.position.set(ex, ey);
      this.worldContainer.addChild(container);
      const baseHP = BASE_ENEMY_HP + this.cfg.level * ENEMY_HP_PER_LEVEL;
      const minuteBonus = 1 + Math.floor(this.speedRampTimer / 60) * 0.15;
      const hp = Math.ceil(isElite ? baseHP * minuteBonus * ELITE_HP_MULT : baseHP * minuteBonus);
      let behaviour = EnemyBehaviour.Chaser;
      if (!isElite) {
        const r = Math.random();
        if (r < 0.1) behaviour = EnemyBehaviour.Flanker;
        else if (r < 0.15) behaviour = EnemyBehaviour.Sprinter;
        else if (r < 0.2 && this.cfg.level >= 3) behaviour = EnemyBehaviour.Tank;
      }
      this.enemies.push({
        container,
        x: ex,
        y: ey,
        speed,
        isElite,
        scale,
        hitRadius: ENEMY_HIT_RADIUS * scale,
        wobblePhase: Math.random() * Math.PI * 2,
        alive: true,
        hp,
        maxHp: hp,
        behaviour,
        aiTimer: 0
      });
      this.totalSpawned++;
    }
    // ─── Boss ───────────────────────────────────────────────────
    spawnBoss(isFinal, playerX, playerY) {
      var _a;
      for (const enemy of this.enemies) {
        if (enemy.container) this._releaseContainer(enemy.container);
      }
      this.enemies = [];
      const container = this._getContainer();
      let sprite = null;
      if (this.cfg.bossSpriteAliases.length > 0) {
        const alias = this.cfg.bossSpriteAliases[Math.floor(Math.random() * this.cfg.bossSpriteAliases.length)];
        try {
          const tex = PIXI__namespace.Assets.get(alias);
          if (tex) {
            sprite = new PIXI__namespace.Sprite(tex);
            sprite.anchor.set(0.5, 0.5);
            const maxSize = BOSS_HIT_RADIUS * 4;
            const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
            sprite.scale.set(s);
            container.addChild(sprite);
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
        container.addChild(gfx);
      }
      const glow = new PIXI__namespace.Graphics();
      glow.circle(0, 0, BOSS_HIT_RADIUS + 10);
      glow.fill({ color: isFinal ? 16711680 : 16737792, alpha: 0.25 });
      container.addChildAt(glow, 0);
      if (isFinal) {
        const crown = new PIXI__namespace.Text({ text: "👑", style: { fontSize: 20 } });
        crown.anchor.set(0.5, 1);
        crown.position.set(0, -BOSS_HIT_RADIUS - 5);
        container.addChild(crown);
      }
      const angle = Math.random() * Math.PI * 2;
      const bx = clamp(playerX + Math.cos(angle) * 350, BOSS_HIT_RADIUS, this.cfg.mapWidth - BOSS_HIT_RADIUS);
      const by = clamp(playerY + Math.sin(angle) * 350, BOSS_HIT_RADIUS, this.cfg.mapHeight - BOSS_HIT_RADIUS);
      container.position.set(bx, by);
      this.worldContainer.addChild(container);
      const minuteBonus = 1 + Math.floor(this.speedRampTimer / 60) * 0.2;
      const bossHP = Math.ceil(
        (BASE_ENEMY_HP + this.cfg.level * ENEMY_HP_PER_LEVEL) * BOSS_HP_MULT * minuteBonus * (isFinal ? 2 : 1)
      );
      const boss = {
        container,
        x: bx,
        y: by,
        speed: this.currentEnemySpeed * BOSS_SPEED_MULT,
        isElite: false,
        isBoss: true,
        isFinalBoss: isFinal,
        scale: 2.5,
        hitRadius: BOSS_HIT_RADIUS,
        wobblePhase: Math.random() * Math.PI * 2,
        alive: true,
        hp: bossHP,
        maxHp: bossHP,
        behaviour: EnemyBehaviour.Chaser,
        aiTimer: 0
      };
      this.enemies.push(boss);
      this.activeBoss = boss;
      this.totalSpawned++;
      (_a = this.onBossSpawn) == null ? void 0 : _a.call(this, isFinal);
    }
    // ─── Movement AI ────────────────────────────────────────────
    updateMovement(dt, playerX, playerY, camX, camY) {
      const cullMargin = 200;
      const camLeft = camX - cullMargin;
      const camRight = camX + this.cfg.vpWidth + cullMargin;
      const camTop = camY - cullMargin;
      const camBottom = camY + this.cfg.vpHeight + cullMargin;
      for (const enemy of this.enemies) {
        if (!enemy.alive) continue;
        const dx = playerX - enemy.x;
        const dy = playerY - enemy.y;
        const d = Math.sqrt(dx * dx + dy * dy);
        if (d > 1) {
          enemy.aiTimer += dt;
          let moveX = dx / d;
          let moveY = dy / d;
          let spd = enemy.speed;
          switch (enemy.behaviour) {
            case EnemyBehaviour.Flanker: {
              const flankAngle = Math.atan2(dy, dx) + Math.PI * 0.3 * Math.sin(enemy.aiTimer * 2);
              moveX = Math.cos(flankAngle);
              moveY = Math.sin(flankAngle);
              spd *= 1.15;
              break;
            }
            case EnemyBehaviour.Tank:
              spd *= 0.6;
              break;
            case EnemyBehaviour.Sprinter: {
              const dashCycle = enemy.aiTimer % 3;
              if (dashCycle < 0.3) spd *= 2.5;
              else if (dashCycle < 1) spd *= 0.5;
              break;
            }
            default: {
              enemy.wobblePhase += dt * 3;
              const wobbleX = Math.sin(enemy.wobblePhase) * 15;
              const wobbleY = Math.cos(enemy.wobblePhase * 0.7) * 15;
              moveX = dx / d + wobbleX / d;
              moveY = dy / d + wobbleY / d;
              break;
            }
          }
          enemy.x += moveX * spd * dt;
          enemy.y += moveY * spd * dt;
          enemy.x = clamp(enemy.x, 0, this.cfg.mapWidth);
          enemy.y = clamp(enemy.y, 0, this.cfg.mapHeight);
          enemy.container.position.set(enemy.x, enemy.y);
          enemy.container.scale.x = dx > 0 ? Math.abs(enemy.container.scale.x) : -Math.abs(enemy.container.scale.x);
        }
        enemy.container.visible = enemy.x >= camLeft && enemy.x <= camRight && enemy.y >= camTop && enemy.y <= camBottom;
      }
    }
    // ─── Spatial Grid ───────────────────────────────────────────
    rebuildGrid() {
      this.spatialGrid.clear();
      for (const e of this.enemies) {
        if (e.alive) this.spatialGrid.insert(e);
      }
    }
    // ─── Player Collision ───────────────────────────────────────
    /** Returns damage dealt to player (0 = no hit). */
    checkPlayerCollision(playerX, playerY, invulnerable) {
      if (invulnerable) return null;
      const nearby = this.spatialGrid.query(playerX, playerY);
      for (const enemy of nearby) {
        if (!enemy.alive) continue;
        const d = dist({ x: playerX, y: playerY }, { x: enemy.x, y: enemy.y });
        if (d < PLAYER_RADIUS + enemy.hitRadius) {
          const dmg = enemy.isBoss ? BOSS_DAMAGE : enemy.isElite ? ELITE_DAMAGE : ENEMY_DAMAGE;
          const angle = Math.atan2(playerY - enemy.y, playerX - enemy.x);
          return { damage: dmg, knockbackAngle: angle };
        }
      }
      return null;
    }
    // ─── Enemy Damage (from weapons) ────────────────────────────
    /** Apply damage to an enemy. Returns true if the enemy died. */
    damageEnemy(enemy, damage, coinDropMult) {
      var _a, _b;
      enemy.hp -= damage;
      enemy.container.alpha = 0.5;
      setTimeout(() => {
        if (enemy.container) enemy.container.alpha = 1;
      }, 80);
      if (enemy.hp <= 0) {
        enemy.alive = false;
        this.particles.spawnBurst(enemy.x, enemy.y, enemy.isBoss ? 16737792 : enemy.isElite ? 16776960 : 16729156);
        const coinCount = enemy.isBoss ? coinDropMult * 5 : coinDropMult;
        for (let c = 0; c < coinCount; c++) {
          const ox = c === 0 ? 0 : (Math.random() - 0.5) * 30;
          const oy = c === 0 ? 0 : (Math.random() - 0.5) * 30;
          (_a = this.onSpawnOrb) == null ? void 0 : _a.call(this, enemy.x + ox, enemy.y + oy);
        }
        this._releaseContainer(enemy.container);
        const idx = this.enemies.indexOf(enemy);
        if (idx >= 0) this.enemies.splice(idx, 1);
        this.enemiesKilled++;
        this.audio.playSFX("hit");
        if (enemy.isBoss) {
          this.activeBoss = null;
          (_b = this.onBossDeath) == null ? void 0 : _b.call(this, !!enemy.isFinalBoss);
        }
        return true;
      }
      return false;
    }
    // ─── Container Pool ─────────────────────────────────────────
    _getContainer() {
      if (this._containerPool.length > 0) {
        const c = this._containerPool.pop();
        c.visible = true;
        c.alpha = 1;
        return c;
      }
      return new PIXI__namespace.Container();
    }
    _releaseContainer(container) {
      if (!container) return;
      if (container.parent) container.parent.removeChild(container);
      while (container.children.length > 0) {
        const child = container.children[0];
        container.removeChild(child);
        try {
          child.destroy({ children: false, texture: false });
        } catch {
        }
      }
      container.visible = false;
      if (this._containerPool.length < 50) {
        this._containerPool.push(container);
      } else {
        try {
          container.destroy({ children: true });
        } catch {
        }
      }
    }
    // ─── Cleanup ────────────────────────────────────────────────
    cleanup() {
      for (const enemy of this.enemies) {
        if (enemy.container) this._releaseContainer(enemy.container);
      }
      this.enemies = [];
      for (const c of this._containerPool) {
        try {
          c.destroy({ children: true });
        } catch {
        }
      }
      this._containerPool = [];
      this.activeBoss = null;
    }
  }
  class ProjectileManager {
    constructor(worldContainer, mapWidth, mapHeight, enemyManager) {
      __publicField(this, "projectiles", []);
      __publicField(this, "worldContainer");
      __publicField(this, "pool");
      __publicField(this, "mapWidth");
      __publicField(this, "mapHeight");
      __publicField(this, "enemyManager");
      this.worldContainer = worldContainer;
      this.mapWidth = mapWidth;
      this.mapHeight = mapHeight;
      this.enemyManager = enemyManager;
      this.pool = new ObjectPool(
        () => new PIXI__namespace.Graphics(),
        (gfx) => {
          gfx.clear();
          gfx.alpha = 1;
          gfx.visible = false;
        },
        30
      );
    }
    /** Fire a straight projectile toward a target direction. */
    fireProjectile(fromX, fromY, toX, toY, damage, color, radius = PROJECTILE_RADIUS, speedMult = 1, pierce = 0, aoeRadius = 0, lifetime = PROJECTILE_LIFETIME, weaponId) {
      const dx = toX - fromX;
      const dy = toY - fromY;
      const d = Math.sqrt(dx * dx + dy * dy);
      if (d < 1) return;
      const gfx = this._getGfx(color, radius);
      gfx.position.set(fromX, fromY);
      this.worldContainer.addChild(gfx);
      this.projectiles.push({
        gfx,
        x: fromX,
        y: fromY,
        vx: dx / d * PROJECTILE_SPEED * speedMult,
        vy: dy / d * PROJECTILE_SPEED * speedMult,
        damage,
        lifetime,
        age: 0,
        weaponId,
        pierceLeft: pierce,
        aoeRadius
      });
    }
    /** Fire a boomerang projectile. */
    fireBoomerang(fromX, fromY, toX, toY, damage, color, radius, maxRange, pierce, speedMult = 1, weaponId) {
      const dx = toX - fromX;
      const dy = toY - fromY;
      const d = Math.sqrt(dx * dx + dy * dy);
      if (d < 1) return;
      const gfx = this._getGfx(color, radius);
      gfx.position.set(fromX, fromY);
      this.worldContainer.addChild(gfx);
      this.projectiles.push({
        gfx,
        x: fromX,
        y: fromY,
        vx: dx / d * PROJECTILE_SPEED * speedMult,
        vy: dy / d * PROJECTILE_SPEED * speedMult,
        damage,
        lifetime: 5,
        age: 0,
        weaponId,
        pierceLeft: pierce,
        returning: false,
        originX: fromX,
        originY: fromY,
        maxRange
      });
    }
    /** Spawn a static AoE zone that damages enemies once. */
    spawnAoE(x, y, damage, radius, color, coinDropMult) {
      const gfx = new PIXI__namespace.Graphics();
      gfx.circle(0, 0, radius);
      gfx.fill({ color, alpha: 0.3 });
      gfx.circle(0, 0, radius);
      gfx.setStrokeStyle({ width: 2, color, alpha: 0.6 });
      gfx.stroke();
      gfx.position.set(x, y);
      this.worldContainer.addChild(gfx);
      for (let i = this.enemyManager.enemies.length - 1; i >= 0; i--) {
        const enemy = this.enemyManager.enemies[i];
        if (!enemy.alive) continue;
        const d = dist({ x, y }, { x: enemy.x, y: enemy.y });
        if (d < radius + enemy.hitRadius) {
          this.enemyManager.damageEnemy(enemy, damage, coinDropMult);
        }
      }
      const startTime = performance.now();
      const animate = () => {
        const elapsed = (performance.now() - startTime) / 1e3;
        if (elapsed >= 0.5) {
          try {
            this.worldContainer.removeChild(gfx);
            gfx.destroy();
          } catch {
          }
          return;
        }
        gfx.alpha = 1 - elapsed / 0.5;
        requestAnimationFrame(animate);
      };
      requestAnimationFrame(animate);
    }
    update(dt, coinDropMult, playerX, playerY) {
      var _a;
      for (let i = this.projectiles.length - 1; i >= 0; i--) {
        const proj = this.projectiles[i];
        proj.age += dt;
        if (proj.originX !== void 0 && proj.maxRange !== void 0) {
          const dFromOrigin = dist({ x: proj.x, y: proj.originX }, { x: proj.y, y: proj.originY });
          if (!proj.returning && dFromOrigin >= proj.maxRange) {
            proj.returning = true;
          }
          if (proj.returning && playerX !== void 0 && playerY !== void 0) {
            const dx = playerX - proj.x;
            const dy = playerY - proj.y;
            const d = Math.sqrt(dx * dx + dy * dy);
            if (d > 1) {
              const speed = Math.sqrt(proj.vx * proj.vx + proj.vy * proj.vy);
              proj.vx = dx / d * speed * 1.2;
              proj.vy = dy / d * speed * 1.2;
            }
            if (d < PROJECTILE_RADIUS + 20) {
              this._release(proj);
              this.projectiles.splice(i, 1);
              continue;
            }
          }
        }
        proj.x += proj.vx * dt;
        proj.y += proj.vy * dt;
        proj.gfx.position.set(proj.x, proj.y);
        if (proj.age >= proj.lifetime || proj.x < -50 || proj.x > this.mapWidth + 50 || proj.y < -50 || proj.y > this.mapHeight + 50) {
          this._release(proj);
          this.projectiles.splice(i, 1);
          continue;
        }
        for (let j = this.enemyManager.enemies.length - 1; j >= 0; j--) {
          const enemy = this.enemyManager.enemies[j];
          if (!enemy.alive) continue;
          if ((_a = proj.hitEnemies) == null ? void 0 : _a.has(enemy)) continue;
          const d = dist({ x: proj.x, y: proj.y }, { x: enemy.x, y: enemy.y });
          const hitDist = (proj.gfx.width / 2 || PROJECTILE_RADIUS) + enemy.hitRadius;
          if (d < hitDist) {
            if (proj.aoeRadius && proj.aoeRadius > 0) {
              this.spawnAoE(proj.x, proj.y, proj.damage, proj.aoeRadius, 16733474, coinDropMult);
              this._release(proj);
              this.projectiles.splice(i, 1);
              break;
            }
            this.enemyManager.damageEnemy(enemy, proj.damage, coinDropMult);
            if (!proj.hitEnemies) proj.hitEnemies = /* @__PURE__ */ new Set();
            proj.hitEnemies.add(enemy);
            if (proj.pierceLeft !== void 0 && proj.pierceLeft !== -1) {
              if (proj.pierceLeft <= 0) {
                this._release(proj);
                this.projectiles.splice(i, 1);
                break;
              }
              proj.pierceLeft--;
            } else if (proj.pierceLeft === void 0) {
              this._release(proj);
              this.projectiles.splice(i, 1);
              break;
            }
          }
        }
      }
    }
    cleanup() {
      for (const proj of this.projectiles) {
        if (proj.gfx) {
          proj.gfx.visible = false;
          if (proj.gfx.parent) proj.gfx.parent.removeChild(proj.gfx);
        }
      }
      this.projectiles = [];
    }
    _getGfx(color, radius) {
      const gfx = this.pool.get();
      gfx.clear();
      gfx.circle(0, 0, radius);
      gfx.fill(color);
      gfx.circle(0, 0, radius + 2);
      gfx.fill({ color, alpha: 0.3 });
      gfx.visible = true;
      gfx.alpha = 1;
      return gfx;
    }
    _release(proj) {
      proj.gfx.visible = false;
      if (proj.gfx.parent) proj.gfx.parent.removeChild(proj.gfx);
      this.pool.release(proj.gfx);
    }
  }
  class WeaponSystem {
    constructor(projectiles, enemyManager, particles, worldContainer, mapWidth, mapHeight) {
      /** Active weapons (max MAX_WEAPON_SLOTS). */
      __publicField(this, "weapons", []);
      /** Active companion entities. */
      __publicField(this, "companions", []);
      // ─── Orbital visuals ────────────────────────────────────────
      __publicField(this, "orbitalContainers", /* @__PURE__ */ new Map());
      __publicField(this, "orbitalAngle", 0);
      // ─── ForceField visual ──────────────────────────────────────
      __publicField(this, "forceFieldGfx", null);
      __publicField(this, "forceFieldTimer", 0);
      // ─── Dependencies ───────────────────────────────────────────
      __publicField(this, "projectiles");
      __publicField(this, "enemyManager");
      __publicField(this, "particles");
      __publicField(this, "worldContainer");
      __publicField(this, "mapWidth");
      __publicField(this, "mapHeight");
      this.projectiles = projectiles;
      this.enemyManager = enemyManager;
      this.particles = particles;
      this.worldContainer = worldContainer;
      this.mapWidth = mapWidth;
      this.mapHeight = mapHeight;
    }
    get weaponCount() {
      return this.weapons.length;
    }
    get isFull() {
      return this.weapons.length >= MAX_WEAPON_SLOTS;
    }
    /** Add a new weapon at level 1. */
    addWeapon(def) {
      if (this.isFull) return null;
      if (this.weapons.some((w) => w.def.id === def.id)) return null;
      const instance = {
        def,
        level: 1,
        cooldownTimer: 0,
        state: {}
      };
      this.weapons.push(instance);
      if (def.pattern === AttackPattern.Companion) {
        this._spawnCompanionPig(instance);
      }
      if (def.pattern === AttackPattern.Orbital) {
        this._createOrbitalVisuals(instance);
      }
      return instance;
    }
    /** Level up a weapon. Returns false if already max. */
    levelUpWeapon(weaponId) {
      const weapon = this.weapons.find((w) => w.def.id === weaponId);
      if (!weapon || weapon.level >= MAX_WEAPON_LEVEL) return false;
      weapon.level++;
      if (weapon.def.pattern === AttackPattern.Orbital) {
        this._destroyOrbitalVisuals(weapon.def.id);
        this._createOrbitalVisuals(weapon);
      }
      if (weapon.def.pattern === AttackPattern.Companion) {
        const stats = this._getStats(weapon);
        while (this.companions.length < stats.count) {
          this._spawnCompanionPig(weapon);
        }
      }
      return true;
    }
    getWeapon(id) {
      return this.weapons.find((w) => w.def.id === id);
    }
    /**
     * Evolve two weapons into one. Removes both ingredients and adds the evolved weapon.
     * The evolved weapon occupies one slot (net -1 slot), freeing space.
     * Returns the new evolved instance, or null if ingredients not found.
     */
    evolveWeapons(ingredientAId, ingredientBId, evolvedDef) {
      const idxA = this.weapons.findIndex((w) => w.def.id === ingredientAId);
      const idxB = this.weapons.findIndex((w) => w.def.id === ingredientBId);
      if (idxA < 0 || idxB < 0) return null;
      this._cleanupWeaponVisuals(ingredientAId);
      this._cleanupWeaponVisuals(ingredientBId);
      const toRemove = [idxA, idxB].sort((a, b) => b - a);
      for (const idx of toRemove) this.weapons.splice(idx, 1);
      const instance = {
        def: evolvedDef,
        level: 1,
        cooldownTimer: 0,
        state: {}
      };
      this.weapons.push(instance);
      if (evolvedDef.pattern === AttackPattern.Companion) {
        const stats = this._getStats(instance);
        for (let i = 0; i < stats.count; i++) {
          this._spawnCompanionPig(instance);
        }
      }
      if (evolvedDef.pattern === AttackPattern.Orbital) {
        this._createOrbitalVisuals(instance);
      }
      return instance;
    }
    /** Remove visuals associated with a weapon being destroyed. */
    _cleanupWeaponVisuals(weaponId) {
      const weapon = this.weapons.find((w) => w.def.id === weaponId);
      if (!weapon) return;
      if (weapon.def.pattern === AttackPattern.Orbital) {
        this._destroyOrbitalVisuals(weaponId);
      }
      if (weapon.def.pattern === AttackPattern.Companion) {
        for (const comp of this.companions) {
          if (comp.container.parent) comp.container.parent.removeChild(comp.container);
          try {
            comp.container.destroy({ children: true });
          } catch {
          }
        }
        this.companions = [];
      }
      if (weapon.def.pattern === AttackPattern.ForceField && this.forceFieldGfx) {
        if (this.forceFieldGfx.parent) this.forceFieldGfx.parent.removeChild(this.forceFieldGfx);
        try {
          this.forceFieldGfx.destroy();
        } catch {
        }
        this.forceFieldGfx = null;
      }
    }
    /** Main update — fires all weapon attacks. */
    update(dt, playerX, playerY, stats) {
      this.orbitalAngle += dt * 2;
      for (const weapon of this.weapons) {
        weapon.cooldownTimer -= dt;
        const wStats = this._getStats(weapon);
        const effectiveCooldown = wStats.cooldown * stats.cooldownMultiplier;
        const effectiveDamage = Math.ceil(wStats.damage * stats.damageMultiplier);
        const effectiveRange = wStats.range * stats.rangeMultiplier;
        if (weapon.cooldownTimer > 0 && weapon.def.pattern !== AttackPattern.Orbital && weapon.def.pattern !== AttackPattern.ForceField) {
          continue;
        }
        switch (weapon.def.pattern) {
          case AttackPattern.Projectile:
            this._fireProjectile(weapon, wStats, playerX, playerY, effectiveDamage, effectiveRange, effectiveCooldown);
            break;
          case AttackPattern.Orbital:
            this._updateOrbital(weapon, wStats, playerX, playerY, effectiveDamage, stats);
            break;
          case AttackPattern.AoE:
            this._fireAoE(weapon, wStats, playerX, playerY, effectiveDamage, effectiveCooldown, stats.coinDropMult);
            break;
          case AttackPattern.Boomerang:
            this._fireBoomerang(weapon, wStats, playerX, playerY, effectiveDamage, effectiveRange, effectiveCooldown);
            break;
          case AttackPattern.Lightning:
            this._fireLightning(weapon, wStats, playerX, playerY, effectiveDamage, effectiveRange, effectiveCooldown, stats.coinDropMult);
            break;
          case AttackPattern.ForceField:
            this._updateForceField(weapon, wStats, dt, playerX, playerY, effectiveDamage, effectiveCooldown, stats.coinDropMult);
            break;
          case AttackPattern.Companion:
            break;
        }
      }
      this._updateCompanions(dt, playerX, playerY, stats);
    }
    // ─── Projectile Weapon ──────────────────────────────────────
    _fireProjectile(weapon, wStats, px, py, damage, range, cooldown) {
      if (weapon.cooldownTimer > 0) return;
      const targets = this._findNearestEnemies(px, py, range, wStats.count);
      if (targets.length === 0) return;
      weapon.cooldownTimer = cooldown;
      if (weapon.def.id === "firerain") {
        for (const target of targets) {
          const ox = target.x + (Math.random() - 0.5) * 60;
          const oy = target.y + (Math.random() - 0.5) * 60;
          this.projectiles.fireProjectile(
            px,
            py - 30,
            // Fire from above player
            ox,
            oy,
            damage,
            weapon.def.color,
            weapon.def.projectileRadius ?? PROJECTILE_RADIUS,
            wStats.speedMult,
            wStats.pierce,
            wStats.aoeRadius,
            2,
            weapon.def.id
          );
        }
        return;
      }
      for (const target of targets) {
        this.projectiles.fireProjectile(
          px,
          py,
          target.x,
          target.y,
          damage,
          weapon.def.color,
          weapon.def.projectileRadius ?? PROJECTILE_RADIUS,
          wStats.speedMult,
          wStats.pierce,
          wStats.aoeRadius,
          void 0,
          weapon.def.id
        );
      }
    }
    // ─── Orbital Weapon ─────────────────────────────────────────
    _updateOrbital(weapon, wStats, px, py, damage, stats) {
      const blades = this.orbitalContainers.get(weapon.def.id);
      if (!blades) return;
      const orbitRadius = wStats.range;
      const angleStep = Math.PI * 2 / blades.length;
      for (let i = 0; i < blades.length; i++) {
        const blade = blades[i];
        const angle = this.orbitalAngle + angleStep * i;
        const bx = px + Math.cos(angle) * orbitRadius;
        const by = py + Math.sin(angle) * orbitRadius;
        blade.position.set(bx, by);
        blade.rotation += 0.15;
        const nearby = this.enemyManager.spatialGrid.query(bx, by);
        for (const enemy of nearby) {
          if (!enemy.alive) continue;
          const d = dist({ x: bx, y: by }, { x: enemy.x, y: enemy.y });
          if (d < 16 + enemy.hitRadius) {
            this.enemyManager.damageEnemy(enemy, damage, stats.coinDropMult);
          }
        }
      }
      if (weapon.def.id === "evo_guardianblade" && wStats.aoeRadius > 0) {
        const pulseKey = "__guardianPulseTimer";
        const timer = (weapon.state[pulseKey] ?? 0) + 1 / 60;
        weapon.state[pulseKey] = timer;
        if (timer >= 1.5) {
          weapon.state[pulseKey] = 0;
          this.projectiles.spawnAoE(px, py, Math.ceil(damage * 0.6), wStats.aoeRadius, weapon.def.color, stats.coinDropMult);
        }
      }
    }
    _createOrbitalVisuals(weapon) {
      const wStats = this._getStats(weapon);
      const blades = [];
      for (let i = 0; i < wStats.count; i++) {
        const gfx = new PIXI__namespace.Graphics();
        gfx.moveTo(-12, 0);
        gfx.lineTo(0, -4);
        gfx.lineTo(12, 0);
        gfx.lineTo(0, 4);
        gfx.closePath();
        gfx.fill(weapon.def.color);
        gfx.setStrokeStyle({ width: 1, color: 16777215, alpha: 0.6 });
        gfx.stroke();
        this.worldContainer.addChild(gfx);
        blades.push(gfx);
      }
      this.orbitalContainers.set(weapon.def.id, blades);
    }
    _destroyOrbitalVisuals(weaponId) {
      const blades = this.orbitalContainers.get(weaponId);
      if (blades) {
        for (const b of blades) {
          if (b.parent) b.parent.removeChild(b);
          try {
            b.destroy();
          } catch {
          }
        }
        this.orbitalContainers.delete(weaponId);
      }
    }
    // ─── AoE Weapon ─────────────────────────────────────────────
    _fireAoE(weapon, wStats, px, py, damage, cooldown, coinDropMult) {
      var _a, _b;
      if (weapon.cooldownTimer > 0) return;
      weapon.cooldownTimer = cooldown;
      if (weapon.def.id === "evo_apocalypse") {
        const targets = this._findNearestEnemies(px, py, wStats.range, wStats.count);
        if (targets.length > 0) {
          for (const t of targets) {
            const ox = t.x + (Math.random() - 0.5) * 40;
            const oy = t.y + (Math.random() - 0.5) * 40;
            this.projectiles.spawnAoE(ox, oy, damage, wStats.aoeRadius, weapon.def.color, coinDropMult);
          }
        } else {
          for (let i = 0; i < wStats.count; i++) {
            const ox = px + (Math.random() - 0.5) * 200;
            const oy = py + (Math.random() - 0.5) * 200;
            this.projectiles.spawnAoE(ox, oy, damage, wStats.aoeRadius, weapon.def.color, coinDropMult);
          }
        }
        this.particles.flashScreen(
          ((_a = this.worldContainer.parent) == null ? void 0 : _a.children[1]) ?? this.worldContainer,
          this.mapWidth,
          this.mapHeight,
          weapon.def.color,
          0.25,
          300
        );
        return;
      }
      this.projectiles.spawnAoE(px, py, damage, wStats.aoeRadius, weapon.def.color, coinDropMult);
      this.particles.flashScreen(
        ((_b = this.worldContainer.parent) == null ? void 0 : _b.children[1]) ?? this.worldContainer,
        this.mapWidth,
        this.mapHeight,
        weapon.def.color,
        0.15,
        200
      );
    }
    // ─── Boomerang Weapon ───────────────────────────────────────
    _fireBoomerang(weapon, wStats, px, py, damage, range, cooldown) {
      if (weapon.cooldownTimer > 0) return;
      const targets = this._findNearestEnemies(px, py, range * 1.5, wStats.count);
      if (targets.length === 0) return;
      weapon.cooldownTimer = cooldown;
      for (const target of targets) {
        this.projectiles.fireBoomerang(
          px,
          py,
          target.x,
          target.y,
          damage,
          weapon.def.color,
          weapon.def.projectileRadius ?? 6,
          range,
          wStats.pierce,
          wStats.speedMult,
          weapon.def.id
        );
      }
    }
    // ─── Lightning Weapon ───────────────────────────────────────
    _fireLightning(weapon, wStats, px, py, damage, range, cooldown, coinDropMult) {
      if (weapon.cooldownTimer > 0) return;
      const initial = this._findNearestEnemies(px, py, range, wStats.count);
      if (initial.length === 0) return;
      weapon.cooldownTimer = cooldown;
      for (const target of initial) {
        this._chainLightning(px, py, target, damage, wStats.pierce, range * 0.6, coinDropMult, weapon.def.color);
      }
    }
    _chainLightning(fromX, fromY, target, damage, chainsLeft, chainRange, coinDropMult, color) {
      this._drawLightningBolt(fromX, fromY, target.x, target.y, color);
      this.enemyManager.damageEnemy(target, damage, coinDropMult);
      if (chainsLeft > 0) {
        const nearby = this.enemyManager.enemies.filter((e) => e.alive && e !== target && dist({ x: target.x, y: target.y }, { x: e.x, y: e.y }) < chainRange).sort((a, b) => dist({ x: target.x, y: target.y }, a) - dist({ x: target.x, y: target.y }, b));
        if (nearby.length > 0) {
          this._chainLightning(target.x, target.y, nearby[0], Math.ceil(damage * 0.8), chainsLeft - 1, chainRange, coinDropMult, color);
        }
      }
    }
    _drawLightningBolt(fromX, fromY, toX, toY, color) {
      const gfx = new PIXI__namespace.Graphics();
      gfx.setStrokeStyle({ width: 3, color, alpha: 0.9 });
      const segments = 6;
      const dx = (toX - fromX) / segments;
      const dy = (toY - fromY) / segments;
      gfx.moveTo(fromX, fromY);
      for (let i = 1; i < segments; i++) {
        const jitterX = (Math.random() - 0.5) * 20;
        const jitterY = (Math.random() - 0.5) * 20;
        gfx.lineTo(fromX + dx * i + jitterX, fromY + dy * i + jitterY);
      }
      gfx.lineTo(toX, toY);
      gfx.stroke();
      this.worldContainer.addChild(gfx);
      const startTime = performance.now();
      const animate = () => {
        const elapsed = (performance.now() - startTime) / 1e3;
        if (elapsed >= 0.3) {
          try {
            this.worldContainer.removeChild(gfx);
            gfx.destroy();
          } catch {
          }
          return;
        }
        gfx.alpha = 1 - elapsed / 0.3;
        requestAnimationFrame(animate);
      };
      requestAnimationFrame(animate);
    }
    // ─── ForceField Weapon ──────────────────────────────────────
    _updateForceField(weapon, wStats, dt, px, py, damage, cooldown, coinDropMult) {
      const radius = wStats.aoeRadius;
      if (!this.forceFieldGfx) {
        this.forceFieldGfx = new PIXI__namespace.Graphics();
        this.worldContainer.addChild(this.forceFieldGfx);
      }
      this.forceFieldGfx.clear();
      this.forceFieldGfx.circle(0, 0, radius);
      this.forceFieldGfx.fill({ color: weapon.def.color, alpha: 0.08 });
      this.forceFieldGfx.circle(0, 0, radius);
      this.forceFieldGfx.setStrokeStyle({ width: 2, color: weapon.def.color, alpha: 0.3 + Math.sin(this.forceFieldTimer) * 0.1 });
      this.forceFieldGfx.stroke();
      this.forceFieldGfx.position.set(px, py);
      this.forceFieldTimer += dt * 3;
      weapon.cooldownTimer -= dt;
      if (weapon.cooldownTimer <= 0) {
        weapon.cooldownTimer = cooldown;
        for (let i = this.enemyManager.enemies.length - 1; i >= 0; i--) {
          const enemy = this.enemyManager.enemies[i];
          if (!enemy.alive) continue;
          const d = dist({ x: px, y: py }, { x: enemy.x, y: enemy.y });
          if (d < radius + enemy.hitRadius) {
            this.enemyManager.damageEnemy(enemy, damage, coinDropMult);
          }
        }
      }
    }
    // ─── Companion System ───────────────────────────────────────
    _spawnCompanionPig(weapon) {
      const idx = this.companions.length;
      const container = new PIXI__namespace.Container();
      const wStats = this._getStats(weapon);
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
      this.worldContainer.addChild(container);
      this.companions.push({
        container,
        x: 0,
        y: 0,
        orbitAngle: idx * (Math.PI * 2 / Math.max(this.companions.length + 1, 1)),
        attackCooldown: 0,
        attackRange: wStats.range,
        orbitRadius: 50 + idx * 20,
        damage: wStats.damage
      });
    }
    _updateCompanions(dt, px, py, stats) {
      const companionWeapon = this.weapons.find((w) => w.def.pattern === AttackPattern.Companion);
      if (!companionWeapon || this.companions.length === 0) return;
      const wStats = this._getStats(companionWeapon);
      const effectiveDamage = Math.ceil(wStats.damage * stats.damageMultiplier);
      const effectiveCooldown = wStats.cooldown * stats.cooldownMultiplier;
      for (const comp of this.companions) {
        comp.orbitAngle += dt * 1.5;
        const targetX = px + Math.cos(comp.orbitAngle) * comp.orbitRadius;
        const targetY = py + Math.sin(comp.orbitAngle) * comp.orbitRadius;
        comp.x = lerp(comp.x, targetX, dt * 5);
        comp.y = lerp(comp.y, targetY, dt * 5);
        comp.container.position.set(comp.x, comp.y);
        const dxComp = targetX - comp.x;
        if (Math.abs(dxComp) > 0.5) {
          comp.container.scale.x = dxComp > 0 ? 1 : -1;
        }
        comp.attackCooldown -= dt;
        if (comp.attackCooldown <= 0) {
          const targets = this._findNearestEnemies(comp.x, comp.y, wStats.range * stats.rangeMultiplier, 1);
          if (targets.length > 0) {
            comp.attackCooldown = effectiveCooldown;
            if (companionWeapon.def.id === "evo_pigrang") {
              this.projectiles.fireBoomerang(
                comp.x,
                comp.y,
                targets[0].x,
                targets[0].y,
                effectiveDamage,
                companionWeapon.def.color,
                companionWeapon.def.projectileRadius ?? 7,
                wStats.range * stats.rangeMultiplier,
                wStats.pierce,
                wStats.speedMult,
                companionWeapon.def.id
              );
            } else {
              this.projectiles.fireProjectile(
                comp.x,
                comp.y,
                targets[0].x,
                targets[0].y,
                effectiveDamage,
                16738740,
                companionWeapon.def.projectileRadius ?? 3,
                wStats.speedMult,
                wStats.pierce,
                0,
                void 0,
                companionWeapon.def.id
              );
            }
          }
        }
      }
    }
    // ─── Helpers ────────────────────────────────────────────────
    _getStats(weapon) {
      return weapon.def.levels[weapon.level - 1];
    }
    _findNearestEnemies(x, y, range, count) {
      const enemies = this.enemyManager.enemies.filter((e) => e.alive).map((e) => ({ enemy: e, dist: dist({ x, y }, { x: e.x, y: e.y }) })).filter((e) => e.dist < range).sort((a, b) => a.dist - b.dist);
      return enemies.slice(0, count).map((e) => e.enemy);
    }
    // ─── Cleanup ────────────────────────────────────────────────
    cleanup() {
      for (const [id] of this.orbitalContainers) {
        this._destroyOrbitalVisuals(id);
      }
      this.orbitalContainers.clear();
      if (this.forceFieldGfx) {
        if (this.forceFieldGfx.parent) this.forceFieldGfx.parent.removeChild(this.forceFieldGfx);
        try {
          this.forceFieldGfx.destroy();
        } catch {
        }
        this.forceFieldGfx = null;
      }
      for (const comp of this.companions) {
        if (comp.container.parent) comp.container.parent.removeChild(comp.container);
        try {
          comp.container.destroy({ children: true });
        } catch {
        }
      }
      this.companions = [];
      this.weapons = [];
    }
  }
  const LEVEL_THRESHOLDS = [5, 12, 22, 35, 55, 80];
  const LEVEL_THRESHOLD_STEP = 35;
  const PASSIVE_DEFS = [
    {
      id: "moveSpeed",
      icon: "🏃",
      name: "Pés Rápidos",
      description: "+12% velocidade de movimento",
      maxLevel: 5,
      apply: (s) => {
        s.playerSpeed *= 1.12;
      }
    },
    {
      id: "damage",
      icon: "⚔️",
      name: "Força Bruta",
      description: "+15% dano total",
      maxLevel: 5,
      apply: (s) => {
        s.damageMultiplier *= 1.15;
      }
    },
    {
      id: "hp",
      icon: "❤️",
      name: "Vitalidade",
      description: "+15 HP máximo e cura 10",
      maxLevel: 5,
      apply: (s) => {
        s.maxHP += 15;
        s.playerHP = Math.min(s.playerHP + 10, s.maxHP);
      }
    },
    {
      id: "atkRange",
      icon: "🎯",
      name: "Olho de Águia",
      description: "+12% alcance",
      maxLevel: 5,
      apply: (s) => {
        s.rangeMultiplier *= 1.12;
      }
    },
    {
      id: "atkSpeed",
      icon: "⚡",
      name: "Fogo Rápido",
      description: "+12% velocidade de ataque",
      maxLevel: 5,
      apply: (s) => {
        s.cooldownMultiplier *= 0.88;
      }
    },
    {
      id: "armor",
      icon: "🛡️",
      name: "Defesa",
      description: "-8% dano recebido",
      maxLevel: 5,
      apply: (s) => {
        s.armor = 1 - (1 - s.armor) * 0.92;
      }
    },
    {
      id: "magnet",
      icon: "🧲",
      name: "Íman de Moedas",
      description: "+80 raio de recolha",
      maxLevel: 3,
      apply: (s) => {
        s.magnetRadius += 80;
      }
    },
    {
      id: "coinRate",
      icon: "🪙",
      name: "Febre do Ouro",
      description: "2x moedas por inimigo",
      maxLevel: 3,
      apply: (s) => {
        s.coinDropMult *= 2;
      }
    }
  ];
  class UpgradeSystem {
    constructor(weaponSystem, stats, audio, uiContainer, vpWidth, vpHeight) {
      /** Number of XP orbs collected this run. */
      __publicField(this, "xpOrbsCollected", 0);
      /** Player level (starts at 1). */
      __publicField(this, "playerLevel", 1);
      /** Is the upgrade popup showing? */
      __publicField(this, "paused", false);
      // Private
      __publicField(this, "nextLevelAt");
      __publicField(this, "levelIndex", 0);
      __publicField(this, "passiveLevels", {});
      __publicField(this, "overlay", null);
      __publicField(this, "weaponSystem");
      __publicField(this, "stats");
      __publicField(this, "audio");
      __publicField(this, "uiContainer");
      __publicField(this, "vpWidth");
      __publicField(this, "vpHeight");
      /** Emitted when player picks an upgrade (for UI refresh). */
      __publicField(this, "onUpgradePicked", null);
      this.weaponSystem = weaponSystem;
      this.stats = stats;
      this.audio = audio;
      this.uiContainer = uiContainer;
      this.vpWidth = vpWidth;
      this.vpHeight = vpHeight;
      this.nextLevelAt = LEVEL_THRESHOLDS[0];
    }
    /** Called when an XP orb is collected. Returns true if level-up triggered. */
    collectOrb() {
      this.xpOrbsCollected++;
      if (this.xpOrbsCollected >= this.nextLevelAt) {
        this._advanceLevel();
        this.showUpgradePopup();
        return true;
      }
      return false;
    }
    _advanceLevel() {
      this.playerLevel++;
      this.levelIndex++;
      if (this.levelIndex < LEVEL_THRESHOLDS.length) {
        this.nextLevelAt = LEVEL_THRESHOLDS[this.levelIndex];
      } else {
        this.nextLevelAt += LEVEL_THRESHOLD_STEP;
      }
    }
    /** Build 3 upgrade choices and show the popup. */
    showUpgradePopup() {
      if (this.paused) return;
      this.paused = true;
      const choices = this._buildChoices(3);
      if (choices.length === 0) {
        this.paused = false;
        return;
      }
      this.audio.playSFX("levelup");
      this._renderPopup(choices);
    }
    /** Check which evolutions are currently available (both ingredients at max level). */
    _getAvailableEvolutions() {
      const owned = new Map(this.weaponSystem.weapons.map((w) => [w.def.id, w]));
      return EVOLUTION_RECIPES.filter((evo) => {
        if (owned.has(evo.result.id)) return false;
        const a = owned.get(evo.ingredientA);
        const b = owned.get(evo.ingredientB);
        return a && b && a.level >= MAX_WEAPON_LEVEL && b.level >= MAX_WEAPON_LEVEL;
      });
    }
    _buildChoices(count) {
      const pool = [];
      const evolutions = this._getAvailableEvolutions();
      const evoChoices = evolutions.map((evo) => ({
        type: UpgradeChoiceType.Evolution,
        evolutionDef: evo,
        weaponDef: evo.result,
        icon: evo.result.icon,
        title: `⟐ ${evo.result.name}`,
        desc: evo.description
      }));
      if (!this.weaponSystem.isFull) {
        const owned = new Set(this.weaponSystem.weapons.map((w) => w.def.id));
        const available = ALL_WEAPONS.filter((w) => !owned.has(w.id));
        for (const wDef of available) {
          pool.push({
            type: UpgradeChoiceType.NewWeapon,
            weaponDef: wDef,
            icon: wDef.icon,
            title: wDef.name,
            desc: wDef.description
          });
        }
      }
      for (const weapon of this.weaponSystem.weapons) {
        if (weapon.level < MAX_WEAPON_LEVEL && weapon.def.levels.length > 1) {
          const nextStats = weapon.def.levels[weapon.level];
          const desc = nextStats ? `Nível ${weapon.level + 1} — +dano, +efeito` : `Nível ${weapon.level + 1}`;
          pool.push({
            type: UpgradeChoiceType.WeaponLevelUp,
            weaponDef: weapon.def,
            icon: weapon.def.icon,
            title: `${weapon.def.name} ↑`,
            desc
          });
        }
      }
      for (const passive of PASSIVE_DEFS) {
        const current = this.passiveLevels[passive.id] ?? 0;
        if (current < passive.maxLevel) {
          pool.push({
            type: UpgradeChoiceType.Passive,
            passiveDef: passive,
            icon: passive.icon,
            title: `${passive.name} ${current > 0 ? `(${current + 1})` : ""}`,
            desc: passive.description
          });
        }
      }
      const shuffled = pool.sort(() => Math.random() - 0.5);
      const result = [];
      for (const evo of evoChoices) {
        if (result.length >= count) break;
        result.push(evo);
      }
      const newWeapons = shuffled.filter((c) => c.type === UpgradeChoiceType.NewWeapon);
      const others = shuffled.filter((c) => c.type !== UpgradeChoiceType.NewWeapon);
      if (result.length < count && newWeapons.length > 0 && !this.weaponSystem.isFull && this.weaponSystem.weaponCount < 4) {
        result.push(newWeapons[0]);
      }
      const remaining = [...newWeapons.slice(result.some((r) => r.type === UpgradeChoiceType.NewWeapon) ? 1 : 0), ...others];
      const shuffledRemaining = remaining.sort(() => Math.random() - 0.5);
      for (const choice of shuffledRemaining) {
        if (result.length >= count) break;
        if (!result.includes(choice)) result.push(choice);
      }
      return result.slice(0, count);
    }
    _applyChoice(choice) {
      var _a;
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
              choice.evolutionDef.result
            );
          }
          break;
      }
      (_a = this.onUpgradePicked) == null ? void 0 : _a.call(this);
    }
    // ─── Popup Rendering ────────────────────────────────────────
    _renderPopup(choices) {
      const overlay = new PIXI__namespace.Container();
      const bg = new PIXI__namespace.Graphics();
      bg.rect(0, 0, this.vpWidth, this.vpHeight);
      bg.fill({ color: 0, alpha: 0.75 });
      bg.eventMode = "static";
      overlay.addChild(bg);
      const titleText = new PIXI__namespace.Text({
        text: `NÍVEL ${this.playerLevel} — ESCOLHE`,
        style: {
          fontFamily: "Arial",
          fontSize: 22,
          fontWeight: "bold",
          fill: 16766720,
          align: "center",
          dropShadow: { color: 0, blur: 4, distance: 2 }
        }
      });
      titleText.anchor.set(0.5, 0.5);
      titleText.position.set(this.vpWidth / 2, this.vpHeight * 0.15);
      overlay.addChild(titleText);
      const cardW = Math.min(140, (this.vpWidth - 60) / 3);
      const cardH = 180;
      const gap = 12;
      const totalW = cardW * choices.length + gap * (choices.length - 1);
      const startX = (this.vpWidth - totalW) / 2;
      const cardY = this.vpHeight / 2 - cardH / 2;
      choices.forEach((choice, idx) => {
        const cx = startX + idx * (cardW + gap);
        const card = new PIXI__namespace.Container();
        card.eventMode = "static";
        card.cursor = "pointer";
        const borderColor = choice.type === UpgradeChoiceType.Evolution ? 16739904 : choice.type === UpgradeChoiceType.NewWeapon ? 5227511 : choice.type === UpgradeChoiceType.WeaponLevelUp ? 16766720 : 8505220;
        const cardBg = new PIXI__namespace.Graphics();
        cardBg.roundRect(0, 0, cardW, cardH, 10);
        cardBg.fill({ color: 1710654, alpha: 0.95 });
        cardBg.setStrokeStyle({ width: 2, color: borderColor, alpha: 0.8 });
        cardBg.stroke();
        card.addChild(cardBg);
        const hoverBg = new PIXI__namespace.Graphics();
        hoverBg.roundRect(0, 0, cardW, cardH, 10);
        hoverBg.fill({ color: 2763358, alpha: 0.95 });
        hoverBg.setStrokeStyle({ width: 3, color: 16772693 });
        hoverBg.stroke();
        hoverBg.visible = false;
        card.addChild(hoverBg);
        const badgeText = choice.type === UpgradeChoiceType.Evolution ? "⟐ EVOLUÇÃO" : choice.type === UpgradeChoiceType.NewWeapon ? "NOVA ARMA" : choice.type === UpgradeChoiceType.WeaponLevelUp ? "UPGRADE" : "PASSIVO";
        const badge = new PIXI__namespace.Text({
          text: badgeText,
          style: { fontFamily: "Arial", fontSize: 8, fontWeight: "bold", fill: borderColor }
        });
        badge.anchor.set(0.5, 0);
        badge.position.set(cardW / 2, 6);
        card.addChild(badge);
        const icon = new PIXI__namespace.Text({ text: choice.icon, style: { fontSize: 36 } });
        icon.anchor.set(0.5, 0.5);
        icon.position.set(cardW / 2, 45);
        card.addChild(icon);
        const tText = new PIXI__namespace.Text({
          text: choice.title,
          style: {
            fontFamily: "Arial",
            fontSize: 13,
            fontWeight: "bold",
            fill: 16777215,
            align: "center",
            wordWrap: true,
            wordWrapWidth: cardW - 16
          }
        });
        tText.anchor.set(0.5, 0);
        tText.position.set(cardW / 2, 70);
        card.addChild(tText);
        const dText = new PIXI__namespace.Text({
          text: choice.desc,
          style: {
            fontFamily: "Arial",
            fontSize: 10,
            fill: 12303291,
            align: "center",
            wordWrap: true,
            wordWrapWidth: cardW - 16
          }
        });
        dText.anchor.set(0.5, 0);
        dText.position.set(cardW / 2, 100);
        card.addChild(dText);
        card.on("pointerdown", () => {
          this._applyChoice(choice);
          this.uiContainer.removeChild(overlay);
          this.overlay = null;
          this.paused = false;
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
      this.overlay = overlay;
      this.uiContainer.addChild(overlay);
    }
    cleanup() {
      if (this.overlay) {
        try {
          this.uiContainer.removeChild(this.overlay);
        } catch {
        }
        this.overlay = null;
      }
      this.paused = false;
    }
  }
  const TOP_BAR_OFFSET = 54;
  class UIManager {
    constructor(uiContainer, vpWidth, vpHeight) {
      __publicField(this, "uiContainer");
      __publicField(this, "vpWidth");
      __publicField(this, "vpHeight");
      // Timer
      __publicField(this, "timerText");
      __publicField(this, "timerBar");
      __publicField(this, "timerBarWidth", 0);
      __publicField(this, "timerBarHeight", 0);
      __publicField(this, "timerBarX", 0);
      __publicField(this, "timerBarY", 0);
      // HP
      __publicField(this, "hpBar");
      __publicField(this, "hpBarWidth", 0);
      __publicField(this, "hpBarHeight", 0);
      __publicField(this, "hpBarX", 0);
      __publicField(this, "hpBarY", 0);
      __publicField(this, "hpText", null);
      // Counters
      __publicField(this, "killText");
      __publicField(this, "orbText", null);
      __publicField(this, "statsText", null);
      // Weapon slots
      __publicField(this, "weaponSlotContainer", null);
      __publicField(this, "weaponSlotIcons", []);
      __publicField(this, "weaponSlotBadges", []);
      // Boss HP bar
      __publicField(this, "bossHPBarBg", null);
      __publicField(this, "bossHPBarFill", null);
      __publicField(this, "bossHPBarText", null);
      __publicField(this, "_bossBarX", 0);
      __publicField(this, "_bossBarY", 0);
      __publicField(this, "_bossBarW", 0);
      __publicField(this, "_bossBarH", 0);
      __publicField(this, "_bossBarFinal", false);
      // Minimap
      __publicField(this, "minimapContainer", null);
      __publicField(this, "_mmPlayerDot", null);
      __publicField(this, "_mmEnemyDots", null);
      __publicField(this, "_mmVpRect", null);
      // Level display
      __publicField(this, "levelText", null);
      __publicField(this, "playerLevelText", null);
      this.uiContainer = uiContainer;
      this.vpWidth = vpWidth;
      this.vpHeight = vpHeight;
    }
    create(level, biomeName, timerDuration, maxHP) {
      const timerBarW = 240;
      const timerBarH = 24;
      const timerBarX = this.vpWidth / 2 - timerBarW / 2;
      const timerBarY = TOP_BAR_OFFSET + 10;
      const timerBarBg = new PIXI__namespace.Graphics();
      timerBarBg.roundRect(timerBarX, timerBarY, timerBarW, timerBarH, 6);
      timerBarBg.fill({ color: 0, alpha: 0.7 });
      this.uiContainer.addChild(timerBarBg);
      this.timerBar = new PIXI__namespace.Graphics();
      this.timerBarWidth = timerBarW;
      this.timerBarHeight = timerBarH;
      this.timerBarX = timerBarX;
      this.timerBarY = timerBarY;
      this.uiContainer.addChild(this.timerBar);
      this.timerText = new PIXI__namespace.Text({
        text: formatTime(timerDuration),
        style: { fontFamily: "Arial", fontSize: 14, fontWeight: "bold", fill: 16777215, align: "center" }
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
      this.uiContainer.addChild(this.hpBar);
      this.hpText = new PIXI__namespace.Text({
        text: `${maxHP}/${maxHP}`,
        style: { fontFamily: "Arial", fontSize: 10, fontWeight: "bold", fill: 16777215, align: "center" }
      });
      this.hpText.anchor.set(0.5, 0.5);
      this.hpText.position.set(this.vpWidth / 2, hpBarY + hpBarH / 2);
      this.uiContainer.addChild(this.hpText);
      const leftY = TOP_BAR_OFFSET + 10;
      const levelBg = new PIXI__namespace.Graphics();
      levelBg.roundRect(8, leftY, 170, 24, 5);
      levelBg.fill({ color: 0, alpha: 0.6 });
      this.uiContainer.addChild(levelBg);
      this.levelText = new PIXI__namespace.Text({
        text: `Cap.${level} — ${biomeName}`,
        style: { fontFamily: "Arial", fontSize: 13, fontWeight: "bold", fill: 16763904 }
      });
      this.levelText.position.set(14, leftY + 3);
      this.uiContainer.addChild(this.levelText);
      const statsBg = new PIXI__namespace.Graphics();
      statsBg.roundRect(8, leftY + 28, 170, 38, 5);
      statsBg.fill({ color: 0, alpha: 0.5 });
      this.uiContainer.addChild(statsBg);
      this.statsText = new PIXI__namespace.Text({
        text: "",
        style: { fontFamily: "Arial", fontSize: 10, fill: 13421772, lineHeight: 13 }
      });
      this.statsText.position.set(14, leftY + 31);
      this.uiContainer.addChild(this.statsText);
      const lvlBg = new PIXI__namespace.Graphics();
      lvlBg.roundRect(8, leftY + 70, 80, 20, 5);
      lvlBg.fill({ color: 0, alpha: 0.5 });
      this.uiContainer.addChild(lvlBg);
      this.playerLevelText = new PIXI__namespace.Text({
        text: "Lv.1",
        style: { fontFamily: "Arial", fontSize: 11, fontWeight: "bold", fill: 5227511 }
      });
      this.playerLevelText.position.set(14, leftY + 72);
      this.uiContainer.addChild(this.playerLevelText);
      const rightY = TOP_BAR_OFFSET + 10;
      const killBg = new PIXI__namespace.Graphics();
      killBg.roundRect(this.vpWidth - 120, rightY, 112, 24, 5);
      killBg.fill({ color: 0, alpha: 0.6 });
      this.uiContainer.addChild(killBg);
      this.killText = new PIXI__namespace.Text({
        text: "☠ 0",
        style: { fontFamily: "Arial", fontSize: 13, fontWeight: "bold", fill: 16737894 }
      });
      this.killText.position.set(this.vpWidth - 114, rightY + 3);
      this.uiContainer.addChild(this.killText);
      const orbBg = new PIXI__namespace.Graphics();
      orbBg.roundRect(this.vpWidth - 120, rightY + 28, 112, 24, 5);
      orbBg.fill({ color: 0, alpha: 0.6 });
      this.uiContainer.addChild(orbBg);
      this.orbText = new PIXI__namespace.Text({
        text: "🪙 0",
        style: { fontFamily: "Arial", fontSize: 13, fontWeight: "bold", fill: 16766720 }
      });
      this.orbText.position.set(this.vpWidth - 114, rightY + 31);
      this.uiContainer.addChild(this.orbText);
      this._createWeaponSlots();
      this._createMinimap();
    }
    // ─── Frame Update ───────────────────────────────────────────
    update(timeRemaining, timerDuration, playerHP, maxHP, enemiesKilled, xpOrbs, stats, weaponSystem, upgradeSystem) {
      this.timerText.text = formatTime(timeRemaining);
      this._updateTimerBar(timeRemaining, timerDuration);
      this._updateHPBar(playerHP, maxHP);
      if (this.hpText) this.hpText.text = `${Math.ceil(playerHP)}/${maxHP}`;
      this.killText.text = `☠ ${enemiesKilled}`;
      if (this.orbText) this.orbText.text = `🪙 ${xpOrbs}`;
      if (this.playerLevelText) this.playerLevelText.text = `Lv.${upgradeSystem.playerLevel}`;
      if (this.statsText) {
        const dmg = `⚔ DMG x${stats.damageMultiplier.toFixed(1)}`;
        const spd = `⚡ SPD x${(1 / stats.cooldownMultiplier).toFixed(1)}`;
        const rng = `🎯 RNG x${stats.rangeMultiplier.toFixed(1)}`;
        this.statsText.text = `${dmg}  ${spd}
${rng}  🏃 ${Math.round(stats.playerSpeed)}`;
      }
      this._updateWeaponSlots(weaponSystem);
    }
    // ─── Minimap ────────────────────────────────────────────────
    updateMinimap(playerX, playerY, camX, camY, vpWidth, vpHeight, mapWidth, mapHeight, enemies) {
      if (!this._mmPlayerDot || !this._mmEnemyDots || !this._mmVpRect) return;
      const scaleX = MINIMAP_SIZE / mapWidth;
      const scaleY = MINIMAP_SIZE / mapHeight;
      this._mmPlayerDot.clear();
      this._mmPlayerDot.circle(playerX * scaleX, playerY * scaleY, 3);
      this._mmPlayerDot.fill(5227511);
      this._mmEnemyDots.clear();
      for (const enemy of enemies) {
        if (!enemy.alive) continue;
        this._mmEnemyDots.circle(enemy.x * scaleX, enemy.y * scaleY, enemy.isElite ? 2 : 1);
      }
      this._mmEnemyDots.fill(15022389);
      this._mmVpRect.clear();
      this._mmVpRect.rect(camX * scaleX, camY * scaleY, vpWidth * scaleX, vpHeight * scaleY);
      this._mmVpRect.setStrokeStyle({ width: 1, color: 16777215, alpha: 0.5 });
      this._mmVpRect.stroke();
    }
    // ─── Boss HP Bar ────────────────────────────────────────────
    showBossHPBar(isFinal) {
      this.hideBossHPBar();
      const barWidth = 300;
      const barHeight = 16;
      const x = (this.vpWidth - barWidth) / 2;
      const y = TOP_BAR_OFFSET + 70;
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
        style: { fontFamily: "Arial", fontSize: 12, fontWeight: "bold", fill: 16777215, stroke: { color: 0, width: 2 } }
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
    updateBossHPBar(boss) {
      if (!boss || !this.bossHPBarFill) return;
      const ratio = Math.max(0, boss.hp / boss.maxHp);
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
    // ─── End-Game Messages ──────────────────────────────────────
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
    // ─── Private ────────────────────────────────────────────────
    _updateTimerBar(timeRemaining, timerDuration) {
      if (!this.timerBar) return;
      this.timerBar.clear();
      const progress = clamp(timeRemaining / timerDuration, 0, 1);
      const barW = this.timerBarWidth * progress;
      let color;
      if (progress > 0.5) color = 4431943;
      else if (progress > 0.25) color = 16757504;
      else color = 15022389;
      this.timerBar.roundRect(this.timerBarX, this.timerBarY, barW, this.timerBarHeight, 6);
      this.timerBar.fill(color);
    }
    _updateHPBar(playerHP, maxHP) {
      if (!this.hpBar) return;
      this.hpBar.clear();
      const progress = clamp(playerHP / maxHP, 0, 1);
      const barW = this.hpBarWidth * progress;
      let color;
      if (progress > 0.6) color = 15022389;
      else if (progress > 0.3) color = 12986408;
      else color = 12000284;
      this.hpBar.roundRect(this.hpBarX, this.hpBarY, barW, this.hpBarHeight, 5);
      this.hpBar.fill(color);
    }
    _createWeaponSlots() {
      this.weaponSlotContainer = new PIXI__namespace.Container();
      const slotSize = 36;
      const gap = 6;
      const totalW = 6 * slotSize + 5 * gap;
      const startX = (this.vpWidth - totalW) / 2;
      const y = this.vpHeight - slotSize - 12;
      for (let i = 0; i < 6; i++) {
        const x = startX + i * (slotSize + gap);
        const bg = new PIXI__namespace.Graphics();
        bg.roundRect(x, y, slotSize, slotSize, 6);
        bg.fill({ color: 1710638, alpha: 0.8 });
        bg.setStrokeStyle({ width: 1, color: 5592439, alpha: 0.6 });
        bg.stroke();
        this.weaponSlotContainer.addChild(bg);
        const icon = new PIXI__namespace.Text({
          text: "",
          style: { fontSize: 20 }
        });
        icon.anchor.set(0.5, 0.5);
        icon.position.set(x + slotSize / 2, y + slotSize / 2 - 3);
        this.weaponSlotContainer.addChild(icon);
        this.weaponSlotIcons.push(icon);
        const badge = new PIXI__namespace.Text({
          text: "",
          style: { fontFamily: "Arial", fontSize: 8, fontWeight: "bold", fill: 16766720 }
        });
        badge.anchor.set(0.5, 0);
        badge.position.set(x + slotSize / 2, y + slotSize - 12);
        this.weaponSlotContainer.addChild(badge);
        this.weaponSlotBadges.push(badge);
      }
      this.uiContainer.addChild(this.weaponSlotContainer);
    }
    _updateWeaponSlots(weaponSystem) {
      const romanNumerals = ["I", "II", "III", "IV", "V"];
      for (let i = 0; i < 6; i++) {
        if (i < weaponSystem.weapons.length) {
          const w = weaponSystem.weapons[i];
          this.weaponSlotIcons[i].text = w.def.icon;
          this.weaponSlotBadges[i].text = romanNumerals[w.level - 1] ?? `${w.level}`;
        } else {
          this.weaponSlotIcons[i].text = "";
          this.weaponSlotBadges[i].text = "";
        }
      }
    }
    _createMinimap() {
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
    }
  }
  let app = null;
  let scene = null;
  let gameActive = false;
  let gamePaused = false;
  let dotNetRef = null;
  class SurviveScene {
    // ────────────────────────────────────────────────────────────
    constructor(containerId, levelData) {
      __publicField(this, "containerId");
      __publicField(this, "data");
      // Level config
      __publicField(this, "level");
      __publicField(this, "biomeName");
      __publicField(this, "timerDuration");
      __publicField(this, "mapWidth");
      __publicField(this, "mapHeight");
      __publicField(this, "vpWidth");
      __publicField(this, "vpHeight");
      __publicField(this, "backgroundPath");
      __publicField(this, "enemySprites");
      __publicField(this, "playerSpritePath");
      __publicField(this, "bossSprites");
      __publicField(this, "isFinalLevel");
      // Runtime timers
      __publicField(this, "timeRemaining");
      __publicField(this, "timeElapsed", 0);
      __publicField(this, "alive", true);
      __publicField(this, "won", false);
      __publicField(this, "timerStopped", false);
      __publicField(this, "invulnTimer", 0);
      // Player
      __publicField(this, "player", null);
      __publicField(this, "playerX");
      __publicField(this, "playerY");
      __publicField(this, "playerGlow", null);
      __publicField(this, "playerArrow", null);
      __publicField(this, "_velX", 0);
      __publicField(this, "_velY", 0);
      /** Shared mutable stats read/written by UpgradeSystem passives. */
      __publicField(this, "stats");
      // Camera
      __publicField(this, "camX");
      __publicField(this, "camY");
      // Containers
      __publicField(this, "worldContainer");
      __publicField(this, "uiContainer");
      // XP orbs (cross-cut upgrade + economy)
      __publicField(this, "xpOrbs", []);
      __publicField(this, "_orbPool", null);
      // Asset aliases
      __publicField(this, "_bgAlias", "");
      __publicField(this, "_playerAlias", "");
      __publicField(this, "enemySpriteAliases", []);
      __publicField(this, "bossSpriteAliases", []);
      // ── Subsystem managers ──────────────────────────────────────
      __publicField(this, "audio");
      __publicField(this, "input");
      __publicField(this, "particles");
      __publicField(this, "enemyManager");
      __publicField(this, "projectiles");
      __publicField(this, "weaponSystem");
      __publicField(this, "upgradeSystem");
      __publicField(this, "ui");
      this.containerId = containerId;
      this.data = levelData;
      this.level = levelData.level ?? levelData.Level ?? 1;
      this.biomeName = levelData.biomeName ?? levelData.BiomeName ?? "Forest";
      this.timerDuration = levelData.timerDurationSeconds ?? levelData.TimerDurationSeconds ?? 60;
      this.mapWidth = levelData.mapWidth ?? levelData.MapWidth ?? 2e3;
      this.mapHeight = levelData.mapHeight ?? levelData.MapHeight ?? 2e3;
      this.vpWidth = levelData.viewportWidth ?? levelData.ViewportWidth ?? 800;
      this.vpHeight = levelData.viewportHeight ?? levelData.ViewportHeight ?? 600;
      this.backgroundPath = levelData.backgroundPath ?? levelData.BackgroundPath ?? "";
      this.enemySprites = levelData.enemySprites ?? levelData.EnemySprites ?? [];
      this.playerSpritePath = levelData.playerSpritePath ?? levelData.PlayerSpritePath ?? "";
      this.bossSprites = levelData.bossSprites ?? levelData.BossSprites ?? [];
      this.isFinalLevel = levelData.isFinalLevel ?? levelData.IsFinalLevel ?? false;
      this.timeRemaining = this.timerDuration;
      this.playerX = this.mapWidth / 2;
      this.playerY = this.mapHeight / 2;
      this.camX = clamp(this.playerX - this.vpWidth / 2, 0, this.mapWidth - this.vpWidth);
      this.camY = clamp(this.playerY - this.vpHeight / 2, 0, this.mapHeight - this.vpHeight);
      this.stats = {
        playerSpeed: levelData.playerSpeed ?? levelData.PlayerSpeed ?? 120,
        maxHP: PLAYER_MAX_HP,
        playerHP: PLAYER_MAX_HP,
        damageMultiplier: 1,
        cooldownMultiplier: 1,
        rangeMultiplier: 1,
        magnetRadius: XP_PICKUP_RADIUS,
        coinDropMult: 1,
        armor: 0
      };
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
      this.worldContainer.position.set(-this.camX, -this.camY);
      this._orbPool = new ObjectPool(
        () => new PIXI__namespace.Graphics(),
        (gfx) => {
          gfx.clear();
          gfx.alpha = 1;
          gfx.visible = false;
        },
        40
      );
      await this._loadAssets();
      this._createBackground();
      this._createPlayer();
      this.audio = new AudioManager();
      this.input = new InputManager(this.vpWidth, this.vpHeight);
      this.input.setup(app, this.uiContainer);
      this.particles = new ParticleSystem(this.worldContainer);
      const enemyCfg = {
        level: this.level,
        baseEnemyCount: this.data.baseEnemyCount ?? this.data.BaseEnemyCount ?? 5,
        maxEnemyCount: this.data.maxEnemyCount ?? this.data.MaxEnemyCount ?? 80,
        spawnInterval: this.data.spawnIntervalSeconds ?? this.data.SpawnIntervalSeconds ?? 2,
        enemySpeed: this.data.enemySpeed ?? this.data.EnemySpeed ?? 60,
        maxEnemySpeed: this.data.maxEnemySpeed ?? this.data.MaxEnemySpeed ?? 150,
        enemyScale: this.data.enemyScale ?? this.data.EnemyScale ?? 1,
        hasElites: this.data.hasEliteEnemies ?? this.data.HasEliteEnemies ?? false,
        eliteChance: this.data.eliteSpawnChance ?? this.data.EliteSpawnChance ?? 0,
        mapWidth: this.mapWidth,
        mapHeight: this.mapHeight,
        vpWidth: this.vpWidth,
        vpHeight: this.vpHeight,
        isFinalLevel: this.isFinalLevel,
        spawnRampPerMinute: this.data.spawnRampPerMinute ?? this.data.SpawnRampPerMinute ?? 0.2,
        speedRampPerMinute: this.data.speedRampPerMinute ?? this.data.SpeedRampPerMinute ?? 0.1,
        timerDuration: this.timerDuration,
        enemySpriteAliases: this.enemySpriteAliases,
        bossSpriteAliases: this.bossSpriteAliases
      };
      this.enemyManager = new EnemyManager(enemyCfg, this.worldContainer, this.particles, this.audio);
      this.enemyManager.onSpawnOrb = (x, y) => this._spawnXPOrb(x, y);
      this.projectiles = new ProjectileManager(this.worldContainer, this.mapWidth, this.mapHeight, this.enemyManager);
      this.weaponSystem = new WeaponSystem(
        this.projectiles,
        this.enemyManager,
        this.particles,
        this.worldContainer,
        this.mapWidth,
        this.mapHeight
      );
      this.upgradeSystem = new UpgradeSystem(
        this.weaponSystem,
        this.stats,
        this.audio,
        this.uiContainer,
        this.vpWidth,
        this.vpHeight
      );
      this.ui = new UIManager(this.uiContainer, this.vpWidth, this.vpHeight);
      this.ui.create(this.level, this.biomeName, this.timerDuration, this.stats.maxHP);
      this.enemyManager.onBossSpawn = (isFinal) => {
        this.ui.showBossHPBar(isFinal);
        this.particles.flashScreen(this.uiContainer, this.vpWidth, this.vpHeight, isFinal ? 16711680 : 16737792);
        this.timerStopped = true;
      };
      this.enemyManager.onBossDeath = (isFinal) => {
        this.ui.hideBossHPBar();
        if (isFinal) {
          this.won = true;
          this._onWin();
        } else {
          this.timerStopped = false;
        }
      };
      this.enemyManager.spawnInitial();
      this.weaponSystem.addWeapon(WEAPON_SHARPSHOT);
      this.audio.playMusic();
      this.alive = true;
      this.won = false;
      gameActive = true;
      app.ticker.add(this._update, this);
    }
    // ─── Asset Loading ──────────────────────────────────────────
    async _loadAssets() {
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
    _createBackground() {
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
        decorations.circle(Math.random() * this.mapWidth, Math.random() * this.mapHeight, 2 + Math.random() * 6);
      }
      decorations.fill({ color: decoColor, alpha: 0.3 });
      this.worldContainer.addChild(decorations);
      try {
        const bgTexture = PIXI__namespace.Assets.get(this._bgAlias || `surviveBg_${this.level}`);
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
    _createPlayer() {
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
    // ─── Game Loop ──────────────────────────────────────────────
    _update(ticker) {
      var _a;
      if (!this.alive || this.won || gamePaused || this.upgradeSystem.paused) return;
      const dt = ticker.deltaMS / 1e3;
      this.timeElapsed += dt;
      if (!this.timerStopped) this.timeRemaining -= dt;
      const hasBosses = this.bossSpriteAliases.length > 0 && !this.isFinalLevel;
      if (hasBosses && !this.enemyManager.midBossSpawned && this.timeRemaining <= this.timerDuration / 2) {
        this.enemyManager.midBossSpawned = true;
        this.enemyManager.spawnBoss(false, this.playerX, this.playerY);
      }
      if (this.timeRemaining <= 0) {
        this.timeRemaining = 0;
        if (this.isFinalLevel || !hasBosses) {
          this.won = true;
          this._onWin();
          return;
        }
        if (!this.enemyManager.finalBossSpawned) {
          this.enemyManager.finalBossSpawned = true;
          this.enemyManager.spawnBoss(true, this.playerX, this.playerY);
        }
      }
      if (this.enemyManager.activeBoss) {
        this.ui.updateBossHPBar(this.enemyManager.activeBoss);
        const bossGlow = (_a = this.enemyManager.activeBoss.container) == null ? void 0 : _a.children[0];
        if (bossGlow) bossGlow.alpha = 0.15 + Math.sin(this.timeElapsed * 4) * 0.1;
      }
      if (this.invulnTimer > 0) {
        this.invulnTimer -= dt;
        if (this.player) this.player.alpha = Math.floor(this.timeElapsed / 0.08) % 2 === 0 ? 0.4 : 1;
      } else if (this.player) {
        this.player.alpha = 1;
      }
      this.input.setCamera(this.camX, this.camY);
      const { dx, dy } = this.input.getMovement(dt);
      this._movePlayer(dt, dx, dy);
      this._updateCamera(dt);
      if (!this.enemyManager.activeBoss) {
        this.enemyManager.updateSpawning(dt, this.timeElapsed, this.camX, this.camY);
      }
      this.enemyManager.updateMovement(dt, this.playerX, this.playerY, this.camX, this.camY);
      this.enemyManager.rebuildGrid();
      this.weaponSystem.update(dt, this.playerX, this.playerY, this.stats);
      this.projectiles.update(dt, this.stats.coinDropMult, this.playerX, this.playerY);
      const hit = this.enemyManager.checkPlayerCollision(this.playerX, this.playerY, this.invulnTimer > 0);
      if (hit) {
        const dmgReduced = hit.damage * (1 - this.stats.armor);
        this.stats.playerHP -= dmgReduced;
        this.invulnTimer = INVULN_DURATION;
        this.audio.playSFX("hit");
        const kbDist = 40;
        this.playerX += Math.cos(hit.knockbackAngle) * kbDist;
        this.playerY += Math.sin(hit.knockbackAngle) * kbDist;
        this._velX *= 0.3;
        this._velY *= 0.3;
        this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
        this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
        this.player.position.set(this.playerX, this.playerY);
        if (this.stats.playerHP <= 0) {
          this.stats.playerHP = 0;
          this.alive = false;
          this._onDeath();
          return;
        }
      }
      this._updateXPOrbs(dt);
      this.particles.update(dt);
      this.ui.update(
        this.timeRemaining,
        this.timerDuration,
        this.stats.playerHP,
        this.stats.maxHP,
        this.enemyManager.enemiesKilled,
        this.upgradeSystem.xpOrbsCollected,
        this.stats,
        this.weaponSystem,
        this.upgradeSystem
      );
      this.ui.updateMinimap(
        this.playerX,
        this.playerY,
        this.camX,
        this.camY,
        this.vpWidth,
        this.vpHeight,
        this.mapWidth,
        this.mapHeight,
        this.enemyManager.enemies
      );
      if (this.playerGlow) {
        this.playerGlow.alpha = 0.1 + Math.sin(this.timeElapsed * 3) * 0.08;
      }
    }
    // ─── Player Movement ───────────────────────────────────────
    _movePlayer(dt, dx, dy) {
      const targetVX = dx * this.stats.playerSpeed;
      const targetVY = dy * this.stats.playerSpeed;
      const hasInput = dx !== 0 || dy !== 0;
      const speed = hasInput ? 20 : 12;
      const f = 1 - Math.exp(-speed * dt);
      this._velX += (targetVX - this._velX) * f;
      this._velY += (targetVY - this._velY) * f;
      if (!hasInput && Math.abs(this._velX) < 0.5 && Math.abs(this._velY) < 0.5) {
        this._velX = 0;
        this._velY = 0;
      }
      this.playerX += this._velX * dt;
      this.playerY += this._velY * dt;
      this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
      this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
      this.player.position.set(this.playerX, this.playerY);
      if (this.playerArrow && (this._velX !== 0 || this._velY !== 0)) {
        this.playerArrow.rotation = Math.atan2(this._velY, this._velX);
      }
    }
    _updateCamera(dt) {
      const targetCamX = this.playerX - this.vpWidth / 2;
      const targetCamY = this.playerY - this.vpHeight / 2;
      const f = 1 - Math.exp(-8 * dt);
      this.camX += (targetCamX - this.camX) * f;
      this.camY += (targetCamY - this.camY) * f;
      this.camX = clamp(this.camX, 0, this.mapWidth - this.vpWidth);
      this.camY = clamp(this.camY, 0, this.mapHeight - this.vpHeight);
      this.worldContainer.position.set(-this.camX, -this.camY);
    }
    // ─── XP Orbs ────────────────────────────────────────────────
    _spawnXPOrb(x, y) {
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
      gfx.position.set(x, y);
      this.worldContainer.addChild(gfx);
      this.xpOrbs.push({ gfx, x, y, lifetime: 8 });
    }
    _updateXPOrbs(dt) {
      for (let i = this.xpOrbs.length - 1; i >= 0; i--) {
        const orb = this.xpOrbs[i];
        orb.lifetime -= dt;
        const ddx = this.playerX - orb.x;
        const ddy = this.playerY - orb.y;
        const d = Math.sqrt(ddx * ddx + ddy * ddy);
        if (d < this.stats.magnetRadius) {
          const speed = XP_ORB_SPEED * (1 - d / this.stats.magnetRadius);
          orb.x += ddx / d * speed * dt;
          orb.y += ddy / d * speed * dt;
          orb.gfx.position.set(orb.x, orb.y);
          if (d < PLAYER_RADIUS) {
            orb.gfx.visible = false;
            if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx);
            this._orbPool.release(orb.gfx);
            this.xpOrbs.splice(i, 1);
            this.upgradeSystem.collectOrb();
            continue;
          }
        }
        if (orb.lifetime <= 0) {
          orb.gfx.visible = false;
          if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx);
          this._orbPool.release(orb.gfx);
          this.xpOrbs.splice(i, 1);
          continue;
        }
        if (orb.lifetime < 2) orb.gfx.alpha = orb.lifetime / 2;
      }
    }
    // ─── End-Game ───────────────────────────────────────────────
    _onWin() {
      gameActive = false;
      const title = this.enemyManager.finalBossSpawned ? "BOSS DEFEATED!" : "SURVIVED!";
      this.ui.showMessage(title, 4431943, `Level ${this.level} Complete!`);
      this.audio.playSFX("win");
      setTimeout(() => {
        if (dotNetRef) {
          try {
            dotNetRef.invokeMethodAsync(
              "OnLevelComplete",
              this.enemyManager.enemiesKilled,
              this.timeElapsed,
              this.upgradeSystem.xpOrbsCollected
            );
          } catch (e) {
            console.error("Failed to invoke OnLevelComplete:", e);
          }
        }
      }, 2e3);
    }
    _onDeath() {
      gameActive = false;
      this.particles.spawnBurst(this.playerX, this.playerY, 5227511);
      if (this.player) this.player.alpha = 0.3;
      this.ui.showMessage("SURVIVAL ENDED", 15022389, `Survived ${formatTime(this.timeElapsed)}`);
      this.audio.playSFX("death");
      setTimeout(() => {
        if (dotNetRef) {
          try {
            dotNetRef.invokeMethodAsync(
              "OnPlayerDeath",
              this.enemyManager.enemiesKilled,
              this.timeElapsed,
              this.upgradeSystem.xpOrbsCollected
            );
          } catch (e) {
            console.error("Failed to invoke OnPlayerDeath:", e);
          }
        }
      }, 2e3);
    }
    // ─── Cleanup / Destroy ──────────────────────────────────────
    cleanup() {
      var _a, _b, _c;
      if (app == null ? void 0 : app.ticker) {
        try {
          app.ticker.remove(this._update, this);
        } catch {
        }
      }
      (_a = this.input) == null ? void 0 : _a.cleanup();
      (_b = this.weaponSystem) == null ? void 0 : _b.cleanup();
      (_c = this.projectiles) == null ? void 0 : _c.cleanup();
      for (const orb of this.xpOrbs) {
        if (orb.gfx) {
          orb.gfx.visible = false;
          if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx);
        }
      }
      this.xpOrbs = [];
      this._orbPool = null;
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
    }
    destroy() {
      var _a;
      this.cleanup();
      (_a = this.audio) == null ? void 0 : _a.destroy();
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
