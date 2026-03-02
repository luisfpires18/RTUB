var __defProp = Object.defineProperty;
var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
var __publicField = (obj, key, value) => __defNormalProp(obj, typeof key !== "symbol" ? key + "" : key, value);
(function() {
  "use strict";
  const SESSION_CACHE_BUST = "";
  const loadedAssetAliases = /* @__PURE__ */ new Set();
  const audioBufferCache = {};
  const audioCacheBuster = "";
  function getEventField(evt, field) {
    if (!evt) return void 0;
    const record = evt;
    return record[field] ?? record[field[0].toLowerCase() + field.slice(1)] ?? record[field.toLowerCase()];
  }
  function formatNum(n) {
    if (n == null) return "0";
    const abs = Math.abs(n);
    const sign = n < 0 ? "-" : "";
    if (abs >= 1e12)
      return sign + (abs / 1e12).toFixed(abs % 1e12 === 0 ? 0 : 2).replace(/\.?0+$/, "") + "T";
    if (abs >= 1e9)
      return sign + (abs / 1e9).toFixed(abs % 1e9 === 0 ? 0 : 2).replace(/\.?0+$/, "") + "B";
    if (abs >= 1e6)
      return sign + (abs / 1e6).toFixed(abs % 1e6 === 0 ? 0 : 2).replace(/\.?0+$/, "") + "M";
    if (abs >= 1e3)
      return sign + (abs / 1e3).toFixed(abs % 1e3 === 0 ? 0 : 2).replace(/\.?0+$/, "") + "K";
    return sign + Math.round(abs).toString();
  }
  function resolveEvents(battleData) {
    if (!battleData) return [];
    const data = battleData;
    const eventsJson = data.EventsJson ?? data.eventsJson ?? data.eventsjson;
    if (eventsJson && typeof eventsJson === "string") {
      try {
        const parsed = JSON.parse(eventsJson);
        if (Array.isArray(parsed)) return parsed;
        if (parsed && typeof parsed === "object") {
          const obj = parsed;
          if (Array.isArray(obj.Events)) return obj.Events;
        }
        return [];
      } catch (e) {
        console.error("Failed to parse EventsJson:", e);
        return [];
      }
    }
    if (Array.isArray(battleData)) return battleData;
    return data.events ?? data.Events ?? [];
  }
  function pick(data, pascal, camel, fallback) {
    if (!data) return fallback;
    return data[pascal] ?? data[camel] ?? fallback;
  }
  let sharedAudioContext = null;
  function getSharedAudioContext() {
    if (!sharedAudioContext || sharedAudioContext.state === "closed") {
      try {
        const Ctor = window.AudioContext || window.webkitAudioContext;
        sharedAudioContext = new Ctor();
      } catch (e) {
        console.warn("AudioContext not available:", e);
        return null;
      }
    }
    return sharedAudioContext;
  }
  function playSound(ctx, type, sfxVolume) {
    if (!sfxVolume) return;
    if (ctx.state === "suspended") ctx.resume();
    const t = ctx.currentTime;
    const oscillator = ctx.createOscillator();
    const gainNode = ctx.createGain();
    oscillator.connect(gainNode);
    gainNode.connect(ctx.destination);
    switch (type) {
      case "attack":
        oscillator.frequency.value = 200;
        oscillator.type = "square";
        gainNode.gain.setValueAtTime(sfxVolume * 0.3, t);
        gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.1);
        oscillator.start(t);
        oscillator.stop(t + 0.1);
        break;
      case "hit":
        oscillator.frequency.value = 150;
        oscillator.type = "sawtooth";
        gainNode.gain.setValueAtTime(sfxVolume * 0.4, t);
        gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.15);
        oscillator.start(t);
        oscillator.stop(t + 0.15);
        break;
      case "critical": {
        oscillator.frequency.value = 400;
        oscillator.type = "sine";
        gainNode.gain.setValueAtTime(sfxVolume * 0.5, t);
        gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.2);
        const osc2 = ctx.createOscillator();
        const gain2 = ctx.createGain();
        osc2.connect(gain2);
        gain2.connect(ctx.destination);
        osc2.frequency.value = 600;
        osc2.type = "sine";
        gain2.gain.setValueAtTime(sfxVolume * 0.3, t + 0.05);
        gain2.gain.exponentialRampToValueAtTime(0.01, t + 0.25);
        oscillator.start(t);
        oscillator.stop(t + 0.2);
        osc2.start(t + 0.05);
        osc2.stop(t + 0.25);
        break;
      }
      case "ko":
        oscillator.frequency.setValueAtTime(300, t);
        oscillator.frequency.exponentialRampToValueAtTime(50, t + 0.5);
        oscillator.type = "triangle";
        gainNode.gain.setValueAtTime(sfxVolume * 0.6, t);
        gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.5);
        oscillator.start(t);
        oscillator.stop(t + 0.5);
        break;
      case "victory": {
        const notes = [262, 330, 392, 523];
        notes.forEach((freq, i) => {
          const osc = ctx.createOscillator();
          const gain = ctx.createGain();
          osc.connect(gain);
          gain.connect(ctx.destination);
          osc.frequency.value = freq;
          osc.type = "sine";
          const startTime = t + i * 0.15;
          gain.gain.setValueAtTime(sfxVolume * 0.4, startTime);
          gain.gain.exponentialRampToValueAtTime(0.01, startTime + 0.3);
          osc.start(startTime);
          osc.stop(startTime + 0.3);
        });
        break;
      }
      case "defeat":
        oscillator.frequency.setValueAtTime(200, t);
        oscillator.frequency.exponentialRampToValueAtTime(80, t + 0.5);
        oscillator.type = "sawtooth";
        gainNode.gain.setValueAtTime(sfxVolume * 0.5, t);
        gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.5);
        oscillator.start(t);
        oscillator.stop(t + 0.5);
        break;
      case "block":
        oscillator.frequency.value = 150;
        oscillator.type = "triangle";
        gainNode.gain.setValueAtTime(sfxVolume * 0.4, t);
        gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.15);
        oscillator.start(t);
        oscillator.stop(t + 0.15);
        break;
    }
  }
  async function loadBackgroundMusic(ctx, musicUrl, volume, audioEnabled) {
    const state = { source: null, gainNode: null, currentTrack: musicUrl };
    try {
      const fullUrl = musicUrl + audioCacheBuster;
      let audioBuffer = audioBufferCache[fullUrl];
      if (!audioBuffer) {
        const response = await fetch(fullUrl);
        const arrayBuffer = await response.arrayBuffer();
        audioBuffer = await ctx.decodeAudioData(arrayBuffer);
        audioBufferCache[fullUrl] = audioBuffer;
      }
      state.gainNode = ctx.createGain();
      state.gainNode.connect(ctx.destination);
      state.gainNode.gain.value = audioEnabled ? volume : 0;
      state.source = ctx.createBufferSource();
      state.source.buffer = audioBuffer;
      state.source.loop = true;
      state.source.connect(state.gainNode);
      state.source.start(0);
    } catch (e) {
      console.warn("Could not load background music:", e);
    }
    return state;
  }
  function stopMusic(state) {
    if (!(state == null ? void 0 : state.source)) return;
    try {
      state.source.stop();
    } catch {
    }
    state.source = null;
    state.gainNode = null;
    state.currentTrack = null;
  }
  function setMusicVolume(state, volume, enabled) {
    if (state == null ? void 0 : state.gainNode) {
      state.gainNode.gain.value = enabled ? volume : 0;
    }
  }
  function animateTo(owner, target, properties, duration, onComplete) {
    var _a;
    if (!target || target.destroyed) {
      onComplete == null ? void 0 : onComplete();
      return;
    }
    const speed = owner.battleSpeed ?? 1;
    const adjustedDuration = Math.max(20, speed > 0 ? duration / speed : duration);
    const startTime = Date.now();
    const container = target;
    const startValues = {};
    for (const key of Object.keys(properties)) {
      if (key === "scale") {
        startValues[key] = ((_a = target.scale) == null ? void 0 : _a.x) ?? 1;
      } else {
        startValues[key] = container[key] ?? 0;
      }
    }
    const animate = () => {
      var _a2;
      const elapsed = Date.now() - startTime;
      const t = Math.min(elapsed / adjustedDuration, 1);
      try {
        for (const key of Object.keys(properties)) {
          const from = startValues[key];
          const to = properties[key];
          const value = from + (to - from) * t;
          if (key === "scale") {
            (_a2 = target.scale) == null ? void 0 : _a2.set(value);
          } else if (key === "width" || key === "height") {
            container[key] = value;
          } else {
            container[key] = value;
          }
        }
      } catch {
        onComplete == null ? void 0 : onComplete();
        return;
      }
      if (t < 1) {
        const id2 = requestAnimationFrame(animate);
        owner._rafIds.push(id2);
      } else {
        onComplete == null ? void 0 : onComplete();
      }
    };
    const id = requestAnimationFrame(animate);
    owner._rafIds.push(id);
  }
  const MAX_POOL_SIZE = 20;
  function getPooledText(textPool, text, style) {
    try {
      let t = textPool.pool.pop();
      if (t) {
        if (t.destroyed) {
          t = void 0;
        }
      }
      if (t) {
        t.text = text;
        if (t.style) {
          Object.assign(t.style, style);
        }
        t.alpha = 1;
        t.scale.set(1);
        t.visible = true;
      } else {
        t = new PIXI.Text({ text, style });
      }
      return t;
    } catch {
      try {
        return new PIXI.Text({ text, style });
      } catch {
        return null;
      }
    }
  }
  function releaseText(textPool, t) {
    if (!t) return;
    t.visible = false;
    if (t.parent) t.parent.removeChild(t);
    if (textPool.pool.length < MAX_POOL_SIZE) {
      textPool.pool.push(t);
    } else {
      try {
        t.destroy();
      } catch {
      }
    }
  }
  function destroyTextPool(textPool) {
    for (const t of textPool.pool) {
      try {
        t.destroy();
      } catch {
      }
    }
    textPool.pool = [];
  }
  const MIN_FLOAT_MS = 350;
  function showFloatingText(owner, text, x, y, color) {
    if (!owner.stage || !owner.app) return;
    const floatText = getPooledText(owner._textPool, text, {
      fontFamily: "Arial",
      fontSize: 26,
      fontWeight: "bold",
      fill: color,
      stroke: { color: 0, width: 4 }
    });
    if (!floatText) return;
    floatText.anchor.set(0.5);
    floatText.x = x;
    floatText.y = y;
    owner.stage.addChild(floatText);
    const speed = owner.battleSpeed || 1;
    const compensated = Math.max(900, MIN_FLOAT_MS * speed);
    animateTo(owner, floatText, { y: floatText.y - 70, alpha: 0 }, compensated, () => {
      releaseText(owner._textPool, floatText);
    });
  }
  function showDamageText(owner, damage, isCritical, x, y) {
    if (!owner.stage || !owner.app) return;
    const text = isCritical ? `CRIT! -${formatNum(Math.abs(damage))}` : `-${formatNum(Math.abs(damage))}`;
    const fontSize = isCritical ? 28 : 24;
    const fillColor = isCritical ? 16776960 : 16729156;
    const strokeWidth = isCritical ? 4 : 3;
    const floatDistance = isCritical ? 80 : 60;
    const duration = isCritical ? 1e3 : 800;
    const damageText = getPooledText(owner._textPool, text, {
      fontFamily: "Arial",
      fontSize,
      fontWeight: "bold",
      fill: fillColor,
      stroke: { color: 0, width: strokeWidth }
    });
    if (!damageText) return;
    damageText.anchor.set(0.5);
    damageText.x = x + (Math.random() - 0.5) * 30;
    damageText.y = y;
    owner.stage.addChild(damageText);
    const speed = owner.battleSpeed || 1;
    const compensated = Math.max(duration, MIN_FLOAT_MS * speed);
    animateTo(owner, damageText, { y: damageText.y - floatDistance, alpha: 0 }, compensated, () => {
      releaseText(owner._textPool, damageText);
    });
  }
  function playBuffVfx(owner, target, color) {
    if (!target || !owner.stage) return;
    const cx = target.x;
    const cy = target.y - (target.height || 40) / 2;
    for (let i = 0; i < 8; i++) {
      const p = new PIXI.Graphics();
      p.circle(0, 0, 3);
      p.fill({ color, alpha: 0.8 });
      p.x = cx + (Math.random() - 0.5) * 30;
      p.y = cy + (Math.random() - 0.5) * 20;
      owner.stage.addChild(p);
      animateTo(
        owner,
        p,
        { y: p.y - 40 - Math.random() * 30, alpha: 0 },
        600 + Math.random() * 200,
        () => {
          if (p.parent) p.parent.removeChild(p);
          p.destroy();
        }
      );
    }
  }
  const DEFAULT_WIDTH = 800;
  const DEFAULT_HEIGHT = 500;
  const DEFAULT_EVENT_INTERVAL = 400;
  const DEFAULT_SPRITES = {
    player: "/sprites/games/my-tuno/default_tuno.png",
    background: "/sprites/games/my-tuno/backgrounds/forest.png",
    enemies: {
      normal: "/sprites/games/my-tuno/enemies/forest/monkey.png",
      boss: "/sprites/games/my-tuno/enemies/forest/boss_1_bear.png"
    }
  };
  const DEFAULT_CONSUMABLE_IMAGES = {
    fino: "/images/consumables/fino.svg",
    caneca: "/images/consumables/caneca.svg",
    cigarro: "/images/consumables/cigarro.svg",
    canhao: "/images/consumables/canhao.svg"
  };
  let globalAudioEnabled = false;
  let globalSfxVolume = 0.5;
  let musicState = null;
  const _StageBattleScene = class _StageBattleScene {
    /* ────────────────────────── Constructor ────────────────────────── */
    constructor(container, data) {
      // PIXI application
      __publicField(this, "app", null);
      __publicField(this, "stage", null);
      // Container & config
      __publicField(this, "container");
      __publicField(this, "eventInterval");
      // Events
      __publicField(this, "eventsList");
      __publicField(this, "dotNetRef");
      // Stage info
      __publicField(this, "stageNumber");
      __publicField(this, "enemyType");
      __publicField(this, "enemyCount");
      __publicField(this, "playerName");
      __publicField(this, "enemyName");
      __publicField(this, "backgroundPath");
      __publicField(this, "playerSpritePath");
      __publicField(this, "enemySpritePaths");
      __publicField(this, "enemyPlacements");
      // Layout
      __publicField(this, "isMobile", false);
      __publicField(this, "playerX", 0);
      __publicField(this, "playerDisplayHeight", 0);
      // HP tracking
      __publicField(this, "playerMaxHp", 100);
      __publicField(this, "playerCurrentHp", 100);
      __publicField(this, "enemyMaxHp", 100);
      __publicField(this, "enemyCurrentHp", 100);
      __publicField(this, "enemyHPs", []);
      __publicField(this, "isInitialSetup", true);
      // Sprites
      __publicField(this, "playerSprite", null);
      __publicField(this, "enemySprites", []);
      __publicField(this, "backgroundSprite", null);
      __publicField(this, "_overlay", null);
      __publicField(this, "_ground", null);
      // HP / Speed bars
      __publicField(this, "playerHpBar", null);
      __publicField(this, "enemyHpBars", []);
      __publicField(this, "bossHpBar", null);
      __publicField(this, "playerSpeedBar", null);
      __publicField(this, "enemySpeedBars", []);
      __publicField(this, "bossSpeedBar", null);
      // Speed bar timers
      __publicField(this, "playerActionTime", 3.5);
      __publicField(this, "enemyActionTimes", []);
      __publicField(this, "playerSpeedBarTimer", 3500);
      __publicField(this, "enemySpeedBarTimers", []);
      __publicField(this, "battleStartTime", 0);
      __publicField(this, "currentSimTime", 0);
      // Battle speed (anti-exploit) – server-provided allowlist; defaults to [1, 5]
      __publicField(this, "_allowedSpeeds", [1, 5]);
      __publicField(this, "_battleSpeed", 1);
      // Playback
      __publicField(this, "playbackSpeed", 1);
      __publicField(this, "currentEventIndex", 0);
      __publicField(this, "isPlaying", false);
      __publicField(this, "battleFinished", false);
      __publicField(this, "eventTimer", null);
      // Audio
      __publicField(this, "audioContext", null);
      __publicField(this, "audioEnabled");
      __publicField(this, "sfxVolume");
      // Idle animation
      __publicField(this, "idleAnimationTime", 0);
      __publicField(this, "enemyIdleOffsets", []);
      __publicField(this, "playerIdleOffset", null);
      // Attack animation flags
      __publicField(this, "_playerAttacking", false);
      __publicField(this, "_enemyAttacking", {});
      // Pending flash timeout IDs — tracked per entity to prevent overlapping
      // flash restores from resetting tint too early when attacks overlap.
      __publicField(this, "_playerFlashTimeout", null);
      __publicField(this, "_enemyFlashTimeouts", {});
      // Shot / penalty buff visual
      __publicField(this, "hasShotBuff");
      __publicField(this, "hasPenaltyBuff");
      __publicField(this, "playerAura", null);
      // Canhao / Penalty timer bars (bottom-left corner)
      __publicField(this, "canhaoTimerBar", null);
      __publicField(this, "penaltyTimerBar", null);
      __publicField(this, "canhaoBuffExpiresAt", null);
      __publicField(this, "penaltyBuffExpiresAt", null);
      __publicField(this, "canhaoBuffDurationMs", 2 * 60 * 1e3);
      __publicField(this, "penaltyBuffDurationMs", 2 * 60 * 1e3);
      // Asset aliases
      __publicField(this, "bgAlias", "");
      __publicField(this, "_playerAlias", "");
      __publicField(this, "enemySpriteAliases", []);
      // Interactive mode
      __publicField(this, "interactiveMode");
      __publicField(this, "interactivePlayerHP");
      __publicField(this, "interactivePlayerMaxHP");
      __publicField(this, "interactivePlayerActionTime");
      __publicField(this, "interactiveEnemies");
      __publicField(this, "_pendingPlayerAttacks", 0);
      __publicField(this, "_pendingEnemyAttacks", []);
      __publicField(this, "_consumableTickAccum", 0);
      // Consumable bar UI
      __publicField(this, "consumableBarContainer", null);
      __publicField(this, "consumableButtons", []);
      __publicField(this, "_consumablePending", false);
      __publicField(this, "consumableQuantities");
      __publicField(this, "activeBuffs");
      __publicField(this, "consumableImages");
      __publicField(this, "consumableCooldowns");
      // Cleanup trackers (VfxOwner requirement)
      __publicField(this, "_timeoutIds", []);
      __publicField(this, "_rafIds", []);
      __publicField(this, "_textPool", { pool: [] });
      // Destroyed flag — prevents async callbacks from running after destroy
      __publicField(this, "_destroyed", false);
      // JS-side heartbeat interval — pings Blazor every 30s to reset the server watchdog
      __publicField(this, "_heartbeatInterval", null);
      // JS-side battle watchdog — if a single battle exceeds this, force-finish it
      __publicField(this, "_jsBattleWatchdogId", null);
      // 2 minutes
      // WebGL context loss recovery timer
      __publicField(this, "_contextLossTimerId", null);
      // finishBattle retry state
      __publicField(this, "_finishRetryCount", 0);
      // Event listeners
      __publicField(this, "_onContextLost", null);
      __publicField(this, "_onContextRestored", null);
      __publicField(this, "_onResize", null);
      this.container = container;
      this.eventsList = data.events ?? [];
      this.dotNetRef = data.dotNetRef ?? null;
      this.eventInterval = data.eventInterval ?? DEFAULT_EVENT_INTERVAL;
      this.currentEventIndex = 0;
      this.stageNumber = pick(data, "StageNumber", "stageNumber", 1);
      this.enemyType = pick(data, "EnemyType", "enemyType", "normal");
      this.enemyCount = pick(data, "EnemyCount", "enemyCount", 1);
      this.playerName = pick(data, "PlayerName", "playerName", "Player");
      this.enemyName = pick(data, "EnemyName", "enemyName", "Enemy");
      this.backgroundPath = pick(data, "BackgroundPath", "backgroundPath", DEFAULT_SPRITES.background);
      this.playerSpritePath = pick(data, "PlayerSpritePath", "playerSpritePath", DEFAULT_SPRITES.player);
      const enemySpriteArr = data.EnemySprites ?? data.enemySprites;
      if (enemySpriteArr && Array.isArray(enemySpriteArr)) {
        this.enemySpritePaths = enemySpriteArr;
      } else {
        const singlePath = pick(data, "EnemySpritePath", "enemySpritePath", "") || DEFAULT_SPRITES.enemies[this.enemyType] || DEFAULT_SPRITES.enemies.normal;
        this.enemySpritePaths = Array(this.enemyCount).fill(singlePath);
      }
      const placementsArr = data.EnemyPlacements ?? data.enemyPlacements;
      this.enemyPlacements = Array.isArray(placementsArr) ? placementsArr : Array(this.enemyCount).fill(0);
      this.enemyHPs = Array.from({ length: this.enemyCount }, () => ({ current: 100, max: 100 }));
      this.enemyActionTimes = Array(this.enemyCount).fill(3.5);
      this.enemySpeedBarTimers = Array(this.enemyCount).fill(3500);
      const serverSpeeds = data.AllowedSpeeds ?? data.allowedSpeeds;
      if (Array.isArray(serverSpeeds) && serverSpeeds.length > 0) {
        this._allowedSpeeds = serverSpeeds.filter((s) => typeof s === "number");
      }
      this._enemyAttacking = {};
      this.audioEnabled = globalAudioEnabled;
      this.sfxVolume = globalSfxVolume;
      this.hasShotBuff = pick(data, "HasShotBuff", "hasShotBuff", false);
      this.hasPenaltyBuff = pick(data, "HasPenaltyBuff", "hasPenaltyBuff", false);
      this.interactiveMode = pick(data, "InteractiveMode", "interactiveMode", false);
      this.interactivePlayerHP = data.PlayerHP ?? data.playerHP ?? null;
      this.interactivePlayerMaxHP = data.PlayerMaxHP ?? data.playerMaxHP ?? null;
      this.interactivePlayerActionTime = data.PlayerActionTime ?? data.playerActionTime ?? null;
      this.interactiveEnemies = data.Enemies ?? data.enemies ?? [];
      this._pendingEnemyAttacks = Array(this.enemyCount).fill(0);
      const cData = data.consumables ?? data.Consumables ?? {};
      this.consumableQuantities = {
        fino: pick(cData, "Fino", "fino", 0),
        caneca: pick(cData, "Caneca", "caneca", 0),
        cigarro: pick(cData, "Cigarro", "cigarro", 0),
        canhao: pick(cData, "Canhao", "canhao", 0),
        shot: pick(cData, "Shot", "shot", 0),
        penalty: pick(cData, "Penalty", "penalty", 0)
      };
      const abData = data.activeBuffs ?? data.ActiveBuffs ?? {};
      this.activeBuffs = {
        cigarro: !!pick(abData, "Cigarro", "cigarro", false),
        canhao: !!pick(abData, "Canhao", "canhao", false),
        shot: !!pick(abData, "Shot", "shot", false),
        penalty: !!pick(abData, "Penalty", "penalty", false)
      };
      const canhaoUtc = data.canhaoBuffExpiresAtUtc ?? data.CanhaoBuffExpiresAtUtc ?? null;
      this.canhaoBuffExpiresAt = canhaoUtc ? new Date(canhaoUtc).getTime() : null;
      const penaltyUtc = data.penaltyBuffExpiresAtUtc ?? data.PenaltyBuffExpiresAtUtc ?? null;
      this.penaltyBuffExpiresAt = penaltyUtc ? new Date(penaltyUtc).getTime() : null;
      const ciData = data.consumableImages ?? data.ConsumableImages ?? {};
      this.consumableImages = {
        fino: pick(ciData, "Fino", "fino", DEFAULT_CONSUMABLE_IMAGES.fino),
        caneca: pick(ciData, "Caneca", "caneca", DEFAULT_CONSUMABLE_IMAGES.caneca),
        cigarro: pick(ciData, "Cigarro", "cigarro", DEFAULT_CONSUMABLE_IMAGES.cigarro),
        canhao: pick(ciData, "Canhao", "canhao", DEFAULT_CONSUMABLE_IMAGES.canhao)
      };
      const ccData = data.consumableCooldowns ?? data.ConsumableCooldowns ?? {};
      this.consumableCooldowns = {
        fino: pick(ccData, "Fino", "fino", 0),
        caneca: pick(ccData, "Caneca", "caneca", 0),
        cigarro: pick(ccData, "Cigarro", "cigarro", 0),
        canhao: pick(ccData, "Canhao", "canhao", 0),
        shot: pick(ccData, "Shot", "shot", 0),
        penalty: pick(ccData, "Penalty", "penalty", 0)
      };
      this.setupAudio();
      this.initPixi();
    }
    get battleSpeed() {
      return this._battleSpeed;
    }
    set battleSpeed(v) {
      this._battleSpeed = this._allowedSpeeds.includes(v) ? v : 1;
    }
    get allowedSpeeds() {
      return this._allowedSpeeds;
    }
    /* ────────────────────────── Audio Setup ────────────────────────── */
    setupAudio() {
      this.audioContext = getSharedAudioContext();
      if (this.audioContext) {
        if (this.audioContext.state === "suspended") {
          this.audioContext.resume().catch(() => {
          });
        }
        if (!(musicState == null ? void 0 : musicState.source)) {
          this.loadBgMusic();
        }
      } else {
        this.audioEnabled = false;
      }
    }
    async loadBgMusic() {
      var _a;
      if (!this.audioContext) return;
      const isBoss = ((_a = this.enemyType) == null ? void 0 : _a.toLowerCase()) === "boss";
      const musicFile = isBoss ? "/sound/boss_battle.mp3" : "/sound/stage_battle.mp3";
      musicState = await loadBackgroundMusic(
        this.audioContext,
        musicFile,
        0.3,
        this.audioEnabled
      );
    }
    _playSound(type) {
      if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
      playSound(this.audioContext, type, this.sfxVolume);
    }
    /* ────────────────────────── PixiJS Init ────────────────────────── */
    async initPixi() {
      while (this.container.firstChild) {
        this.container.removeChild(this.container.firstChild);
      }
      const containerW = this.container.clientWidth || DEFAULT_WIDTH;
      const containerH = this.container.clientHeight || DEFAULT_HEIGHT;
      this.app = new PIXI.Application();
      await this.app.init({
        width: containerW,
        height: containerH,
        backgroundColor: 1710618,
        antialias: true,
        resizeTo: this.container
      });
      this.container.appendChild(this.app.canvas);
      this.stage = this.app.stage;
      delete globalThis.__PIXI_APP__;
      delete globalThis.__PIXI_STAGE__;
      try {
        Object.defineProperty(this.app.ticker, "speed", {
          value: 1,
          writable: false,
          configurable: false
        });
      } catch {
      }
      this._onContextLost = (e) => {
        console.warn("WebGL context lost — waiting for restore");
        e.preventDefault();
        if (this._contextLossTimerId != null) clearTimeout(this._contextLossTimerId);
        this._contextLossTimerId = setTimeout(() => {
          if (!this._destroyed && !this.battleFinished) {
            console.warn("WebGL context not restored within timeout — forcing battle finish");
            this.finishBattle();
          }
        }, _StageBattleScene.CONTEXT_LOSS_RECOVERY_MS);
      };
      this.app.canvas.addEventListener("webglcontextlost", this._onContextLost);
      this._onContextRestored = () => {
        console.log("WebGL context restored");
        if (this._contextLossTimerId != null) {
          clearTimeout(this._contextLossTimerId);
          this._contextLossTimerId = null;
        }
      };
      this.app.canvas.addEventListener("webglcontextrestored", this._onContextRestored);
      this._onResize = () => {
      };
      window.addEventListener("resize", this._onResize);
      await this.loadAssets();
      this.create();
    }
    /* ────────────────────────── Asset Loading ──────────────────────── */
    async loadAssets() {
      this.bgAlias = `bg_${this.backgroundPath}`;
      this._playerAlias = `player_${this.playerSpritePath}`;
      const tryLoad = async (alias, src, fallbackAlias) => {
        if (loadedAssetAliases.has(alias)) return;
        try {
          await PIXI.Assets.load({ alias, src: src + SESSION_CACHE_BUST });
          loadedAssetAliases.add(alias);
        } catch (e) {
          console.warn(`Sprite 404, using fallback: ${src}`, e.message);
          if (fallbackAlias && loadedAssetAliases.has(fallbackAlias)) {
            try {
              const fallbackTex = PIXI.Assets.get(fallbackAlias);
              if (fallbackTex) PIXI.Assets.cache.set(alias, fallbackTex);
              loadedAssetAliases.add(alias);
            } catch {
            }
          }
        }
      };
      const defaultBgAlias = `bg_${DEFAULT_SPRITES.background}`;
      const defaultPlayerAlias = `player_${DEFAULT_SPRITES.player}`;
      await tryLoad(defaultBgAlias, DEFAULT_SPRITES.background, null);
      await tryLoad(defaultPlayerAlias, DEFAULT_SPRITES.player, null);
      await tryLoad(this.bgAlias, this.backgroundPath, defaultBgAlias);
      await tryLoad(this._playerAlias, this.playerSpritePath, defaultPlayerAlias);
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
    create() {
      if (!this.app || !this.stage) return;
      const { width, height } = this.app.screen;
      this.isMobile = width <= height || width < 500;
      this.backgroundSprite = PIXI.Sprite.from(this.bgAlias);
      this.backgroundSprite.width = width;
      this.backgroundSprite.height = height;
      this.backgroundSprite.x = width / 2;
      this.backgroundSprite.y = height / 2;
      this.backgroundSprite.anchor.set(0.5);
      this.stage.addChild(this.backgroundSprite);
      const overlay = new PIXI.Graphics();
      overlay.rect(0, 0, width, height);
      overlay.fill({ color: 0, alpha: 0.3 });
      this.stage.addChild(overlay);
      this._overlay = overlay;
      this.createPlayer(width, height);
      this.createEnemies(width, height);
      this.createHudBars(width, height);
      if (this.interactiveMode) {
        this.initInteractiveState();
        this.createConsumableBar();
        this.startInteractiveBattle();
      } else {
        this.preprocessInitialEvents();
        this.startTimedBattle();
      }
      this.app.ticker.add(() => this.update());
    }
    /* ────────────────────────── Pre-processing ─────────────────────── */
    preprocessInitialEvents() {
      let firstPlayerAttackIndex = -1;
      for (let i = 0; i < this.eventsList.length; i++) {
        const evt = this.eventsList[i];
        const evtType = getEventField(evt, "Type");
        const attacker = getEventField(evt, "Attacker");
        if (evtType === "Attack" && (attacker === "Attacker" || attacker === "Player")) {
          firstPlayerAttackIndex = i;
          break;
        }
      }
      for (let i = 0; i < this.eventsList.length && i < firstPlayerAttackIndex; i++) {
        const evt = this.eventsList[i];
        const evtType = getEventField(evt, "Type");
        const maxHP = getEventField(evt, "MaxHP");
        if (evtType === "HPUpdate" && maxHP) {
          this.processInitialHPEvent(evt);
        }
      }
      this.currentEventIndex = firstPlayerAttackIndex >= 0 ? firstPlayerAttackIndex : 0;
      this.isInitialSetup = false;
      this.playerSpeedBarTimer = this.playerActionTime * 1e3;
      for (let i = 0; i < this.enemyCount; i++) {
        this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1e3;
      }
    }
    processInitialHPEvent(evt) {
      const character = getEventField(evt, "Character") ?? "";
      const hp = getEventField(evt, "HP") ?? 0;
      const maxHP = getEventField(evt, "MaxHP") ?? 0;
      const actionTime = getEventField(evt, "ActionTime");
      if (character === "Attacker" || character === "Player") {
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
      } else if (character.startsWith("Enemy")) {
        const enemyIndex = parseInt(character.replace("Enemy", ""));
        if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemyHPs.length) {
          this.enemyHPs[enemyIndex].max = maxHP;
          this.enemyHPs[enemyIndex].current = hp;
          if (actionTime) this.enemyActionTimes[enemyIndex] = actionTime;
          const hpBarData = this.enemyHpBars[enemyIndex];
          if (hpBarData == null ? void 0 : hpBarData.text) {
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
      } else if (character === "Defender" && this.enemyCount === 1) {
        this.enemyHPs[0].max = maxHP;
        this.enemyHPs[0].current = hp;
        if (actionTime) this.enemyActionTimes[0] = actionTime;
        const hpBarData = this.enemyHpBars[0];
        if (hpBarData == null ? void 0 : hpBarData.text) {
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
    createPlayer(width, height) {
      if (!this.stage) return;
      const bottomBarReserve = Math.min(140, height * 0.15);
      const groundOffset = bottomBarReserve + 10;
      let playerX, playerY;
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
      const isCustomSprite = this.playerSpritePath !== DEFAULT_SPRITES.player;
      if (isCustomSprite) {
        this.playerSprite.scale.set(-scale, scale);
      } else {
        this.playerSprite.scale.set(scale);
      }
      this.playerX = playerX;
      this.playerDisplayHeight = this.playerSprite.height;
      if (this.hasShotBuff) {
        this.playerAura = PIXI.Sprite.from(this._playerAlias);
        this.playerAura.anchor.set(0.5, 1);
        const auraScale = Math.abs(this.playerSprite.scale.x) * 1.25;
        if (isCustomSprite) {
          this.playerAura.scale.set(-auraScale, auraScale);
        } else {
          this.playerAura.scale.set(auraScale);
        }
        this.playerAura.x = playerX;
        this.playerAura.y = playerY;
        this.playerAura.alpha = 0.8;
        const cm = new PIXI.ColorMatrixFilter();
        cm.matrix = [
          0,
          0,
          0,
          0,
          0,
          0,
          0,
          0,
          0,
          0.667,
          0,
          0,
          0,
          0,
          1,
          0,
          0,
          0,
          1,
          0
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
        swayAmplitude: 2
      };
    }
    /* ────────────────────────── HUD Bars ───────────────────────────── */
    createHudBars(width, height) {
      var _a;
      if (!this.stage) return;
      const isMobile = this.isMobile;
      const barWidth = isMobile ? Math.min(220, width * 0.32) : Math.min(400, width * 0.4);
      const barHeight = isMobile ? Math.min(22, height * 0.035) : Math.min(36, height * 0.055);
      const topBarHeight = 54;
      const paddingTop = topBarHeight + 8;
      const paddingLeft = Math.min(16, width * 0.03);
      const hpBg = new PIXI.Graphics();
      hpBg.roundRect(paddingLeft, paddingTop, barWidth, barHeight, barHeight / 2);
      hpBg.fill({ color: 1710618, alpha: 0.85 });
      hpBg.stroke({ color: 3355443, width: 1 });
      this.stage.addChild(hpBg);
      const hpFill = new PIXI.Graphics();
      hpFill.roundRect(0, 0, barWidth, barHeight, barHeight / 2);
      hpFill.fill(5025616);
      hpFill.x = paddingLeft;
      hpFill.y = paddingTop;
      this.stage.addChild(hpFill);
      const hpBorder = new PIXI.Graphics();
      hpBorder.roundRect(paddingLeft, paddingTop, barWidth, barHeight, barHeight / 2);
      hpBorder.stroke({ width: 1.5, color: 6732650 });
      this.stage.addChild(hpBorder);
      const hpFontSize = isMobile ? Math.min(12, barHeight * 0.55) : Math.min(16, barHeight * 0.5);
      const hpText = new PIXI.Text({
        text: "",
        style: {
          fontFamily: "Arial, sans-serif",
          fontSize: hpFontSize,
          fontWeight: "bold",
          fill: 16777215,
          stroke: { color: 0, width: 2 }
        }
      });
      hpText.anchor.set(0.5, 0.5);
      hpText.x = paddingLeft + barWidth / 2;
      hpText.y = paddingTop + barHeight / 2;
      this.stage.addChild(hpText);
      this.playerHpBar = {
        bar: hpFill,
        barBg: hpBg,
        border: hpBorder,
        text: hpText,
        maxWidth: barWidth,
        barHeight,
        x: paddingLeft,
        y: paddingTop
      };
      const speedBarHeight = isMobile ? Math.min(10, height * 0.015) : Math.min(18, height * 0.025);
      const speedBarY = paddingTop + barHeight + 3;
      const speedBg = new PIXI.Graphics();
      speedBg.roundRect(paddingLeft, speedBarY, barWidth, speedBarHeight, speedBarHeight / 2);
      speedBg.fill({ color: 1118481, alpha: 0.85 });
      this.stage.addChild(speedBg);
      const speedFill = new PIXI.Graphics();
      speedFill.roundRect(0, 0, barWidth, speedBarHeight, speedBarHeight / 2);
      speedFill.fill(48340);
      speedFill.x = paddingLeft;
      speedFill.y = speedBarY;
      this.stage.addChild(speedFill);
      const speedFontSize = isMobile ? Math.min(8, speedBarHeight * 0.8) : Math.min(14, speedBarHeight * 0.8);
      const speedText = new PIXI.Text({
        text: "",
        style: {
          fontFamily: "Arial, sans-serif",
          fontSize: speedFontSize,
          fontWeight: "bold",
          fill: 16777215,
          stroke: { color: 0, width: 2 }
        }
      });
      speedText.anchor.set(0.5, 0.5);
      speedText.x = paddingLeft + barWidth / 2;
      speedText.y = speedBarY + speedBarHeight / 2;
      this.stage.addChild(speedText);
      this.playerSpeedBar = {
        bar: speedFill,
        barBg: speedBg,
        maxWidth: barWidth,
        text: speedText,
        barHeight: speedBarHeight
      };
      const bottomPadding = isMobile ? 10 : 12;
      const timerBarX = paddingLeft;
      const timerBarWidth = isMobile ? Math.min(240, width * 0.38) : Math.min(440, width * 0.44);
      const canhaoBarHeight = isMobile ? 14 : 22;
      const penaltyBarHeightCalc = isMobile ? 14 : 22;
      const penaltyBarYCalc = height - bottomPadding - penaltyBarHeightCalc;
      const canhaoBarY = penaltyBarYCalc - 4 - canhaoBarHeight;
      const canhaoBg = new PIXI.Graphics();
      canhaoBg.roundRect(timerBarX, canhaoBarY, timerBarWidth, canhaoBarHeight, canhaoBarHeight / 2);
      canhaoBg.fill({ color: 1703936, alpha: 0.85 });
      this.stage.addChild(canhaoBg);
      const canhaoFill = new PIXI.Graphics();
      canhaoFill.roundRect(0, 0, timerBarWidth, canhaoBarHeight, canhaoBarHeight / 2);
      canhaoFill.fill(15684432);
      canhaoFill.x = timerBarX;
      canhaoFill.y = canhaoBarY;
      this.stage.addChild(canhaoFill);
      const canhaoLabelFontSize = isMobile ? 8 : 13;
      const canhaoLabel = new PIXI.Text({
        text: "💥 AOE",
        style: { fontFamily: "Arial, sans-serif", fontSize: canhaoLabelFontSize, fontWeight: "bold", fill: 16777215, stroke: { color: 0, width: 2 } }
      });
      canhaoLabel.anchor.set(0, 0.5);
      canhaoLabel.x = timerBarX + 6;
      canhaoLabel.y = canhaoBarY + canhaoBarHeight / 2;
      this.stage.addChild(canhaoLabel);
      const canhaoFontSize = isMobile ? 9 : 14;
      const canhaoText = new PIXI.Text({
        text: "",
        style: { fontFamily: "Arial, sans-serif", fontSize: canhaoFontSize, fontWeight: "bold", fill: 16777215, stroke: { color: 0, width: 2 } }
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
        bar: canhaoFill,
        barBg: canhaoBg,
        label: canhaoLabel,
        text: canhaoText,
        maxWidth: timerBarWidth,
        barHeight: canhaoBarHeight
      };
      const penaltyBarHeight = penaltyBarHeightCalc;
      const penaltyBarY = penaltyBarYCalc;
      const penaltyBg = new PIXI.Graphics();
      penaltyBg.roundRect(timerBarX, penaltyBarY, timerBarWidth, penaltyBarHeight, penaltyBarHeight / 2);
      penaltyBg.fill({ color: 1706496, alpha: 0.85 });
      this.stage.addChild(penaltyBg);
      const penaltyFill = new PIXI.Graphics();
      penaltyFill.roundRect(0, 0, timerBarWidth, penaltyBarHeight, penaltyBarHeight / 2);
      penaltyFill.fill(16750592);
      penaltyFill.x = timerBarX;
      penaltyFill.y = penaltyBarY;
      this.stage.addChild(penaltyFill);
      const penaltyLabelFontSize = isMobile ? 8 : 13;
      const penaltyLabel = new PIXI.Text({
        text: "⚡ Lifesteal",
        style: { fontFamily: "Arial, sans-serif", fontSize: penaltyLabelFontSize, fontWeight: "bold", fill: 16777215, stroke: { color: 0, width: 2 } }
      });
      penaltyLabel.anchor.set(0, 0.5);
      penaltyLabel.x = timerBarX + 6;
      penaltyLabel.y = penaltyBarY + penaltyBarHeight / 2;
      this.stage.addChild(penaltyLabel);
      const penaltyFontSize = isMobile ? 9 : 14;
      const penaltyText = new PIXI.Text({
        text: "",
        style: { fontFamily: "Arial, sans-serif", fontSize: penaltyFontSize, fontWeight: "bold", fill: 16777215, stroke: { color: 0, width: 2 } }
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
        bar: penaltyFill,
        barBg: penaltyBg,
        label: penaltyLabel,
        text: penaltyText,
        maxWidth: timerBarWidth,
        barHeight: penaltyBarHeight
      };
      const isBossMode = ((_a = this.enemyType) == null ? void 0 : _a.toLowerCase()) === "boss";
      if (isBossMode) {
        this.createBossHudBars(width, height);
      }
    }
    createBossHudBars(width, height) {
      if (!this.stage) return;
      const isMobile = this.isMobile;
      const barWidth = isMobile ? Math.min(220, width * 0.32) : Math.min(400, width * 0.4);
      const barHeight = isMobile ? Math.min(22, height * 0.035) : Math.min(36, height * 0.055);
      const topBarHeight = 54;
      const paddingTop = topBarHeight + 8;
      const paddingLeft = Math.min(16, width * 0.03);
      const speedBarHeight = isMobile ? Math.min(10, height * 0.015) : Math.min(18, height * 0.025);
      const speedBarY = paddingTop + barHeight + 3;
      const hpFontSize = isMobile ? Math.min(12, barHeight * 0.55) : Math.min(16, barHeight * 0.5);
      const speedFontSize = isMobile ? Math.min(8, speedBarHeight * 0.8) : Math.min(14, speedBarHeight * 0.8);
      const rightX = width - paddingLeft - barWidth;
      const bossHpBg = new PIXI.Graphics();
      bossHpBg.roundRect(rightX, paddingTop, barWidth, barHeight, barHeight / 2);
      bossHpBg.fill({ color: 1710618, alpha: 0.85 });
      bossHpBg.stroke({ color: 3355443, width: 1 });
      this.stage.addChild(bossHpBg);
      const bossHpFill = new PIXI.Graphics();
      bossHpFill.roundRect(0, 0, barWidth, barHeight, barHeight / 2);
      bossHpFill.fill(16007990);
      bossHpFill.x = rightX;
      bossHpFill.y = paddingTop;
      this.stage.addChild(bossHpFill);
      const bossHpBorder = new PIXI.Graphics();
      bossHpBorder.roundRect(rightX, paddingTop, barWidth, barHeight, barHeight / 2);
      bossHpBorder.stroke({ width: 1.5, color: 15684432 });
      this.stage.addChild(bossHpBorder);
      const bossHpText = new PIXI.Text({
        text: "",
        style: {
          fontFamily: "Arial, sans-serif",
          fontSize: hpFontSize,
          fontWeight: "bold",
          fill: 16777215,
          stroke: { color: 0, width: 2 }
        }
      });
      bossHpText.anchor.set(0.5, 0.5);
      bossHpText.x = rightX + barWidth / 2;
      bossHpText.y = paddingTop + barHeight / 2;
      this.stage.addChild(bossHpText);
      this.bossHpBar = {
        bar: bossHpFill,
        barBg: bossHpBg,
        border: bossHpBorder,
        text: bossHpText,
        maxWidth: barWidth,
        barHeight,
        x: rightX,
        y: paddingTop
      };
      const bossSpeedBg = new PIXI.Graphics();
      bossSpeedBg.roundRect(rightX, speedBarY, barWidth, speedBarHeight, speedBarHeight / 2);
      bossSpeedBg.fill({ color: 1118481, alpha: 0.85 });
      this.stage.addChild(bossSpeedBg);
      const bossSpeedFill = new PIXI.Graphics();
      bossSpeedFill.roundRect(0, 0, barWidth, speedBarHeight, speedBarHeight / 2);
      bossSpeedFill.fill(48340);
      bossSpeedFill.x = rightX;
      bossSpeedFill.y = speedBarY;
      this.stage.addChild(bossSpeedFill);
      const bossSpeedText = new PIXI.Text({
        text: "",
        style: {
          fontFamily: "Arial, sans-serif",
          fontSize: speedFontSize,
          fontWeight: "bold",
          fill: 16777215,
          stroke: { color: 0, width: 2 }
        }
      });
      bossSpeedText.anchor.set(0.5, 0.5);
      bossSpeedText.x = rightX + barWidth / 2;
      bossSpeedText.y = speedBarY + speedBarHeight / 2;
      this.stage.addChild(bossSpeedText);
      this.bossSpeedBar = {
        bar: bossSpeedFill,
        barBg: bossSpeedBg,
        text: bossSpeedText,
        maxWidth: barWidth,
        barHeight: speedBarHeight
      };
    }
    /* ────────────────────────── Create Enemies ─────────────────────── */
    createEnemies(width, height) {
      var _a, _b, _c, _d, _e, _f;
      if (!this.stage) return;
      this.enemySprites = [];
      this.enemyHpBars = [];
      this.enemySpeedBars = [];
      this.enemyIdleOffsets = [];
      const isMobile = this.isMobile;
      const bottomBarReserve = Math.min(140, height * 0.15);
      const groundOffset = bottomBarReserve + 10;
      let baseX, baseY;
      if (isMobile) {
        baseX = width * 0.5;
        baseY = height * 0.5;
      } else {
        baseX = width * 0.72;
        baseY = height - groundOffset;
      }
      const isBoss = ((_a = this.enemyType) == null ? void 0 : _a.toLowerCase()) === "boss";
      const isMiniboss = ((_b = this.enemyType) == null ? void 0 : _b.toLowerCase()) === "miniboss";
      const tempSprites = [];
      const scaledWidths = [];
      const scaledHeights = [];
      if (isMobile) {
        let countScaleFactor = 1;
        if (this.enemyCount >= 9) countScaleFactor = 0.38;
        else if (this.enemyCount >= 8) countScaleFactor = 0.42;
        else if (this.enemyCount >= 7) countScaleFactor = 0.48;
        else if (this.enemyCount >= 6) countScaleFactor = 0.52;
        else if (this.enemyCount >= 5) countScaleFactor = 0.6;
        else if (this.enemyCount >= 4) countScaleFactor = 0.75;
        else if (this.enemyCount >= 3) countScaleFactor = 0.85;
        const bossBoost = isBoss ? 1.5 : 1;
        const maxSpriteHeight = height * 0.28;
        for (let i = 0; i < this.enemyCount; i++) {
          const alias = ((_c = this.enemySpriteAliases) == null ? void 0 : _c[i]) ?? `enemy_${this.enemySpritePaths[i]}`;
          const spr = PIXI.Sprite.from(alias);
          spr.anchor.set(0.5, 1);
          const baseScale = Math.min(1, maxSpriteHeight / spr.height);
          const finalScale = baseScale * countScaleFactor * bossBoost;
          spr.scale.set(finalScale);
          tempSprites.push(spr);
          scaledWidths.push(spr.width);
          scaledHeights.push(spr.height);
        }
      } else {
        const baseSizeNormal = 180;
        const baseSizeBig = 450;
        const isBigEnemy = isBoss || isMiniboss;
        const maxCols = 5;
        const hpBarReserve = 22;
        const rowGapLayout = 30;
        const topSafe = 120;
        const bottomSafe = Math.min(150, height * 0.15 + 20);
        const availableHeight = height - topSafe - bottomSafe;
        const aerialCount = ((_d = this.enemyPlacements) == null ? void 0 : _d.filter((p) => p === 1).length) ?? 0;
        const groundCount = this.enemyCount - aerialCount;
        const groundRows = groundCount > maxCols ? 2 : groundCount > 0 ? 1 : 0;
        const aerialRows = aerialCount > 0 ? 1 : 0;
        const totalRows = groundRows + aerialRows;
        let maxFittedHeight = baseSizeNormal;
        if (totalRows > 0) {
          const fittedH = (availableHeight - (totalRows - 1) * rowGapLayout - totalRows * hpBarReserve) / totalRows;
          maxFittedHeight = Math.max(60, fittedH);
        }
        const targetSize = isBigEnemy ? Math.min(baseSizeBig, maxFittedHeight * 2) : Math.min(baseSizeNormal, maxFittedHeight);
        for (let i = 0; i < this.enemyCount; i++) {
          const alias = ((_e = this.enemySpriteAliases) == null ? void 0 : _e[i]) ?? `enemy_${this.enemySpritePaths[i]}`;
          const spr = PIXI.Sprite.from(alias);
          spr.anchor.set(0.5, 1);
          const finalScale = Math.min(1, targetSize / spr.height);
          spr.scale.set(finalScale);
          tempSprites.push(spr);
          scaledWidths.push(spr.width);
          scaledHeights.push(spr.height);
        }
      }
      const positions = this.calculateEnemyPositions(
        isMobile,
        this.enemyCount,
        baseX,
        baseY,
        width,
        height,
        this.enemyPlacements,
        scaledWidths,
        scaledHeights
      );
      for (let i = 0; i < this.enemyCount; i++) {
        const pos = positions[i];
        if (!pos) continue;
        const enemy = tempSprites[i];
        const isAerial = ((_f = this.enemyPlacements) == null ? void 0 : _f[i]) === 1;
        enemy.x = pos.x;
        enemy.y = pos.y;
        this.enemyIdleOffsets.push({
          baseX: pos.x,
          baseY: pos.y,
          phase: i * (Math.PI / 2),
          isAerial,
          bobAmplitude: isAerial ? 6 : 3,
          swayAmplitude: isAerial ? 4 : 2
        });
        this.stage.addChild(enemy);
        this.enemySprites.push(enemy);
        const hpBarY = pos.y - scaledHeights[i] - 5;
        const hpBarWidth = isMobile ? 35 : Math.max(60, scaledWidths[i] * 0.5);
        const hpBarHeight = isMobile ? 4 : 8;
        const barBg = new PIXI.Graphics();
        barBg.rect(pos.x - hpBarWidth / 2, hpBarY - hpBarHeight / 2, hpBarWidth, hpBarHeight);
        barBg.fill(3355443);
        this.stage.addChild(barBg);
        const bar = new PIXI.Graphics();
        bar.rect(0, 0, hpBarWidth, hpBarHeight);
        bar.fill(16729156);
        bar.x = pos.x - hpBarWidth / 2;
        bar.y = hpBarY - hpBarHeight / 2;
        this.stage.addChild(bar);
        const hpFontSize = isMobile ? 7 : 10;
        const hpTxt = new PIXI.Text({
          text: "",
          style: {
            fontFamily: "Arial",
            fontSize: hpFontSize,
            fontWeight: "bold",
            fill: 16777215,
            stroke: { color: 0, width: 2 }
          }
        });
        const showText = true;
        hpTxt.anchor.set(0.5, 0.5);
        hpTxt.x = pos.x;
        hpTxt.y = hpBarY - 8;
        hpTxt.visible = showText;
        this.stage.addChild(hpTxt);
        this.enemyHpBars.push({
          bar,
          barBg,
          text: hpTxt,
          maxWidth: hpBarWidth,
          barHeight: hpBarHeight
        });
        const speedBarWidth = hpBarWidth;
        const speedBarH = isMobile ? 2 : 4;
        const speedBarYPos = hpBarY + hpBarHeight / 2 + 2;
        const speedBg = new PIXI.Graphics();
        speedBg.rect(pos.x - speedBarWidth / 2, speedBarYPos, speedBarWidth, speedBarH);
        speedBg.fill({ color: 1118481, alpha: 0.7 });
        this.stage.addChild(speedBg);
        const speedFill = new PIXI.Graphics();
        speedFill.rect(0, 0, speedBarWidth, speedBarH);
        speedFill.fill(48340);
        speedFill.x = pos.x - speedBarWidth / 2;
        speedFill.y = speedBarYPos;
        this.stage.addChild(speedFill);
        this.enemySpeedBars.push({
          bar: speedFill,
          barBg: speedBg,
          maxWidth: speedBarWidth,
          barHeight: speedBarH
        });
      }
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
    /**
     * Two-band layout using REAL scaled sprite widths.
     * Ground band at baseY, aerial band lifted above.
     * Desktop: single row per group (big screen, no wrapping needed).
     * Mobile: wraps into rows (max 3 cols).
     */
    calculateEnemyPositions(isMobile, count, baseX, baseY, width, _height, placements, scaledWidths, scaledHeights) {
      const positions = new Array(count);
      const aerialIdx = [];
      const groundIdx = [];
      for (let i = 0; i < count; i++) {
        if ((placements == null ? void 0 : placements[i]) === 1) aerialIdx.push(i);
        else groundIdx.push(i);
      }
      if (!isMobile) {
        const margin2 = 20;
        const hpBarReserve = 22;
        const maxCols = 5;
        const rowGap = 30;
        const layoutRow = (indices, footY, isAerial) => {
          if (indices.length === 0) return;
          if (indices.length === 1) {
            positions[indices[0]] = { x: baseX, y: footY, isAerial };
            return;
          }
          let cellW = 0;
          for (const idx of indices) {
            if (scaledWidths[idx] > cellW) cellW = scaledWidths[idx];
          }
          const colGap = Math.max(6, cellW * 0.05);
          let step = cellW + colGap;
          let totalW = step * (indices.length - 1);
          const maxAvailW = width - 2 * margin2 - cellW;
          if (totalW > maxAvailW && indices.length > 1) {
            step = Math.max(cellW + 2, maxAvailW / (indices.length - 1));
            totalW = step * (indices.length - 1);
          }
          let startX = baseX - totalW / 2;
          const halfCell = cellW / 2;
          if (startX - halfCell < margin2) startX = margin2 + halfCell;
          if (startX + totalW + halfCell > width - margin2) {
            startX = width - margin2 - halfCell - totalW;
            if (startX - halfCell < margin2) startX = margin2 + halfCell;
          }
          for (let c = 0; c < indices.length; c++) {
            positions[indices[c]] = {
              x: startX + c * step,
              y: footY,
              isAerial
            };
          }
        };
        const groundRow1 = groundIdx.slice(0, maxCols);
        const groundRow2 = groundIdx.slice(maxCols);
        let tallestGround2 = 0;
        for (const idx of groundIdx) {
          if (scaledHeights[idx] > tallestGround2) tallestGround2 = scaledHeights[idx];
        }
        if (tallestGround2 === 0) tallestGround2 = 100;
        let tallestAerial2 = 0;
        for (const idx of aerialIdx) {
          if (scaledHeights[idx] > tallestAerial2) tallestAerial2 = scaledHeights[idx];
        }
        const rowStep = tallestGround2 + hpBarReserve + rowGap;
        const groundRowCount = groundRow2.length > 0 ? 2 : groundRow1.length > 0 ? 1 : 0;
        const hasAerial = aerialIdx.length > 0;
        const topSafe = 120;
        const bottomSafe = Math.min(150, _height * 0.15 + 20);
        const aerialBand = hasAerial ? tallestAerial2 + hpBarReserve + rowGap : 0;
        const totalNeeded = groundRowCount * rowStep + aerialBand;
        const availableSpace = _height - topSafe - bottomSafe;
        let groundLineY = baseY;
        if (totalNeeded > availableSpace) {
          groundLineY = _height - bottomSafe;
        }
        const topOfFormation = groundLineY - totalNeeded;
        if (topOfFormation < topSafe) {
          groundLineY += topSafe - topOfFormation;
          const maxGroundY = _height - bottomSafe;
          if (groundLineY > maxGroundY) groundLineY = maxGroundY;
        }
        layoutRow(groundRow1, groundLineY, false);
        const row2Y = groundLineY - rowStep;
        layoutRow(groundRow2, row2Y, false);
        if (hasAerial) {
          const highestGroundRowY = groundRow2.length > 0 ? row2Y : groundLineY;
          const aerialFootY = highestGroundRowY - rowStep;
          layoutRow(aerialIdx, aerialFootY, true);
        }
        return positions;
      }
      const margin = 6;
      const topSafeY = 100;
      const layoutMobileRow = (indices, y, isAerial) => {
        if (indices.length === 0) return;
        if (indices.length === 1) {
          positions[indices[0]] = { x: baseX, y, isAerial };
          return;
        }
        let cellW = 0;
        for (const idx of indices) {
          if (scaledWidths[idx] > cellW) cellW = scaledWidths[idx];
        }
        const maxAvailW = width - 2 * margin;
        const minGap = 4;
        const maxPerRow = Math.max(1, Math.floor((maxAvailW + minGap) / (cellW + minGap)));
        const cols = Math.min(maxPerRow, indices.length);
        const rows = Math.ceil(indices.length / cols);
        let maxH = 0;
        for (const idx of indices) {
          if (scaledHeights[idx] > maxH) maxH = scaledHeights[idx];
        }
        let gi = 0;
        for (let row = 0; row < rows; row++) {
          const inRow = Math.min(cols, indices.length - gi);
          const gap = Math.max(minGap, cellW * 0.1);
          let step = cellW + gap;
          let totalW = step * (inRow - 1);
          const maxRowW = maxAvailW - cellW;
          if (totalW > maxRowW && inRow > 1) {
            step = Math.max(cellW + 2, maxRowW / (inRow - 1));
            totalW = step * (inRow - 1);
          }
          let startX = baseX - totalW / 2;
          const halfCell = cellW / 2;
          if (startX - halfCell < margin) startX = margin + halfCell;
          if (startX + totalW + halfCell > width - margin) {
            startX = width - margin - halfCell - totalW;
            if (startX - halfCell < margin) startX = margin + halfCell;
          }
          for (let c = 0; c < inRow; c++) {
            const idx = indices[gi + c];
            positions[idx] = {
              x: startX + c * step,
              y: y - row * (maxH * 0.55),
              isAerial
            };
          }
          gi += inRow;
        }
      };
      let tallestGround = 0;
      for (const idx of groundIdx) {
        if (scaledHeights[idx] > tallestGround) tallestGround = scaledHeights[idx];
      }
      if (tallestGround === 0) {
        for (let i = 0; i < count; i++) tallestGround += scaledHeights[i];
        tallestGround = count > 0 ? tallestGround / count : 100;
      }
      let tallestAerial = 0;
      for (const idx of aerialIdx) {
        if (scaledHeights[idx] > tallestAerial) tallestAerial = scaledHeights[idx];
      }
      let aerialY = baseY - tallestGround - 40;
      if (aerialY - tallestAerial < topSafeY) {
        aerialY = topSafeY + tallestAerial;
      }
      layoutMobileRow(aerialIdx, aerialY, true);
      layoutMobileRow(groundIdx, baseY, false);
      return positions;
    }
    /* ────────────────── Timed Battle (pre-computed) ────────────────── */
    startTimedBattle() {
      this.isPlaying = true;
      this.battleStartTime = Date.now();
      this.currentSimTime = 0;
      this.battleFinished = false;
    }
    /* ──────────────── Interactive Mode Init ────────────────────────── */
    initInteractiveState() {
      if (this.interactivePlayerHP != null) this.playerCurrentHp = this.interactivePlayerHP;
      if (this.interactivePlayerMaxHP != null) this.playerMaxHp = this.interactivePlayerMaxHP;
      if (this.interactivePlayerActionTime != null) this.playerActionTime = this.interactivePlayerActionTime;
      for (let i = 0; i < this.interactiveEnemies.length && i < this.enemyCount; i++) {
        const e = this.interactiveEnemies[i];
        const hp = e.hp ?? e.HP ?? 100;
        const maxHP = e.maxHP ?? e.MaxHP ?? hp;
        const at = e.actionTime ?? e.ActionTime ?? 3.5;
        this.enemyHPs[i] = { current: hp, max: maxHP };
        this.enemyActionTimes[i] = at;
      }
      this.playerSpeedBarTimer = this.playerActionTime * 1e3;
      for (let i = 0; i < this.enemyCount; i++) {
        this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1e3;
      }
      this.updatePlayerHPBar();
      for (let i = 0; i < this.enemyCount; i++) {
        this.updateIndividualEnemyHPBar(i);
      }
    }
    /* ────────────────────── Consumable Bar UI ──────────────────────── */
    createConsumableBar() {
      if (!this.app || !this.stage) return;
      const { width, height } = this.app.screen;
      const isMobile = this.isMobile;
      const btnGap = isMobile ? 6 : 10;
      const allConsumables = [
        { type: "fino", name: "Fino", color: 16098851, fallbackIcon: "🍺" },
        { type: "caneca", name: "Caneca", color: 16098851, fallbackIcon: "🍻" }
      ];
      const consumables = allConsumables.filter((c) => (this.consumableQuantities[c.type] ?? 0) > 0);
      if (consumables.length === 0) return;
      const maxBarWidth = isMobile ? Math.min(width * 0.9, 320) : Math.min(width * 0.9, 420);
      const btnSize = isMobile ? Math.min(60, Math.floor((maxBarWidth - (consumables.length - 1) * btnGap) / consumables.length)) : Math.min(76, Math.floor((maxBarWidth - (consumables.length - 1) * btnGap) / consumables.length));
      const totalWidth = consumables.length * btnSize + (consumables.length - 1) * btnGap;
      const startX = (width - totalWidth) / 2;
      const barY = height - btnSize - 10;
      this.consumableBarContainer = new PIXI.Container();
      this.stage.addChild(this.consumableBarContainer);
      const backdrop = new PIXI.Graphics();
      backdrop.roundRect(startX - 8, barY - 6, totalWidth + 16, btnSize + 12, 10);
      backdrop.fill({ color: 856343, alpha: 0.7 });
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
        const bg = new PIXI.Graphics();
        bg.roundRect(0, 0, btnSize, btnSize, 8);
        bg.fill({ color: qty > 0 ? 1712946 : 1710618, alpha: 0.95 });
        bg.stroke({ color: qty > 0 ? c.color : 4473924, width: 2 });
        btnContainer.addChild(bg);
        let iconSprite = null;
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
              const btn = this.consumableButtons.find((b) => b.type === type);
              if (btn) btn.iconSprite = spr;
            } catch {
              const fallback = new PIXI.Text({
                text: c.fallbackIcon,
                style: { fontSize: Math.min(22, btnSize * 0.45), fontFamily: "Arial, sans-serif" }
              });
              fallback.anchor.set(0.5);
              spriteContainer.addChild(fallback);
            }
          })();
        } else {
          const fallbackText = new PIXI.Text({
            text: c.fallbackIcon,
            style: { fontSize: Math.min(22, btnSize * 0.45), fontFamily: "Arial, sans-serif" }
          });
          fallbackText.anchor.set(0.5);
          fallbackText.x = btnSize / 2;
          fallbackText.y = btnSize / 2 - 2;
          btnContainer.addChild(fallbackText);
        }
        const qtyText = new PIXI.Text({
          text: `${qty}`,
          style: { fontSize: isMobile ? 10 : 12, fill: 16777215, fontWeight: "bold", fontFamily: "Arial" }
        });
        qtyText.anchor.set(1, 0);
        qtyText.x = btnSize - 2;
        qtyText.y = 1;
        btnContainer.addChild(qtyText);
        const cdOverlay = new PIXI.Graphics();
        cdOverlay.roundRect(0, 0, btnSize, btnSize, 8);
        cdOverlay.fill({ color: 0, alpha: 0.55 });
        cdOverlay.visible = false;
        btnContainer.addChild(cdOverlay);
        const cdText = new PIXI.Text({
          text: "",
          style: { fontSize: isMobile ? 10 : 14, fill: 16746564, fontWeight: "bold" }
        });
        cdText.anchor.set(0.5);
        cdText.x = btnSize / 2;
        cdText.y = btnSize / 2;
        cdText.visible = false;
        btnContainer.addChild(cdText);
        btnContainer.eventMode = "static";
        btnContainer.cursor = "pointer";
        btnContainer.on("pointerdown", () => this.onConsumableClick(type));
        this.consumableBarContainer.addChild(btnContainer);
        this.consumableButtons.push({
          type,
          container: btnContainer,
          bg,
          iconSprite,
          qtyText,
          cdOverlay,
          cdText
        });
      }
      for (const btn of this.consumableButtons) {
        this.updateConsumableButton(btn.type);
      }
    }
    onConsumableClick(type) {
      if (this.battleFinished || this._consumablePending) return;
      const qty = this.consumableQuantities[type] ?? 0;
      if (qty <= 0) return;
      const cd = this.consumableCooldowns[type] ?? 0;
      if (cd > 0) return;
      if ((type === "cigarro" || type === "canhao") && this.activeBuffs[type]) return;
      this.requestUseConsumable(type);
    }
    async requestUseConsumable(type) {
      if (this._destroyed || !this.dotNetRef || this._consumablePending) return;
      this._consumablePending = true;
      try {
        const json = await this.invokeWithTimeout("OnUseConsumable", _StageBattleScene.INTEROP_TIMEOUT_MS, type);
        if (this._destroyed || this.battleFinished) {
          this._consumablePending = false;
          return;
        }
        if (!json) {
          this._consumablePending = false;
          return;
        }
        const result = JSON.parse(json);
        const success = result.success ?? result.Success ?? false;
        if (!success) {
          this._consumablePending = false;
          return;
        }
        const newQty = result.newQuantity ?? result.NewQuantity;
        if (newQty != null) this.consumableQuantities[type] = newQty;
        const cd = result.cooldownSeconds ?? result.CooldownSeconds ?? 0;
        if (cd > 0) this.consumableCooldowns[type] = cd;
        if (type === "fino" || type === "caneca") {
          const hp = result.playerHP ?? result.PlayerHP;
          const maxHP = result.playerMaxHP ?? result.PlayerMaxHP;
          if (hp != null) this.playerCurrentHp = hp;
          if (maxHP != null) this.playerMaxHp = maxHP;
          this.updatePlayerHPBar();
          const heal = result.healAmount ?? result.HealAmount ?? 0;
          if (heal > 0 && this.playerSprite) {
            showFloatingText(this, `+${formatNum(heal)}`, this.playerSprite.x, this.playerSprite.y - this.playerDisplayHeight, 4521796);
            playBuffVfx(this, this.playerSprite, 4521796);
          }
        }
        if (type === "cigarro" || type === "canhao") {
          const buffActive = result.buffActive ?? result.BuffActive ?? false;
          this.activeBuffs[type] = buffActive;
          const msg = result.buffMessage ?? result.BuffMessage ?? "";
          if (msg && this.playerSprite) {
            showFloatingText(this, msg, this.playerSprite.x, this.playerSprite.y - this.playerDisplayHeight - 20, 16755200);
          }
          if (this.playerSprite) {
            playBuffVfx(this, this.playerSprite, type === "cigarro" ? 16737792 : 16729156);
          }
          if (type === "canhao" && buffActive) {
            this.canhaoBuffExpiresAt = Date.now() + this.canhaoBuffDurationMs;
            this.updateCanhaoTimerBar();
          }
        }
        if (type === "shot") {
          this.hasShotBuff = true;
          if (!this.playerAura && this.playerSprite && this.stage) {
            this.playerAura = PIXI.Sprite.from(this._playerAlias);
            this.playerAura.anchor.set(0.5, 1);
            const auraScaleAbs = Math.abs(this.playerSprite.scale.x) * 1.25;
            this.playerAura.scale.set(
              this.playerSprite.scale.x < 0 ? -auraScaleAbs : auraScaleAbs,
              auraScaleAbs
            );
            this.playerAura.x = this.playerSprite.x;
            this.playerAura.y = this.playerSprite.y;
            this.playerAura.alpha = 0.8;
            const cm = new PIXI.ColorMatrixFilter();
            cm.matrix = [
              0,
              0,
              0,
              0,
              0,
              0,
              0,
              0,
              0,
              0.667,
              0,
              0,
              0,
              0,
              1,
              0,
              0,
              0,
              1,
              0
            ];
            this.playerAura.filters = [cm, new PIXI.BlurFilter({ strength: 12 })];
            const idx = this.stage.getChildIndex(this.playerSprite);
            this.stage.addChildAt(this.playerAura, idx);
          }
          if (this.playerAura) {
            this.playerAura.visible = true;
            this.playerAura.alpha = 0.8;
          }
          const msg = result.buffMessage ?? result.BuffMessage ?? "";
          if (msg && this.playerSprite) {
            showFloatingText(this, msg, this.playerSprite.x, this.playerSprite.y - this.playerDisplayHeight - 20, 4504575);
          }
          if (this.playerSprite) playBuffVfx(this, this.playerSprite, 4504575);
        }
        if (type === "penalty") {
          this.hasPenaltyBuff = true;
          const newAt = result.newActionTime ?? result.NewActionTime;
          if (newAt != null) this.playerActionTime = newAt;
          const msg = result.buffMessage ?? result.BuffMessage ?? "";
          if (msg && this.playerSprite) {
            showFloatingText(this, msg, this.playerSprite.x, this.playerSprite.y - this.playerDisplayHeight - 20, 11158783);
          }
          if (this.playerSprite) playBuffVfx(this, this.playerSprite, 11158783);
          this.penaltyBuffExpiresAt = Date.now() + this.penaltyBuffDurationMs;
          this.updatePenaltyTimerBar();
        }
        this.updateConsumableButton(type);
      } catch (e) {
        console.warn("requestUseConsumable error:", e.message);
      }
      this._consumablePending = false;
    }
    updateConsumableButton(type) {
      const btn = this.consumableButtons.find((b) => b.type === type);
      if (!btn) return;
      const qty = this.consumableQuantities[type] ?? 0;
      btn.qtyText.text = `${qty}`;
      const cd = this.consumableCooldowns[type] ?? 0;
      const isBuffActive = (type === "cigarro" || type === "canhao") && this.activeBuffs[type];
      const disabled = qty <= 0 || cd > 0 || isBuffActive;
      btn.cdOverlay.visible = disabled;
      btn.cdText.visible = cd > 0;
      if (cd > 0) btn.cdText.text = `${Math.ceil(cd)}`;
      if (isBuffActive) {
        btn.bg.clear();
        btn.bg.roundRect(0, 0, btn.container.width, btn.container.height, 6);
        btn.bg.fill({ color: 4469504, alpha: 0.9 });
        btn.bg.stroke({ color: 16755200, width: 2 });
      }
      btn.container.alpha = disabled ? 0.5 : 1;
    }
    updateConsumableCooldownVisuals() {
      for (const btn of this.consumableButtons) {
        const cd = this.consumableCooldowns[btn.type] ?? 0;
        const isBuffActive = (btn.type === "cigarro" || btn.type === "canhao") && this.activeBuffs[btn.type];
        const qty = this.consumableQuantities[btn.type] ?? 0;
        const disabled = qty <= 0 || cd > 0 || isBuffActive;
        btn.cdOverlay.visible = disabled;
        btn.cdText.visible = cd > 0;
        if (cd > 0) btn.cdText.text = `${Math.ceil(cd)}`;
        btn.container.alpha = disabled ? 0.5 : 1;
      }
    }
    /* ───────────────── Interactive Battle Start ────────────────────── */
    startInteractiveBattle() {
      this.isPlaying = true;
      this.battleStartTime = Date.now();
      this.currentSimTime = 0;
      this.battleFinished = false;
      this._consumableTickAccum = 0;
      this._finishRetryCount = 0;
      this.clearJsBattleWatchdog();
      this._jsBattleWatchdogId = setTimeout(() => {
        if (!this._destroyed && !this.battleFinished && this.isPlaying) {
          console.warn(`JS battle watchdog: battle exceeded ${_StageBattleScene.JS_BATTLE_WATCHDOG_MS}ms, forcing finish`);
          this.finishBattle();
        }
      }, _StageBattleScene.JS_BATTLE_WATCHDOG_MS);
      this.startHeartbeat();
    }
    /* ──────────────── Heartbeat & Watchdog Helpers ─────────────────── */
    /** Pings Blazor every 30s so the server-side watchdog timer resets. */
    startHeartbeat() {
      this.stopHeartbeat();
      this._heartbeatInterval = setInterval(() => {
        if (this._destroyed || this.battleFinished || !this.dotNetRef) {
          this.stopHeartbeat();
          return;
        }
        this.dotNetRef.invokeMethodAsync("OnBattleHeartbeat").catch((e) => {
          console.warn("Heartbeat ping failed:", e.message);
        });
      }, _StageBattleScene.HEARTBEAT_INTERVAL_MS);
    }
    stopHeartbeat() {
      if (this._heartbeatInterval != null) {
        clearInterval(this._heartbeatInterval);
        this._heartbeatInterval = null;
      }
    }
    clearJsBattleWatchdog() {
      if (this._jsBattleWatchdogId != null) {
        clearTimeout(this._jsBattleWatchdogId);
        this._jsBattleWatchdogId = null;
      }
    }
    /**
     * Wraps a dotNetRef.invokeMethodAsync call with a timeout.
     * If the promise doesn't resolve/reject within `timeoutMs`, the returned
     * promise rejects with an error so callers' catch/finally blocks run.
     */
    invokeWithTimeout(method, timeoutMs, ...args) {
      if (!this.dotNetRef) return Promise.reject(new Error("dotNetRef is null"));
      const interopPromise = this.dotNetRef.invokeMethodAsync(method, ...args);
      const timeoutPromise = new Promise((_, reject) => {
        const id = setTimeout(() => reject(new Error(`Interop call '${method}' timed out after ${timeoutMs}ms`)), timeoutMs);
        interopPromise.then(() => clearTimeout(id), () => clearTimeout(id));
      });
      return Promise.race([interopPromise, timeoutPromise]);
    }
    /* ──────────────── Interactive Server Calls ─────────────────────── */
    async requestPlayerAutoAttack() {
      if (this._destroyed || !this.dotNetRef || this.battleFinished) {
        this._pendingPlayerAttacks = Math.max(0, this._pendingPlayerAttacks - 1);
        return;
      }
      try {
        const json = await this.invokeWithTimeout("OnPlayerAutoAttack", _StageBattleScene.INTEROP_TIMEOUT_MS);
        if (this._destroyed || this.battleFinished) return;
        if (json) this.processServerResult(JSON.parse(json));
      } catch (e) {
        console.warn("requestPlayerAutoAttack error:", e.message);
      } finally {
        this._pendingPlayerAttacks = Math.max(0, this._pendingPlayerAttacks - 1);
      }
    }
    async requestEnemyAttack(enemyIndex) {
      if (this._destroyed || !this.dotNetRef || this.battleFinished) {
        this._pendingEnemyAttacks[enemyIndex] = Math.max(0, this._pendingEnemyAttacks[enemyIndex] - 1);
        return;
      }
      try {
        const json = await this.invokeWithTimeout("OnEnemyAttack", _StageBattleScene.INTEROP_TIMEOUT_MS, enemyIndex);
        if (this._destroyed || this.battleFinished) return;
        if (json) this.processServerResult(JSON.parse(json));
      } catch (e) {
        console.warn("requestEnemyAttack error:", e.message);
      } finally {
        this._pendingEnemyAttacks[enemyIndex] = Math.max(0, this._pendingEnemyAttacks[enemyIndex] - 1);
      }
    }
    async requestTickConsumableCooldowns(realElapsedSeconds) {
      if (this._destroyed || !this.dotNetRef || this.battleFinished) return;
      try {
        const json = await this.invokeWithTimeout("OnTickConsumableCooldowns", _StageBattleScene.INTEROP_TIMEOUT_MS, realElapsedSeconds);
        if (this._destroyed || this.battleFinished) return;
        if (json) {
          const data = JSON.parse(json);
          for (const [type, remaining] of Object.entries(data)) {
            this.consumableCooldowns[type] = remaining;
          }
          this.updateConsumableCooldownVisuals();
        }
      } catch (e) {
        console.warn("OnTickConsumableCooldowns error:", e.message);
      }
    }
    /* ──────────────── Server Result Processing ─────────────────────── */
    processServerResult(result) {
      if (this._destroyed || this.battleFinished) return;
      const events = result.events ?? result.Events ?? [];
      for (const evt of events) {
        this.processInteractiveEvent(evt);
      }
      const battleOver = result.battleOver ?? result.BattleOver ?? false;
      if (battleOver) {
        const outcome = result.outcome ?? result.Outcome;
        if (outcome === 0) {
          this.handleVictory({ Winner: "Player" });
        } else if (outcome === 1) {
          this.handleVictory({ Winner: "Defender" });
        } else {
          this.handleDraw();
        }
      }
    }
    processInteractiveEvent(evt) {
      const evtType = getEventField(evt, "Type");
      switch (evtType) {
        case "HPUpdate":
          this.handleHPUpdate(evt);
          break;
        case "Attack":
          this.handleAttack(evt);
          break;
        case "KO":
          this.handleKO(evt);
          break;
      }
    }
    resolveEnemyIndex(evt) {
      const defender = getEventField(evt, "Defender") ?? "";
      if (defender.startsWith("Enemy")) {
        const idx = parseInt(defender.replace("Enemy", ""));
        if (!isNaN(idx) && idx >= 0 && idx < this.enemySprites.length) return idx;
      }
      return 0;
    }
    /* ────────────────────── Main Update Loop ───────────────────────── */
    update() {
      if (this._destroyed || !this.app || !this.stage || this.battleFinished) return;
      const delta = this.app.ticker.deltaMS;
      this.idleAnimationTime += delta * 1e-3;
      this.updateIdleAnimation();
      if (this.interactiveMode && !this.battleFinished && this.isPlaying) {
        const simDelta = delta * this.battleSpeed;
        this.currentSimTime += simDelta;
        if (this.playerCurrentHp > 0) {
          const anyEnemyAlive = this.enemyHPs.some((hp) => hp && hp.current > 0);
          if (!anyEnemyAlive) {
            this.playerSpeedBarTimer = this.playerActionTime * 1e3;
            this.updatePlayerSpeedBar();
          } else {
            this.playerSpeedBarTimer -= simDelta;
            this.updatePlayerSpeedBar();
            while (this.playerSpeedBarTimer <= 0 && this._pendingPlayerAttacks < _StageBattleScene.MAX_PENDING_ATTACKS) {
              this._pendingPlayerAttacks++;
              this.playerSpeedBarTimer += this.playerActionTime * 1e3;
              this.requestPlayerAutoAttack();
            }
            if (this.playerSpeedBarTimer < 0) this.playerSpeedBarTimer = 0;
          }
        }
        for (let i = 0; i < this.enemyCount; i++) {
          if (this.enemyHPs[i] && this.enemyHPs[i].current > 0) {
            if (this.playerCurrentHp <= 0) {
              this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1e3;
              this.updateEnemySpeedBar(i);
            } else {
              this.enemySpeedBarTimers[i] -= simDelta;
              this.updateEnemySpeedBar(i);
              while (this.enemySpeedBarTimers[i] <= 0 && this._pendingEnemyAttacks[i] < _StageBattleScene.MAX_PENDING_ATTACKS) {
                this._pendingEnemyAttacks[i]++;
                this.enemySpeedBarTimers[i] += this.enemyActionTimes[i] * 1e3;
                this.requestEnemyAttack(i);
              }
              if (this.enemySpeedBarTimers[i] < 0) this.enemySpeedBarTimers[i] = 0;
            }
          }
        }
        this._consumableTickAccum += delta;
        if (this._consumableTickAccum >= 200) {
          const realElapsed = this._consumableTickAccum / 1e3;
          this._consumableTickAccum = 0;
          for (const type of Object.keys(this.consumableCooldowns)) {
            this.consumableCooldowns[type] = Math.max(0, this.consumableCooldowns[type] - realElapsed);
          }
          this.updateConsumableCooldownVisuals();
          this.requestTickConsumableCooldowns(realElapsed);
        }
        this.updateCanhaoTimerBar();
        this.updatePenaltyTimerBar();
        return;
      } else if (!this.interactiveMode && this.isPlaying) {
        const scaledDelta = delta * this.battleSpeed;
        this.currentSimTime += scaledDelta;
        this.playerSpeedBarTimer -= scaledDelta;
        if (this.playerSpeedBarTimer <= 0 && this.currentEventIndex < this.eventsList.length) {
          this.playerSpeedBarTimer = this.playerActionTime * 1e3;
          this.processNextEvent();
        }
        this.updatePlayerSpeedBar();
        for (let i = 0; i < this.enemyCount; i++) {
          this.enemySpeedBarTimers[i] -= scaledDelta;
          if (this.enemySpeedBarTimers[i] <= 0 && this.currentEventIndex < this.eventsList.length) {
            this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1e3;
            this.processNextEvent();
          }
          this.updateEnemySpeedBar(i);
        }
      }
    }
    /* ──────────────── Idle Animation ───────────────────────────────── */
    updateIdleAnimation() {
      const t = this.idleAnimationTime;
      if (this.playerSprite && this.playerIdleOffset && !this._playerAttacking) {
        const p = this.playerIdleOffset;
        this.playerSprite.y = p.baseY + Math.sin(t * 1.2 + p.phase) * p.bobAmplitude;
        this.playerSprite.x = p.baseX + Math.sin(t * 0.8 + p.phase + 1) * p.swayAmplitude;
      }
      if (this.playerAura && this.playerSprite) {
        this.playerAura.x = this.playerSprite.x;
        this.playerAura.y = this.playerSprite.y;
        const pTime = performance.now() / 1e3;
        this.playerAura.alpha = 0.65 + Math.sin(pTime * 1.2) * 0.15;
      }
      for (let i = 0; i < this.enemySprites.length; i++) {
        if (this._enemyAttacking[i]) continue;
        const enemy = this.enemySprites[i];
        const offset = this.enemyIdleOffsets[i];
        if (!enemy || !offset) continue;
        const freqMod = offset.isAerial ? 1.5 : 1;
        enemy.y = offset.baseY + Math.sin(t * 1.2 * freqMod + offset.phase) * offset.bobAmplitude;
        enemy.x = offset.baseX + Math.sin(t * 0.8 * freqMod + offset.phase + 1) * offset.swayAmplitude;
      }
    }
    /* ──────────────── Speed Bar Updates ────────────────────────────── */
    updatePlayerSpeedBar() {
      if (!this.playerSpeedBar) return;
      const maxMs = this.playerActionTime * 1e3;
      const ratio = maxMs > 0 ? Math.max(0, Math.min(1, this.playerSpeedBarTimer / maxMs)) : 0;
      this.playerSpeedBar.bar.width = this.playerSpeedBar.maxWidth * ratio;
      if (this.playerSpeedBar.text) {
        const remaining = Math.max(0, this.playerSpeedBarTimer / 1e3);
        this.playerSpeedBar.text.text = `${remaining.toFixed(1)}s`;
      }
    }
    updateEnemySpeedBar(enemyIndex) {
      const speedBarData = this.enemySpeedBars[enemyIndex];
      if (!speedBarData) return;
      const maxMs = this.enemyActionTimes[enemyIndex] * 1e3;
      const ratio = maxMs > 0 ? Math.max(0, Math.min(1, this.enemySpeedBarTimers[enemyIndex] / maxMs)) : 0;
      speedBarData.bar.width = speedBarData.maxWidth * ratio;
      if (this.bossSpeedBar && enemyIndex === 0) {
        this.bossSpeedBar.bar.width = this.bossSpeedBar.maxWidth * ratio;
        if (this.bossSpeedBar.text) {
          const remaining = Math.max(0, this.enemySpeedBarTimers[0] / 1e3);
          this.bossSpeedBar.text.text = `${remaining.toFixed(1)}s`;
        }
      }
    }
    /* ──────────────── Buff Timer Bar Updates ────────────────────────── */
    updateCanhaoTimerBar() {
      if (!this.canhaoTimerBar) return;
      const now = Date.now();
      const active = this.canhaoBuffExpiresAt != null && now < this.canhaoBuffExpiresAt;
      this.canhaoTimerBar.bar.visible = active;
      this.canhaoTimerBar.barBg.visible = active;
      this.canhaoTimerBar.label.visible = active;
      this.canhaoTimerBar.text.visible = active;
      if (!active) return;
      const remainingMs = this.canhaoBuffExpiresAt - now;
      const ratio = Math.max(0, Math.min(1, remainingMs / this.canhaoBuffDurationMs));
      this.canhaoTimerBar.bar.width = this.canhaoTimerBar.maxWidth * ratio;
      const r = ratio > 0.5 ? Math.round(255 * (1 - ratio) * 2) : 255;
      const g = ratio > 0.5 ? 255 : Math.round(255 * ratio * 2);
      this.canhaoTimerBar.bar.tint = r << 16 | g << 8 | 0;
      const totalSec = Math.max(0, Math.ceil(remainingMs / 1e3));
      const min = Math.floor(totalSec / 60);
      const sec = totalSec % 60;
      this.canhaoTimerBar.text.text = `${min}:${sec.toString().padStart(2, "0")}`;
      if (remainingMs <= 15e3) {
        this.canhaoTimerBar.bar.alpha = 0.6 + 0.4 * Math.abs(Math.sin(now * 5e-3));
      } else {
        this.canhaoTimerBar.bar.alpha = 1;
      }
    }
    updatePenaltyTimerBar() {
      if (!this.penaltyTimerBar) return;
      const now = Date.now();
      const active = this.penaltyBuffExpiresAt != null && now < this.penaltyBuffExpiresAt;
      this.penaltyTimerBar.bar.visible = active;
      this.penaltyTimerBar.barBg.visible = active;
      this.penaltyTimerBar.label.visible = active;
      this.penaltyTimerBar.text.visible = active;
      if (!active) return;
      const remainingMs = this.penaltyBuffExpiresAt - now;
      const ratio = Math.max(0, Math.min(1, remainingMs / this.penaltyBuffDurationMs));
      this.penaltyTimerBar.bar.width = this.penaltyTimerBar.maxWidth * ratio;
      const r = 255;
      const g = Math.round(152 * ratio);
      this.penaltyTimerBar.bar.tint = r << 16 | g << 8 | 0;
      const totalSec = Math.max(0, Math.ceil(remainingMs / 1e3));
      const min = Math.floor(totalSec / 60);
      const sec = totalSec % 60;
      this.penaltyTimerBar.text.text = `${min}:${sec.toString().padStart(2, "0")}`;
      if (remainingMs <= 15e3) {
        this.penaltyTimerBar.bar.alpha = 0.6 + 0.4 * Math.abs(Math.sin(now * 5e-3));
      } else {
        this.penaltyTimerBar.bar.alpha = 1;
      }
    }
    /* ──────────────── Event Processing (pre-computed) ──────────────── */
    processEvent(evt) {
      const evtType = getEventField(evt, "Type");
      switch (evtType) {
        case "HPUpdate":
          this.handleHPUpdate(evt);
          break;
        case "Attack":
          this.handleAttack(evt);
          break;
        case "KO":
          this.handleKO(evt);
          break;
        case "Victory":
          this.handleVictory(evt);
          break;
        case "Draw":
          this.handleDraw();
          break;
      }
    }
    processNextEvent() {
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
    handleHPUpdate(evt) {
      const character = getEventField(evt, "Character") ?? "";
      const hp = getEventField(evt, "HP") ?? 0;
      const maxHP = getEventField(evt, "MaxHP");
      if (character === "Attacker" || character === "Player") {
        if (maxHP && maxHP > this.playerMaxHp) this.playerMaxHp = maxHP;
        this.playerCurrentHp = hp;
        this.updatePlayerHPBar();
      } else if (character.startsWith("Enemy")) {
        const idx = parseInt(character.replace("Enemy", ""));
        if (!isNaN(idx) && idx >= 0 && idx < this.enemyHpBars.length) {
          if (maxHP && maxHP > this.enemyHPs[idx].max) this.enemyHPs[idx].max = maxHP;
          this.enemyHPs[idx].current = hp;
          this.updateIndividualEnemyHPBar(idx);
        }
      } else {
        if (maxHP) {
          this.enemyMaxHp = maxHP;
          if (this.enemyHPs[0]) this.enemyHPs[0].max = maxHP;
        }
        this.enemyCurrentHp = hp;
        if (this.enemyHPs[0]) this.enemyHPs[0].current = hp;
        this.updateEnemyHPBar();
      }
    }
    updatePlayerHPBar() {
      if (!this.playerHpBar) return;
      const ratio = Math.min(1, Math.max(0, this.playerCurrentHp / this.playerMaxHp));
      const maxWidth = this.playerHpBar.maxWidth;
      const barHeight = this.playerHpBar.barHeight;
      const radius = barHeight / 2;
      const fillColor = ratio > 0.5 ? 5025616 : ratio > 0.25 ? 16750592 : 16007990;
      this.playerHpBar.bar.clear();
      this.playerHpBar.bar.roundRect(0, 0, maxWidth, barHeight, radius);
      this.playerHpBar.bar.fill(fillColor);
      animateTo(this, this.playerHpBar.bar, { width: maxWidth * ratio }, 200);
      this.playerHpBar.text.text = `${formatNum(Math.max(0, this.playerCurrentHp))} / ${formatNum(this.playerMaxHp)} HP`;
    }
    updateIndividualEnemyHPBar(enemyIndex) {
      const enemyHP = this.enemyHPs[enemyIndex];
      const hpBarData = this.enemyHpBars[enemyIndex];
      if (!enemyHP || !hpBarData) return;
      const ratio = Math.max(0, enemyHP.current / enemyHP.max);
      const newWidth = hpBarData.maxWidth * ratio;
      animateTo(this, hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
      hpBarData.text.text = `${formatNum(Math.max(0, Math.round(enemyHP.current)))}/${formatNum(Math.round(enemyHP.max))}`;
      if (this.bossHpBar && enemyIndex === 0) {
        const maxW = this.bossHpBar.maxWidth;
        const bh = this.bossHpBar.barHeight;
        const r = bh / 2;
        const fillColor = ratio > 0.5 ? 16007990 : ratio > 0.25 ? 13840175 : 12000284;
        this.bossHpBar.bar.clear();
        this.bossHpBar.bar.roundRect(0, 0, maxW, bh, r);
        this.bossHpBar.bar.fill(fillColor);
        animateTo(this, this.bossHpBar.bar, { width: maxW * ratio }, 200);
        this.bossHpBar.text.text = `${formatNum(Math.max(0, Math.round(enemyHP.current)))} / ${formatNum(Math.round(enemyHP.max))} HP`;
      }
    }
    updateEnemyHPBar() {
      const ratio = Math.max(0, this.enemyCurrentHp / this.enemyMaxHp);
      for (const hpBarData of this.enemyHpBars) {
        const newWidth = hpBarData.maxWidth * ratio;
        animateTo(this, hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
        hpBarData.text.text = `${formatNum(Math.max(0, Math.round(this.enemyCurrentHp / this.enemyCount)))}/${formatNum(Math.round(this.enemyMaxHp / this.enemyCount))}`;
      }
      if (this.bossHpBar) {
        const maxW = this.bossHpBar.maxWidth;
        const bh = this.bossHpBar.barHeight;
        const r = bh / 2;
        const fillColor = ratio > 0.5 ? 16007990 : ratio > 0.25 ? 13840175 : 12000284;
        this.bossHpBar.bar.clear();
        this.bossHpBar.bar.roundRect(0, 0, maxW, bh, r);
        this.bossHpBar.bar.fill(fillColor);
        animateTo(this, this.bossHpBar.bar, { width: maxW * ratio }, 200);
        this.bossHpBar.text.text = `${formatNum(Math.max(0, Math.round(this.enemyCurrentHp)))} / ${formatNum(Math.round(this.enemyMaxHp))} HP`;
      }
    }
    /* ──────────────── Attack Handling ───────────────────────────────── */
    handleAttack(evt) {
      const attacker = getEventField(evt, "Attacker") ?? "";
      const defender = getEventField(evt, "Defender") ?? "";
      const damage = getEventField(evt, "Damage") ?? 0;
      const isCritical = getEventField(evt, "IsCritical") ?? false;
      const isBlocked = getEventField(evt, "IsBlocked") ?? false;
      const isDodged = getEventField(evt, "IsDodged") ?? false;
      const isBoosted = getEventField(evt, "IsBoosted") ?? false;
      if (attacker === "Attacker" || attacker === "Player") {
        this.animatePlayerAttack(isCritical);
        if (defender == null ? void 0 : defender.startsWith("Enemy")) {
          const enemyIndex = parseInt(defender.replace("Enemy", ""));
          this.flashEnemy(enemyIndex, isCritical);
          const enemy = this.enemySprites[enemyIndex];
          if (enemy && damage > 0) {
            const yOff = enemy.y - (enemy.height || 40) * 0.8;
            if (isBlocked) {
              showFloatingText(this, "BLOCKED", enemy.x, yOff, 58879);
            } else {
              showDamageText(this, damage, isCritical, enemy.x, yOff);
            }
          }
          if (isBoosted && enemy) {
            showFloatingText(this, "EXTRA", enemy.x, enemy.y - (enemy.height || 40) * 0.8 - 25, 16750592);
          }
        } else {
          this.flashEnemies(isCritical);
          const enemy = this.enemySprites[0];
          if (enemy && damage > 0) {
            const yOff = enemy.y - (enemy.height || 40) * 0.8;
            if (isBlocked) {
              showFloatingText(this, "BLOCKED", enemy.x, yOff, 58879);
            } else {
              showDamageText(this, damage, isCritical, enemy.x, yOff);
            }
          }
          if (isBoosted && enemy) {
            showFloatingText(this, "EXTRA", enemy.x, enemy.y - (enemy.height || 40) * 0.8 - 25, 16750592);
          }
        }
      } else if (attacker.startsWith("Enemy")) {
        const enemyIndex = parseInt(attacker.replace("Enemy", ""));
        this.animateSingleEnemyAttack(enemyIndex, isCritical);
        if (this.playerSprite) {
          const yOff = this.playerSprite.y - (this.playerSprite.height || 40) * 0.8;
          if (isBlocked || isDodged) {
            showFloatingText(this, isDodged ? "DODGE" : "BLOCKED", this.playerSprite.x, yOff, 58879);
          } else {
            this.flashPlayer(isCritical);
            if (damage > 0) showDamageText(this, damage, isCritical, this.playerSprite.x, yOff);
          }
        }
      } else {
        this.animateEnemyAttack(isCritical);
        if (this.playerSprite) {
          const yOff = this.playerSprite.y - (this.playerSprite.height || 40) * 0.8;
          if (isBlocked || isDodged) {
            showFloatingText(this, isDodged ? "DODGE" : "BLOCKED", this.playerSprite.x, yOff, 58879);
          } else {
            this.flashPlayer(isCritical);
            if (damage > 0) showDamageText(this, damage, isCritical, this.playerSprite.x, yOff);
          }
        }
      }
      this._playSound(isBlocked || isDodged ? "block" : isCritical ? "critical" : "attack");
    }
    /* ──────────────── Attack Animations ─────────────────────────────── */
    animatePlayerAttack(isCritical) {
      var _a, _b;
      if (!this.playerSprite) return;
      if (this._playerAttacking) return;
      this._playerAttacking = true;
      const lungeDistance = isCritical ? 80 : 60;
      const lungeDuration = isCritical ? 120 : 150;
      const baseX = ((_a = this.playerIdleOffset) == null ? void 0 : _a.baseX) ?? this.playerSprite.x;
      const baseY = ((_b = this.playerIdleOffset) == null ? void 0 : _b.baseY) ?? this.playerSprite.y;
      const aura = this.playerAura;
      if (this.isMobile) {
        animateTo(this, this.playerSprite, { y: baseY - lungeDistance }, lungeDuration, () => {
          animateTo(this, this.playerSprite, { y: baseY }, 240, () => {
            this._playerAttacking = false;
          });
          if (aura) animateTo(this, aura, { y: baseY }, 240);
        });
        if (aura) animateTo(this, aura, { y: baseY - lungeDistance }, lungeDuration);
      } else {
        animateTo(this, this.playerSprite, { x: baseX + lungeDistance }, lungeDuration, () => {
          animateTo(this, this.playerSprite, { x: baseX }, 240, () => {
            this._playerAttacking = false;
          });
          if (aura) animateTo(this, aura, { x: baseX }, 240);
        });
        if (aura) animateTo(this, aura, { x: baseX + lungeDistance }, lungeDuration);
      }
    }
    animateEnemyAttack(isCritical) {
      var _a, _b;
      for (let index = 0; index < this.enemySprites.length; index++) {
        if (this._enemyAttacking[index]) continue;
        const enemy = this.enemySprites[index];
        this._enemyAttacking[index] = true;
        const lungeDistance = isCritical ? 80 : 60;
        const lungeDuration = isCritical ? 120 : 150;
        const eBaseX = ((_a = this.enemyIdleOffsets[index]) == null ? void 0 : _a.baseX) ?? enemy.x;
        const eBaseY = ((_b = this.enemyIdleOffsets[index]) == null ? void 0 : _b.baseY) ?? enemy.y;
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
        }, index * 50 / this.battleSpeed);
        this._timeoutIds.push(id);
      }
    }
    animateSingleEnemyAttack(enemyIndex, isCritical) {
      var _a, _b;
      if (enemyIndex < 0 || enemyIndex >= this.enemySprites.length) return;
      const enemy = this.enemySprites[enemyIndex];
      if (!enemy) return;
      if (this._enemyAttacking[enemyIndex]) return;
      this._enemyAttacking[enemyIndex] = true;
      const lungeDistance = isCritical ? 80 : 60;
      const lungeDuration = isCritical ? 120 : 150;
      const eBaseX = ((_a = this.enemyIdleOffsets[enemyIndex]) == null ? void 0 : _a.baseX) ?? enemy.x;
      const eBaseY = ((_b = this.enemyIdleOffsets[enemyIndex]) == null ? void 0 : _b.baseY) ?? enemy.y;
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
    flashPlayer(isCritical) {
      if (!this.playerSprite) return;
      if (this._playerFlashTimeout != null) {
        clearTimeout(this._playerFlashTimeout);
        this._playerFlashTimeout = null;
      }
      this.playerSprite.tint = isCritical ? 13369344 : 16711680;
      const flashDuration = isCritical ? 180 : 100;
      const scaledFlash = Math.max(50, flashDuration / this.battleSpeed);
      this._playerFlashTimeout = setTimeout(() => {
        if (this.playerSprite) this.playerSprite.tint = 16777215;
        this._playerFlashTimeout = null;
      }, scaledFlash);
    }
    flashEnemy(enemyIndex, isCritical) {
      if (enemyIndex < 0 || enemyIndex >= this.enemySprites.length) return;
      const enemy = this.enemySprites[enemyIndex];
      if (!enemy) return;
      if (this._enemyFlashTimeouts[enemyIndex] != null) {
        clearTimeout(this._enemyFlashTimeouts[enemyIndex]);
        delete this._enemyFlashTimeouts[enemyIndex];
      }
      enemy.tint = isCritical ? 13369344 : 16711680;
      const flashDuration = isCritical ? 180 : 100;
      const scaledFlash = Math.max(50, flashDuration / this.battleSpeed);
      this._enemyFlashTimeouts[enemyIndex] = setTimeout(() => {
        enemy.tint = 16777215;
        delete this._enemyFlashTimeouts[enemyIndex];
      }, scaledFlash);
    }
    flashEnemies(isCritical) {
      for (let i = 0; i < this.enemySprites.length; i++) {
        this.flashEnemy(i, isCritical);
      }
    }
    /* ──────────────── KO / Victory / Draw ──────────────────────────── */
    handleKO(evt) {
      const character = getEventField(evt, "Character") ?? "";
      this._playSound("ko");
      if (character === "Attacker" || character === "Player") {
        if (this.playerSprite) {
          animateTo(this, this.playerSprite, { alpha: 0.3, rotation: Math.PI / 2 }, 500);
          if (this.playerAura) animateTo(this, this.playerAura, { alpha: 0 }, 500);
        }
      } else if (character.startsWith("Enemy")) {
        const idx = parseInt(character.replace("Enemy", ""));
        if (!isNaN(idx) && idx >= 0 && idx < this.enemySprites.length) {
          const enemy = this.enemySprites[idx];
          animateTo(this, enemy, { alpha: 0, y: enemy.y - 50 }, 500);
          const hpBar = this.enemyHpBars[idx];
          if (hpBar) {
            animateTo(this, hpBar.bar, { alpha: 0 }, 300);
            animateTo(this, hpBar.barBg, { alpha: 0 }, 300);
            animateTo(this, hpBar.text, { alpha: 0 }, 300);
          }
          const speedBar = this.enemySpeedBars[idx];
          if (speedBar) {
            animateTo(this, speedBar.bar, { alpha: 0 }, 300);
            animateTo(this, speedBar.barBg, { alpha: 0 }, 300);
          }
          this.fadeBossHudBars(idx);
        }
      } else {
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
    fadeBossHudBars(enemyIndex) {
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
    handleVictory(evt) {
      const winner = getEventField(evt, "Winner") ?? "";
      const isPlayerWin = winner === "Attacker" || winner === "Player";
      if (isPlayerWin) {
        this._playSound("victory");
        if (this.playerSprite) {
          const originalY = this.playerSprite.y;
          animateTo(this, this.playerSprite, { y: originalY - 20 }, 200, () => {
            animateTo(this, this.playerSprite, { y: originalY }, 200, () => {
              animateTo(this, this.playerSprite, { y: originalY - 20 }, 200, () => {
                animateTo(this, this.playerSprite, { y: originalY }, 200);
              });
            });
          });
        }
      } else {
        this._playSound("defeat");
      }
      const isBossStage = this.enemyType === "boss" || this.stageNumber % 10 === 0;
      if (isBossStage || !isPlayerWin) {
        if (this.app && this.stage) {
          const resultText = new PIXI.Text({
            text: isPlayerWin ? "VICTORY!" : "DEFEAT",
            style: {
              fontSize: 48,
              fontFamily: "Arial, sans-serif",
              fontWeight: "bold",
              fill: isPlayerWin ? 4521796 : 16729156,
              stroke: { color: 0, width: 6 }
            }
          });
          resultText.anchor.set(0.5);
          resultText.x = this.app.screen.width / 2;
          resultText.y = this.app.screen.height / 2;
          resultText.scale.set(0);
          this.stage.addChild(resultText);
          animateTo(this, resultText, { scale: 1 }, 500);
        }
      }
      const finishDelay = isBossStage || !isPlayerWin ? 800 : 400;
      const id = setTimeout(() => this.finishBattle(), finishDelay / this.battleSpeed);
      this._timeoutIds.push(id);
    }
    handleDraw() {
      if (!this.app || !this.stage) return;
      const drawText = new PIXI.Text({
        text: "DRAW",
        style: {
          fontSize: 48,
          fontFamily: "Arial, sans-serif",
          fontWeight: "bold",
          fill: 16755200,
          stroke: { color: 0, width: 6 }
        }
      });
      drawText.anchor.set(0.5);
      drawText.x = this.app.screen.width / 2;
      drawText.y = this.app.screen.height / 2;
      this.stage.addChild(drawText);
      const id = setTimeout(() => this.finishBattle(), 800 / this.battleSpeed);
      this._timeoutIds.push(id);
    }
    /* ──────────────── Finish Battle ─────────────────────────────────── */
    finishBattle() {
      if (this._destroyed || this.battleFinished) return;
      this.battleFinished = true;
      this.isPlaying = false;
      this.stopHeartbeat();
      this.clearJsBattleWatchdog();
      if (this.eventTimer) {
        clearInterval(this.eventTimer);
        this.eventTimer = null;
      }
      this.notifyBlazerFinished();
    }
    /** Notifies Blazor that the battle finished, with retry on failure. */
    notifyBlazerFinished() {
      if (this._destroyed || !this.dotNetRef) return;
      try {
        this.dotNetRef.invokeMethodAsync("OnBattleFinished").catch((e) => {
          console.warn("Could not notify Blazor of battle finish:", e);
          this.retryFinishNotification();
        });
      } catch (e) {
        console.warn("finishBattle: dotNetRef error:", e.message);
        this.retryFinishNotification();
      }
    }
    /** Retries the OnBattleFinished call up to FINISH_RETRY_MAX times. */
    retryFinishNotification() {
      this._finishRetryCount++;
      if (this._finishRetryCount > _StageBattleScene.FINISH_RETRY_MAX || this._destroyed || !this.dotNetRef) {
        console.error(`OnBattleFinished failed after ${this._finishRetryCount} attempts — giving up (server watchdog will handle)`);
        return;
      }
      console.warn(`Retrying OnBattleFinished (attempt ${this._finishRetryCount}/${_StageBattleScene.FINISH_RETRY_MAX}) in ${_StageBattleScene.FINISH_RETRY_DELAY_MS}ms`);
      const id = setTimeout(() => this.notifyBlazerFinished(), _StageBattleScene.FINISH_RETRY_DELAY_MS);
      this._timeoutIds.push(id);
    }
    /* ──────────────── Public API ────────────────────────────────────── */
    setSpeed(speed) {
      const validSpeed = this._allowedSpeeds.includes(speed) ? speed : 1;
      this.playbackSpeed = validSpeed;
      this._battleSpeed = validSpeed;
    }
    setAudioEnabled(enabled) {
      this.audioEnabled = enabled;
      globalAudioEnabled = enabled;
      setMusicVolume(musicState, 0.3, enabled);
    }
    /* ──────────────── Destroy ──────────────────────────────────────── */
    destroy() {
      var _a, _b, _c, _d, _e;
      this._destroyed = true;
      this.battleFinished = true;
      this.isPlaying = false;
      this.stopHeartbeat();
      this.clearJsBattleWatchdog();
      if (this._contextLossTimerId != null) {
        clearTimeout(this._contextLossTimerId);
        this._contextLossTimerId = null;
      }
      if (this._onResize) {
        window.removeEventListener("resize", this._onResize);
        this._onResize = null;
      }
      if (this._onContextLost && ((_a = this.app) == null ? void 0 : _a.canvas)) {
        this.app.canvas.removeEventListener("webglcontextlost", this._onContextLost);
      }
      if (this._onContextRestored && ((_b = this.app) == null ? void 0 : _b.canvas)) {
        this.app.canvas.removeEventListener("webglcontextrestored", this._onContextRestored);
      }
      for (const id of this._timeoutIds) clearTimeout(id);
      for (const id of this._rafIds) cancelAnimationFrame(id);
      this._timeoutIds = [];
      this._rafIds = [];
      if (this._playerFlashTimeout != null) clearTimeout(this._playerFlashTimeout);
      this._playerFlashTimeout = null;
      for (const key of Object.keys(this._enemyFlashTimeouts)) {
        clearTimeout(this._enemyFlashTimeouts[Number(key)]);
      }
      this._enemyFlashTimeouts = {};
      destroyTextPool(this._textPool);
      if (this.eventTimer) {
        clearInterval(this.eventTimer);
        this.eventTimer = null;
      }
      if (!this.app) return;
      try {
        this.app.ticker.stop();
      } catch {
      }
      try {
        if ((_c = this.stage) == null ? void 0 : _c.children) {
          while (this.stage.children.length > 0) {
            const child = this.stage.children[0];
            this.stage.removeChild(child);
            if (child.destroy) {
              try {
                child.destroy({ children: true, texture: false });
              } catch {
              }
            }
          }
        }
      } catch {
      }
      try {
        if (this.app.renderer) {
          try {
            (_e = (_d = this.app.renderer).destroy) == null ? void 0 : _e.call(_d);
          } catch {
          }
          this.app.renderer = null;
        }
        this.app.destroy(false);
      } catch {
      }
      this.app = null;
      this.stage = null;
    }
    /* ──────────────── Reset For Next Battle ─────────────────────────── */
    async resetForNextBattle(data) {
      var _a;
      if (this._destroyed || !this.app || !this.stage) {
        console.warn("resetForNextBattle: app/stage destroyed, skipping");
        return;
      }
      this.isPlaying = false;
      this.battleFinished = true;
      this.stopHeartbeat();
      this.clearJsBattleWatchdog();
      for (const id of this._timeoutIds) clearTimeout(id);
      for (const id of this._rafIds) cancelAnimationFrame(id);
      this._timeoutIds = [];
      this._rafIds = [];
      this.eventsList = data.events ?? [];
      this.dotNetRef = data.dotNetRef ?? this.dotNetRef;
      this.stageNumber = pick(data, "StageNumber", "stageNumber", this.stageNumber + 1);
      this.enemyType = pick(data, "EnemyType", "enemyType", "Normal");
      this.enemyCount = pick(data, "EnemyCount", "enemyCount", 1);
      this.playerName = pick(data, "PlayerName", "playerName", this.playerName);
      this.enemyName = pick(data, "EnemyName", "enemyName", this.enemyName);
      this.backgroundPath = pick(data, "BackgroundPath", "backgroundPath", this.backgroundPath);
      this.playerSpritePath = pick(data, "PlayerSpritePath", "playerSpritePath", this.playerSpritePath);
      this.enemySpritePaths = data.enemySprites ?? data.EnemySprites ?? [];
      this.enemyPlacements = data.enemyPlacements ?? data.EnemyPlacements ?? Array(this.enemyCount).fill(0);
      this.hasShotBuff = pick(data, "HasShotBuff", "hasShotBuff", this.hasShotBuff);
      this.hasPenaltyBuff = pick(data, "HasPenaltyBuff", "hasPenaltyBuff", false);
      this.interactiveMode = pick(data, "InteractiveMode", "interactiveMode", this.interactiveMode);
      this.interactivePlayerHP = data.playerHP ?? data.PlayerHP ?? null;
      this.interactivePlayerMaxHP = data.playerMaxHP ?? data.PlayerMaxHP ?? null;
      this.interactivePlayerActionTime = data.playerActionTime ?? data.PlayerActionTime ?? null;
      this.interactiveEnemies = data.enemies ?? data.Enemies ?? [];
      this._pendingPlayerAttacks = 0;
      this._pendingEnemyAttacks = Array(this.enemyCount).fill(0);
      const cData = data.consumables ?? data.Consumables;
      if (cData) {
        this.consumableQuantities = {
          fino: pick(cData, "Fino", "fino", this.consumableQuantities.fino),
          caneca: pick(cData, "Caneca", "caneca", this.consumableQuantities.caneca),
          cigarro: pick(cData, "Cigarro", "cigarro", this.consumableQuantities.cigarro),
          canhao: pick(cData, "Canhao", "canhao", this.consumableQuantities.canhao),
          shot: pick(cData, "Shot", "shot", this.consumableQuantities.shot),
          penalty: pick(cData, "Penalty", "penalty", this.consumableQuantities.penalty)
        };
      }
      const ciData = data.consumableImages ?? data.ConsumableImages;
      if (ciData) {
        this.consumableImages = {
          fino: pick(ciData, "Fino", "fino", this.consumableImages.fino),
          caneca: pick(ciData, "Caneca", "caneca", this.consumableImages.caneca),
          cigarro: pick(ciData, "Cigarro", "cigarro", this.consumableImages.cigarro),
          canhao: pick(ciData, "Canhao", "canhao", this.consumableImages.canhao)
        };
      }
      const abData = data.activeBuffs ?? data.ActiveBuffs;
      if (abData) {
        this.activeBuffs = {
          cigarro: !!pick(abData, "Cigarro", "cigarro", false),
          canhao: !!pick(abData, "Canhao", "canhao", false),
          shot: !!pick(abData, "Shot", "shot", false),
          penalty: !!pick(abData, "Penalty", "penalty", false)
        };
      }
      const canhaoUtcReset = data.canhaoBuffExpiresAtUtc ?? data.CanhaoBuffExpiresAtUtc ?? null;
      if (canhaoUtcReset) {
        this.canhaoBuffExpiresAt = new Date(canhaoUtcReset).getTime();
      } else if (!(this.canhaoBuffExpiresAt && Date.now() < this.canhaoBuffExpiresAt)) {
        this.canhaoBuffExpiresAt = null;
      }
      const penaltyUtcReset = data.penaltyBuffExpiresAtUtc ?? data.PenaltyBuffExpiresAtUtc ?? null;
      if (penaltyUtcReset) {
        this.penaltyBuffExpiresAt = new Date(penaltyUtcReset).getTime();
      } else if (!(this.penaltyBuffExpiresAt && Date.now() < this.penaltyBuffExpiresAt)) {
        this.penaltyBuffExpiresAt = null;
      }
      const ccData = data.consumableCooldowns ?? data.ConsumableCooldowns;
      if (ccData) {
        for (const [type, remaining] of Object.entries(ccData)) {
          this.consumableCooldowns[type] = remaining;
        }
      }
      try {
        await this.loadAssets();
      } catch (e) {
        console.error("Failed to load assets for next stage:", e);
      }
      if (this._destroyed || !this.app || !this.stage) {
        console.warn("resetForNextBattle: destroyed during asset load");
        return;
      }
      this.currentEventIndex = 0;
      this.battleFinished = false;
      this.playerMaxHp = 100;
      this.playerCurrentHp = 100;
      this.playerActionTime = 3.5;
      this.enemyHPs = Array.from({ length: this.enemyCount }, () => ({ current: 100, max: 100 }));
      this.idleAnimationTime = 0;
      this.enemyIdleOffsets = [];
      const persistent = /* @__PURE__ */ new Set();
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
      if (!this.stage) return;
      const toRemove = [];
      try {
        for (const child of [...this.stage.children]) {
          if (!persistent.has(child)) toRemove.push(child);
        }
      } catch (e) {
        console.warn("resetForNextBattle: error iterating stage:", e.message);
        return;
      }
      for (const child of toRemove) {
        try {
          this.stage.removeChild(child);
          if (child.destroy) {
            child.destroy({ children: true, texture: false });
          }
        } catch {
        }
      }
      if (this.playerSprite) {
        this.playerSprite.alpha = 1;
        this.playerSprite.rotation = 0;
        this.playerSprite.tint = 16777215;
        if (this.playerIdleOffset) {
          this.playerSprite.x = this.playerIdleOffset.baseX;
          this.playerSprite.y = this.playerIdleOffset.baseY;
        }
      }
      if (this.playerHpBar) {
        this.playerHpBar.bar.width = this.playerHpBar.maxWidth;
        this.playerHpBar.text.text = "100/100";
      }
      if (this.playerSpeedBar) {
        this.playerSpeedBar.bar.width = this.playerSpeedBar.maxWidth;
      }
      if (this.bossHpBar) {
        const bh = this.bossHpBar.barHeight;
        const r = bh / 2;
        this.bossHpBar.bar.clear();
        this.bossHpBar.bar.roundRect(0, 0, this.bossHpBar.maxWidth, bh, r);
        this.bossHpBar.bar.fill(16007990);
        this.bossHpBar.bar.width = this.bossHpBar.maxWidth;
        this.bossHpBar.bar.alpha = 1;
        this.bossHpBar.barBg.alpha = 1;
        if (this.bossHpBar.border) this.bossHpBar.border.alpha = 1;
        this.bossHpBar.text.alpha = 1;
        this.bossHpBar.text.text = "100/100";
      }
      if (this.bossSpeedBar) {
        this.bossSpeedBar.bar.width = this.bossSpeedBar.maxWidth;
        this.bossSpeedBar.bar.alpha = 1;
        this.bossSpeedBar.barBg.alpha = 1;
        if (this.bossSpeedBar.text) this.bossSpeedBar.text.alpha = 1;
      }
      if (this.hasShotBuff && !this.playerAura) {
        if (this.playerSprite && this.stage) {
          this.playerAura = PIXI.Sprite.from(this._playerAlias);
          this.playerAura.anchor.set(0.5, 1);
          const auraScaleAbs = Math.abs(this.playerSprite.scale.x) * 1.25;
          this.playerAura.scale.set(
            this.playerSprite.scale.x < 0 ? -auraScaleAbs : auraScaleAbs,
            auraScaleAbs
          );
          this.playerAura.x = this.playerSprite.x;
          this.playerAura.y = this.playerSprite.y;
          this.playerAura.alpha = 0.8;
          const cm = new PIXI.ColorMatrixFilter();
          cm.matrix = [
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0.667,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0
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
      if (this.backgroundSprite) {
        const newBgTexture = PIXI.Assets.get(this.bgAlias);
        if (newBgTexture && this.backgroundSprite.texture !== newBgTexture) {
          this.backgroundSprite.texture = newBgTexture;
        }
      }
      this.enemySprites = [];
      this.enemyHpBars = [];
      this.enemySpeedBars = [];
      this.enemySpeedBarTimers = Array(this.enemyCount).fill(3500);
      this.enemyActionTimes = Array(this.enemyCount).fill(3.5);
      this._playerAttacking = false;
      this._enemyAttacking = {};
      if (this._playerFlashTimeout != null) {
        clearTimeout(this._playerFlashTimeout);
        this._playerFlashTimeout = null;
      }
      for (const key of Object.keys(this._enemyFlashTimeouts)) {
        clearTimeout(this._enemyFlashTimeouts[Number(key)]);
      }
      this._enemyFlashTimeouts = {};
      const width = this.app.screen.width;
      const height = this.app.screen.height;
      this.isMobile = width <= height || width < 500;
      this.createEnemies(width, height);
      const isBossNow = ((_a = this.enemyType) == null ? void 0 : _a.toLowerCase()) === "boss";
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
      for (const enemy of this.enemySprites) {
        if (enemy) {
          enemy.alpha = 0;
          animateTo(this, enemy, { alpha: 1 }, 250);
        }
      }
      for (const hpBar of this.enemyHpBars) {
        if (hpBar) {
          if (hpBar.bar) {
            hpBar.bar.alpha = 0;
            animateTo(this, hpBar.bar, { alpha: 1 }, 250);
          }
          if (hpBar.barBg) {
            hpBar.barBg.alpha = 0;
            animateTo(this, hpBar.barBg, { alpha: 1 }, 250);
          }
          if (hpBar.text) {
            hpBar.text.alpha = 0;
            animateTo(this, hpBar.text, { alpha: 1 }, 250);
          }
        }
      }
      for (const speedBar of this.enemySpeedBars) {
        if (speedBar) {
          if (speedBar.bar) {
            speedBar.bar.alpha = 0;
            animateTo(this, speedBar.bar, { alpha: 1 }, 250);
          }
          if (speedBar.barBg) {
            speedBar.barBg.alpha = 0;
            animateTo(this, speedBar.barBg, { alpha: 1 }, 250);
          }
        }
      }
      if (this._destroyed || !this.app || !this.stage) return;
      if (this.interactiveMode) {
        this.initInteractiveState();
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
    static stopBackgroundMusic() {
      stopMusic(musicState);
      musicState = null;
    }
    static setGlobalAudioEnabled(enabled) {
      globalAudioEnabled = enabled;
      setMusicVolume(musicState, 0.3, enabled);
    }
  };
  __publicField(_StageBattleScene, "MAX_PENDING_ATTACKS", 3);
  __publicField(_StageBattleScene, "HEARTBEAT_INTERVAL_MS", 3e4);
  __publicField(_StageBattleScene, "JS_BATTLE_WATCHDOG_MS", 12e4);
  __publicField(_StageBattleScene, "CONTEXT_LOSS_RECOVERY_MS", 5e3);
  // Interop call timeout — prevents hung promises from freezing combat
  __publicField(_StageBattleScene, "INTEROP_TIMEOUT_MS", 15e3);
  __publicField(_StageBattleScene, "FINISH_RETRY_MAX", 3);
  __publicField(_StageBattleScene, "FINISH_RETRY_DELAY_MS", 2e3);
  let StageBattleScene = _StageBattleScene;
  let stageScene = null;
  let _stopped = false;
  function createGame(containerId, battleData) {
    _stopped = false;
    const container = document.getElementById(containerId);
    if (!container) {
      console.error("Stage battle container not found:", containerId);
      return;
    }
    if (stageScene) {
      stageScene.destroy();
      stageScene = null;
    }
    const data = battleData;
    const events = resolveEvents(battleData);
    const dotNetRef = data.dotNetRef ?? data.DotNetRef ?? null;
    stageScene = new StageBattleScene(container, {
      events,
      dotNetRef,
      stageNumber: pick(data, "StageNumber", "stageNumber", 1),
      enemyType: pick(data, "EnemyType", "enemyType", "normal"),
      enemyCount: pick(data, "EnemyCount", "enemyCount", 1),
      playerName: pick(data, "PlayerName", "playerName", "Player"),
      enemyName: pick(data, "EnemyName", "enemyName", "Enemy"),
      backgroundPath: pick(data, "BackgroundPath", "backgroundPath", ""),
      playerSpritePath: pick(data, "PlayerSpritePath", "playerSpritePath", ""),
      enemySprites: data.enemySprites ?? data.EnemySprites,
      enemyPlacements: data.enemyPlacements ?? data.EnemyPlacements,
      HasShotBuff: pick(data, "HasShotBuff", "hasShotBuff", false),
      interactiveMode: pick(data, "InteractiveMode", "interactiveMode", false),
      playerHP: data.playerHP ?? data.PlayerHP,
      playerMaxHP: data.playerMaxHP ?? data.PlayerMaxHP,
      playerActionTime: data.playerActionTime ?? data.PlayerActionTime,
      enemies: data.enemies ?? data.Enemies,
      consumables: data.consumables ?? data.Consumables,
      consumableImages: data.consumableImages ?? data.ConsumableImages,
      activeBuffs: data.activeBuffs ?? data.ActiveBuffs,
      BattleSpeed: data.battleSpeed ?? data.BattleSpeed,
      AllowedSpeeds: data.allowedSpeeds ?? data.AllowedSpeeds,
      canhaoBuffExpiresAtUtc: data.canhaoBuffExpiresAtUtc ?? data.CanhaoBuffExpiresAtUtc,
      penaltyBuffExpiresAtUtc: data.penaltyBuffExpiresAtUtc ?? data.PenaltyBuffExpiresAtUtc
    });
    const initialSpeed = pick(data, "BattleSpeed", "battleSpeed", 1);
    if (initialSpeed !== 1) {
      stageScene.setSpeed(initialSpeed);
    }
  }
  function destroyBattle() {
    _stopped = true;
    if (stageScene) {
      stageScene.destroy();
      stageScene = null;
    }
    StageBattleScene.stopBackgroundMusic();
  }
  function destroySceneOnly() {
    _stopped = true;
    if (stageScene) {
      stageScene.destroy();
      stageScene = null;
    }
  }
  function nextBattle(battleData) {
    if (!stageScene || !stageScene.app || !stageScene.stage) {
      if (stageScene) {
        try {
          stageScene.destroy();
        } catch {
        }
        stageScene = null;
      }
      createGame("phaserBattleContainer", battleData);
      return;
    }
    if (_stopped) {
      console.warn("nextBattle: game was stopped, ignoring");
      return;
    }
    const data = battleData;
    const events = resolveEvents(battleData);
    stageScene.resetForNextBattle({
      events,
      dotNetRef: data.DotNetRef ?? data.dotNetRef,
      stageNumber: data.StageNumber ?? data.stageNumber,
      enemyType: data.EnemyType ?? data.enemyType,
      enemyCount: data.EnemyCount ?? data.enemyCount,
      playerName: data.PlayerName ?? data.playerName,
      enemyName: data.EnemyName ?? data.enemyName,
      backgroundPath: data.BackgroundPath ?? data.backgroundPath,
      playerSpritePath: data.playerSpritePath ?? data.PlayerSpritePath,
      enemySprites: data.enemySprites ?? data.EnemySprites,
      enemyPlacements: data.enemyPlacements ?? data.EnemyPlacements,
      HasShotBuff: data.HasShotBuff ?? data.hasShotBuff,
      interactiveMode: data.interactiveMode ?? data.InteractiveMode,
      playerHP: data.playerHP ?? data.PlayerHP,
      playerMaxHP: data.playerMaxHP ?? data.PlayerMaxHP,
      playerActionTime: data.playerActionTime ?? data.PlayerActionTime,
      enemies: data.enemies ?? data.Enemies,
      consumables: data.consumables ?? data.Consumables,
      consumableImages: data.consumableImages ?? data.ConsumableImages,
      consumableCooldowns: data.consumableCooldowns ?? data.ConsumableCooldowns,
      activeBuffs: data.activeBuffs ?? data.ActiveBuffs,
      canhaoBuffExpiresAtUtc: data.canhaoBuffExpiresAtUtc ?? data.CanhaoBuffExpiresAtUtc,
      penaltyBuffExpiresAtUtc: data.penaltyBuffExpiresAtUtc ?? data.PenaltyBuffExpiresAtUtc
    });
  }
  const api = {
    start: createGame,
    destroy: destroyBattle,
    destroySceneOnly,
    setSpeed(speed) {
      if (stageScene) {
        stageScene.setSpeed(speed);
      }
    },
    setAudioEnabled(enabled) {
      if (stageScene) {
        stageScene.setAudioEnabled(enabled);
      }
      StageBattleScene.setGlobalAudioEnabled(enabled);
    },
    nextBattle
  };
  window.stageBattleGame = api;
})();
//# sourceMappingURL=pixiStageBattle.js.map
