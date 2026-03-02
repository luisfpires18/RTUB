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
  function resolveDotNetRef(battleData) {
    if (!battleData || Array.isArray(battleData)) return null;
    const data = battleData;
    return data.dotNetRef ?? data.DotNetRef ?? null;
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
  function fadeOut(owner, target, duration, onComplete) {
    animateTo(owner, target, { alpha: 0 }, duration, onComplete);
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
  const DEFAULT_WIDTH = 800;
  const DEFAULT_HEIGHT = 500;
  const DEFAULT_EVENT_INTERVAL = 800;
  const SPRITE_PATHS = {
    attacker: "/sprites/games/my-tuno/tuno_attacking_right.png",
    defender: "/sprites/games/my-tuno/tuno_attacking_left.png",
    background: "/sprites/games/my-tuno/backgrounds/arena.png"
  };
  class ArenaBattleScene {
    /* ────────────────────────── Constructor ────────────────────────── */
    constructor(container, data) {
      // PIXI application
      __publicField(this, "app", null);
      __publicField(this, "stage", null);
      // Container & config
      __publicField(this, "container");
      __publicField(this, "mode");
      __publicField(this, "eventInterval");
      // Events
      __publicField(this, "eventsList");
      __publicField(this, "dotNetRef");
      // Combatants
      __publicField(this, "attackerName");
      __publicField(this, "defenderName");
      __publicField(this, "characterSprites", {});
      __publicField(this, "maxHp", { attacker: 100, defender: 100 });
      __publicField(this, "currentHp", { attacker: 100, defender: 100 });
      __publicField(this, "hpGraphics", null);
      __publicField(this, "hpTexts", {});
      __publicField(this, "nameTexts", {});
      // HUD bar references (persistent, top-left / top-right)
      __publicField(this, "hudBars", {});
      // Shot buff visual
      __publicField(this, "hasShotBuff");
      __publicField(this, "attackerAura", null);
      // Custom sprite paths (overrides SPRITE_PATHS when non-empty)
      __publicField(this, "attackerSpritePath");
      __publicField(this, "defenderSpritePath");
      // Actual PixiJS asset aliases used (may differ if custom sprites loaded)
      __publicField(this, "_attackerAlias", "attackerSprite");
      __publicField(this, "_defenderAlias", "defenderSprite");
      __publicField(this, "_attackerIsCustom", false);
      __publicField(this, "_defenderIsCustom", false);
      // Speed bars
      __publicField(this, "actionTime", { attacker: 5, defender: 5 });
      __publicField(this, "speedBars", { attacker: null, defender: null });
      __publicField(this, "speedBarTimers", { attacker: 0, defender: 0 });
      __publicField(this, "battleStartTime", 0);
      __publicField(this, "currentSimTime", 0);
      // Anti-exploit: getter/setter restricts battleSpeed to allowed values
      __publicField(this, "_battleSpeed", 1);
      // Replay state
      __publicField(this, "currentEventIndex", 0);
      __publicField(this, "battleEvents", null);
      __publicField(this, "replayIndex", 0);
      __publicField(this, "isPlaying", false);
      __publicField(this, "playbackSpeed", 1);
      __publicField(this, "replayAccumulator", 0);
      __publicField(this, "battleFinished", false);
      // Audio
      __publicField(this, "audioContext", null);
      __publicField(this, "audioEnabled", true);
      __publicField(this, "musicVolume", 0.3);
      __publicField(this, "sfxVolume", 0.5);
      __publicField(this, "musicState", null);
      // Interactive mode
      __publicField(this, "interactiveMode");
      __publicField(this, "interactivePlayerHP");
      __publicField(this, "interactivePlayerMaxHP");
      __publicField(this, "interactivePlayerActionTime");
      __publicField(this, "interactiveEnemies");
      __publicField(this, "_playerAttackPending", false);
      __publicField(this, "_enemyAttackPending", false);
      // Cleanup trackers (VfxOwner requirement)
      __publicField(this, "_timeoutIds", []);
      __publicField(this, "_rafIds", []);
      __publicField(this, "_textPool", { pool: [] });
      // Destroyed flag — prevents async callbacks from running after destroy
      __publicField(this, "_destroyed", false);
      // WebGL / visibility listeners
      __publicField(this, "_onContextLost", null);
      __publicField(this, "_onVisibilityChange", null);
      this.container = container;
      this.eventsList = data.events ?? [];
      this.dotNetRef = data.dotNetRef ?? null;
      this.mode = data.mode ?? "live";
      this.eventInterval = data.eventInterval ?? DEFAULT_EVENT_INTERVAL;
      this.attackerName = data.attackerName ?? "Attacker";
      this.defenderName = data.defenderName ?? "Defender";
      this.hasShotBuff = data.HasShotBuff ?? data.hasShotBuff ?? false;
      this.attackerSpritePath = data.AttackerSpritePath ?? data.attackerSpritePath ?? "";
      this.defenderSpritePath = data.DefenderSpritePath ?? data.defenderSpritePath ?? "";
      this.interactiveMode = data.InteractiveMode ?? data.interactiveMode ?? false;
      this.interactivePlayerHP = data.PlayerHP ?? data.playerHP ?? null;
      this.interactivePlayerMaxHP = data.PlayerMaxHP ?? data.playerMaxHP ?? null;
      this.interactivePlayerActionTime = data.PlayerActionTime ?? data.playerActionTime ?? null;
      this.interactiveEnemies = data.Enemies ?? data.enemies ?? [];
      this.setupAudio();
      this.initPixi();
    }
    get battleSpeed() {
      return this._battleSpeed;
    }
    set battleSpeed(v) {
      const allowed = [1, 5];
      this._battleSpeed = allowed.includes(v) ? v : 1;
    }
    /* ────────────────────────── Audio Setup ────────────────────────── */
    setupAudio() {
      this.audioContext = getSharedAudioContext();
      if (this.audioContext) {
        if (this.audioContext.state === "suspended") {
          this.audioContext.resume().catch(() => {
          });
        }
        if (!this.musicState) {
          loadBackgroundMusic(
            this.audioContext,
            "/sound/arena_battle.mp3",
            this.musicVolume,
            this.audioEnabled
          ).then((state) => {
            this.musicState = state;
          });
        }
      } else {
        this.audioEnabled = false;
      }
    }
    _playSound(type) {
      if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
      playSound(this.audioContext, type, this.sfxVolume);
    }
    toggleAudio() {
      this.audioEnabled = !this.audioEnabled;
      setMusicVolume(this.musicState, this.musicVolume, this.audioEnabled);
      return this.audioEnabled;
    }
    setVolume(musicVol, sfxVol) {
      this.musicVolume = Math.max(0, Math.min(1, musicVol));
      this.sfxVolume = Math.max(0, Math.min(1, sfxVol));
      setMusicVolume(this.musicState, this.musicVolume, this.audioEnabled);
    }
    /* ────────────────────────── PixiJS Init ────────────────────────── */
    async initPixi() {
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
      } catch (_) {
      }
      this._onContextLost = (e) => {
        console.warn("WebGL context lost — finishing battle to recover");
        e.preventDefault();
        this.finishBattle();
      };
      this.app.canvas.addEventListener("webglcontextlost", this._onContextLost);
      this._onVisibilityChange = () => {
        var _a, _b, _c, _d, _e, _f;
        if (document.visibilityState === "visible" && !this.battleFinished) {
          const gl = ((_c = (_b = (_a = this.app) == null ? void 0 : _a.canvas) == null ? void 0 : _b.getContext) == null ? void 0 : _c.call(_b, "webgl2")) || ((_f = (_e = (_d = this.app) == null ? void 0 : _d.canvas) == null ? void 0 : _e.getContext) == null ? void 0 : _f.call(_e, "webgl"));
          if (!gl || gl.isContextLost()) {
            console.warn("App returned from background with lost GL context — finishing battle");
            this.finishBattle();
          }
        }
      };
      document.addEventListener("visibilitychange", this._onVisibilityChange);
      await this.loadAssets();
      this.create();
    }
    /* ────────────────────────── Asset Loading ──────────────────────── */
    async loadAssets() {
      const toLoad = [];
      if (this.attackerSpritePath) {
        this._attackerAlias = `arena_atk_${this.attackerSpritePath}`;
        this._attackerIsCustom = true;
        if (!loadedAssetAliases.has(this._attackerAlias)) {
          toLoad.push({ alias: this._attackerAlias, src: this.attackerSpritePath + SESSION_CACHE_BUST });
        }
      } else {
        this._attackerAlias = "attackerSprite";
        if (!loadedAssetAliases.has("attackerSprite")) {
          toLoad.push({ alias: "attackerSprite", src: SPRITE_PATHS.attacker + SESSION_CACHE_BUST });
        }
      }
      if (this.defenderSpritePath) {
        this._defenderAlias = `arena_def_${this.defenderSpritePath}`;
        this._defenderIsCustom = true;
        if (!loadedAssetAliases.has(this._defenderAlias)) {
          toLoad.push({ alias: this._defenderAlias, src: this.defenderSpritePath + SESSION_CACHE_BUST });
        }
      } else {
        this._defenderAlias = "defenderSprite";
        if (!loadedAssetAliases.has("defenderSprite")) {
          toLoad.push({ alias: "defenderSprite", src: SPRITE_PATHS.defender + SESSION_CACHE_BUST });
        }
      }
      if (!loadedAssetAliases.has("arenaBg")) {
        toLoad.push({ alias: "arenaBg", src: SPRITE_PATHS.background + SESSION_CACHE_BUST });
      }
      if (toLoad.length > 0) {
        await PIXI.Assets.load(toLoad);
        for (const a of toLoad) {
          loadedAssetAliases.add(a.alias);
        }
      }
    }
    /* ────────────────────────── Scene Creation ─────────────────────── */
    create() {
      if (!this.app || !this.stage) return;
      const { width, height } = this.app.screen;
      const bg = PIXI.Sprite.from("arenaBg");
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
        this.startInteractiveBattle();
      } else if (this.mode === "live") {
        this.startTimedBattle();
      } else {
        this.setupReplayLoop();
      }
      this.app.ticker.add(this.update, this);
    }
    createArena() {
      if (!this.app || !this.stage) return;
      const { width, height } = this.app.screen;
      const isMobile = width < 768;
      this.createCharacters(width, height, isMobile);
    }
    createCharacters(width, height, isMobile) {
      if (!this.stage) return;
      const maxSpriteHeight = isMobile ? height * 0.25 : height * 0.45;
      const atkSprite = PIXI.Sprite.from(this._attackerAlias);
      atkSprite.anchor.set(0.5, 1);
      const atkScale = this.getSpriteScale(atkSprite, maxSpriteHeight);
      if (this._attackerIsCustom) {
        atkSprite.scale.set(-atkScale, atkScale);
      } else {
        atkSprite.scale.set(atkScale);
      }
      let atkX, atkY;
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
      if (this.hasShotBuff) {
        const aura = PIXI.Sprite.from(this._attackerAlias);
        aura.anchor.set(0.5, 1);
        const auraScale = atkScale * 1.25;
        if (this._attackerIsCustom) {
          aura.scale.set(-auraScale, auraScale);
        } else {
          aura.scale.set(auraScale);
        }
        aura.x = atkX;
        aura.y = atkY;
        aura.alpha = 0.8;
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
        aura.filters = [cm, new PIXI.BlurFilter({ strength: 12 })];
        const spriteIdx = this.stage.getChildIndex(atkSprite);
        this.stage.addChildAt(aura, spriteIdx);
        this.attackerAura = aura;
      }
      const defSprite = PIXI.Sprite.from(this._defenderAlias);
      defSprite.anchor.set(0.5, 1);
      const defScale = this.getSpriteScale(defSprite, maxSpriteHeight);
      defSprite.scale.set(defScale);
      let defX, defY;
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
      const atkNameText = new PIXI.Text({
        text: this.attackerName,
        style: { fontFamily: "Arial", fontSize: isMobile ? 14 : 18, fontWeight: "bold", fill: 16777215 }
      });
      atkNameText.anchor.set(0.5);
      atkNameText.x = atkX;
      atkNameText.y = atkY + 15;
      this.stage.addChild(atkNameText);
      this.nameTexts.attacker = atkNameText;
      const defNameText = new PIXI.Text({
        text: this.defenderName,
        style: { fontFamily: "Arial", fontSize: isMobile ? 14 : 18, fontWeight: "bold", fill: 16777215 }
      });
      defNameText.anchor.set(0.5);
      defNameText.x = defX;
      defNameText.y = defY + 15;
      this.stage.addChild(defNameText);
      this.nameTexts.defender = defNameText;
    }
    getSpriteScale(sprite, maxSpriteHeight) {
      if (!sprite.texture || !sprite.texture.height) {
        return 0.6;
      }
      return Math.min(1, maxSpriteHeight / sprite.texture.height);
    }
    startIdleAnimation() {
      for (const char of Object.values(this.characterSprites)) {
        const data = {
          breathTime: Math.random() * 3,
          scaleTime: Math.random() * 3,
          originalY: char.sprite.y,
          originalX: char.sprite.x,
          originalScale: char.sprite.scale.x
        };
        char.sprite.idleAnimationData = data;
      }
    }
    /* ────────────────────────── HP & Speed Bars ────────────────────── */
    initializeHpFromEvents() {
      for (const evt of this.eventsList) {
        const type = getEventField(evt, "Type");
        if (type !== "HPUpdate") continue;
        const character = getEventField(evt, "Character");
        const hp = getEventField(evt, "HP") ?? 0;
        const maxHP = getEventField(evt, "MaxHP");
        const actionTime = getEventField(evt, "ActionTime");
        if (character === "Attacker") {
          if (maxHP != null) this.maxHp.attacker = maxHP;
          this.currentHp.attacker = hp;
          if (actionTime != null) this.actionTime.attacker = actionTime;
        } else if (character === "Defender") {
          if (maxHP != null) this.maxHp.defender = maxHP;
          this.currentHp.defender = hp;
          if (actionTime != null) this.actionTime.defender = actionTime;
        }
      }
    }
    drawHpBars() {
      if (!this.app || !this.stage) return;
      const { width, height } = this.app.screen;
      const isMobile = width < 768;
      if (this.hudBars.attacker && this.hudBars.defender) {
        for (const key of ["attacker", "defender"]) {
          const hud = this.hudBars[key];
          const ratio = Math.max(0, this.currentHp[key] / this.maxHp[key]);
          const fillColor = key === "attacker" ? ratio > 0.5 ? 5025616 : ratio > 0.25 ? 13421636 : 13386820 : ratio > 0.5 ? 16007990 : ratio > 0.25 ? 13840175 : 12000284;
          hud.hpFill.clear();
          hud.hpFill.roundRect(0, 0, hud.maxHpWidth, hud.hpBarHeight, hud.hpBarHeight / 2);
          hud.hpFill.fill(fillColor);
          hud.hpFill.width = hud.maxHpWidth * ratio;
          hud.hpText.text = `${formatNum(this.currentHp[key])} / ${formatNum(this.maxHp[key])} HP`;
        }
        return;
      }
      const barWidth = isMobile ? Math.min(220, width * 0.32) : Math.min(400, width * 0.4);
      const barHeight = isMobile ? Math.min(22, height * 0.035) : Math.min(36, height * 0.055);
      const speedBarHeight = isMobile ? Math.min(10, height * 0.015) : Math.min(18, height * 0.025);
      const topBarHeight = 54;
      const paddingTop = topBarHeight + 8;
      const paddingLeft = Math.min(16, width * 0.03);
      const positions = {
        attacker: paddingLeft,
        // top-left
        defender: width - paddingLeft - barWidth
        // top-right
      };
      const hpColors = { attacker: 5025616, defender: 16007990 };
      const borderColors = { attacker: 6732650, defender: 15684432 };
      for (const key of ["attacker", "defender"]) {
        const x = positions[key];
        const hpBg = new PIXI.Graphics();
        hpBg.roundRect(x, paddingTop, barWidth, barHeight, barHeight / 2);
        hpBg.fill({ color: 1710618, alpha: 0.85 });
        hpBg.stroke({ color: 3355443, width: 1 });
        this.stage.addChild(hpBg);
        const ratio = Math.max(0, this.currentHp[key] / this.maxHp[key]);
        const hpFill = new PIXI.Graphics();
        hpFill.roundRect(0, 0, barWidth, barHeight, barHeight / 2);
        hpFill.fill(hpColors[key]);
        hpFill.x = x;
        hpFill.y = paddingTop;
        hpFill.width = barWidth * ratio;
        this.stage.addChild(hpFill);
        const hpBorder = new PIXI.Graphics();
        hpBorder.roundRect(x, paddingTop, barWidth, barHeight, barHeight / 2);
        hpBorder.stroke({ width: 1.5, color: borderColors[key] });
        this.stage.addChild(hpBorder);
        const hpFontSize = isMobile ? Math.min(12, barHeight * 0.55) : Math.min(16, barHeight * 0.5);
        const hpText = new PIXI.Text({
          text: `${formatNum(this.currentHp[key])} / ${formatNum(this.maxHp[key])} HP`,
          style: {
            fontFamily: "Arial, sans-serif",
            fontSize: hpFontSize,
            fontWeight: "bold",
            fill: 16777215,
            stroke: { color: 0, width: 2 }
          }
        });
        hpText.anchor.set(0.5, 0.5);
        hpText.x = x + barWidth / 2;
        hpText.y = paddingTop + barHeight / 2;
        this.stage.addChild(hpText);
        const speedBarY = paddingTop + barHeight + 3;
        const speedBg = new PIXI.Graphics();
        speedBg.roundRect(x, speedBarY, barWidth, speedBarHeight, speedBarHeight / 2);
        speedBg.fill({ color: 1118481, alpha: 0.85 });
        this.stage.addChild(speedBg);
        const speedFill = new PIXI.Graphics();
        speedFill.roundRect(0, 0, barWidth, speedBarHeight, speedBarHeight / 2);
        speedFill.fill(48340);
        speedFill.x = x;
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
        speedText.x = x + barWidth / 2;
        speedText.y = speedBarY + speedBarHeight / 2;
        this.stage.addChild(speedText);
        this.hudBars[key] = {
          hpBg,
          hpFill,
          hpBorder,
          hpText,
          speedBg,
          speedFill,
          speedText,
          maxHpWidth: barWidth,
          hpBarHeight: barHeight,
          maxSpeedWidth: barWidth,
          speedBarHeight
        };
      }
    }
    drawSpeedBars() {
      for (const key of ["attacker", "defender"]) {
        const hud = this.hudBars[key];
        if (!hud) continue;
        const maxMs = this.actionTime[key] * 1e3;
        const ratio = maxMs > 0 ? Math.max(0, Math.min(1, this.speedBarTimers[key] / maxMs)) : 0;
        hud.speedFill.width = hud.maxSpeedWidth * ratio;
        const remaining = Math.max(0, this.speedBarTimers[key] / 1e3);
        hud.speedText.text = `${remaining.toFixed(1)}s`;
      }
    }
    /* ──────────────── Interactive Mode Init ────────────────────────── */
    initInteractiveState() {
      if (this.interactivePlayerHP != null) this.currentHp.attacker = this.interactivePlayerHP;
      if (this.interactivePlayerMaxHP != null) this.maxHp.attacker = this.interactivePlayerMaxHP;
      if (this.interactivePlayerActionTime != null) this.actionTime.attacker = this.interactivePlayerActionTime;
      const enemy = this.interactiveEnemies[0];
      if (enemy) {
        const hp = enemy.hp ?? enemy.HP ?? 100;
        const maxHP = enemy.maxHP ?? enemy.MaxHP ?? hp;
        const at = enemy.actionTime ?? enemy.ActionTime ?? 5;
        this.currentHp.defender = hp;
        this.maxHp.defender = maxHP;
        this.actionTime.defender = at;
      }
      this.speedBarTimers.attacker = this.actionTime.attacker * 1e3;
      this.speedBarTimers.defender = this.actionTime.defender * 1e3;
      this.drawHpBars();
      this.drawSpeedBars();
    }
    /* ────────────────── Interactive Battle Flow ────────────────────── */
    startInteractiveBattle() {
      this.battleStartTime = Date.now();
      this.currentSimTime = 0;
      this.battleFinished = false;
      this.isPlaying = true;
      this._playerAttackPending = false;
      this._enemyAttackPending = false;
    }
    async requestPlayerAutoAttack() {
      if (this._destroyed || !this.dotNetRef || this.battleFinished) {
        this._playerAttackPending = false;
        return;
      }
      try {
        const json = await this.dotNetRef.invokeMethodAsync("OnPlayerAutoAttack");
        if (this._destroyed || this.battleFinished) return;
        if (json) this.processServerResult(JSON.parse(json));
      } catch (e) {
        console.warn("OnPlayerAutoAttack error:", e);
      } finally {
        this._playerAttackPending = false;
        this.speedBarTimers.attacker = this.actionTime.attacker * 1e3;
      }
    }
    async requestEnemyAttack() {
      if (this._destroyed || !this.dotNetRef || this.battleFinished) {
        this._enemyAttackPending = false;
        return;
      }
      try {
        const json = await this.dotNetRef.invokeMethodAsync("OnEnemyAttack", 0);
        if (this._destroyed || this.battleFinished) return;
        if (json) this.processServerResult(JSON.parse(json));
      } catch (e) {
        console.warn("OnEnemyAttack error:", e);
      } finally {
        this._enemyAttackPending = false;
        this.speedBarTimers.defender = this.actionTime.defender * 1e3;
      }
    }
    processServerResult(result) {
      if (this._destroyed || this.battleFinished || !result) return;
      const events = result.events ?? result.Events ?? [];
      for (const evt of events) {
        this.processInteractiveEvent(evt);
      }
      const battleOver = result.battleOver ?? result.BattleOver ?? false;
      if (battleOver) {
        this.isPlaying = false;
        const outcome = result.outcome ?? result.Outcome;
        if (outcome === 0) {
          this.showVictory("Attacker");
        } else if (outcome === 2) {
          this.showDraw();
        } else {
          this.showVictory("Defender");
        }
        const id = setTimeout(() => this.finishBattle(), 2e3 / this.battleSpeed);
        this._timeoutIds.push(id);
      }
    }
    processInteractiveEvent(evt) {
      const evtType = evt.type ?? evt.Type;
      switch (evtType) {
        case "HPUpdate":
        case "Attack":
        case "KO":
          this.processEvent(evt);
          break;
        case "StatusEffect":
          this.handleStatusEffect(evt);
          break;
      }
    }
    /* ────────────────────── Timed / Replay Modes ───────────────────── */
    startTimedBattle() {
      this.battleEvents = this.eventsList.map((evt) => ({
        event: evt,
        simTime: getEventField(evt, "SimTime") ?? getEventField(evt, "simTime") ?? 0
      })).sort((a, b) => a.simTime - b.simTime);
      this.currentEventIndex = 0;
      this.battleStartTime = Date.now();
      this.currentSimTime = 0;
      this.battleFinished = false;
      this.isPlaying = true;
      while (this.currentEventIndex < this.battleEvents.length) {
        const eventData = this.battleEvents[this.currentEventIndex];
        if (eventData.simTime > 0) break;
        this.processEvent(eventData.event);
        this.currentEventIndex++;
      }
    }
    processEvent(evt) {
      const type = getEventField(evt, "Type");
      if (type === "HPUpdate") {
        const character = getEventField(evt, "Character");
        const hp = getEventField(evt, "HP") ?? 0;
        if (character === "Attacker") this.currentHp.attacker = hp;
        else if (character === "Defender") this.currentHp.defender = hp;
        this.drawHpBars();
        return;
      }
      if (type === "Attack") {
        const attackerKey = getEventField(evt, "Attacker");
        const defenderKey = getEventField(evt, "Defender");
        const damage = getEventField(evt, "Damage");
        this.playAttack(attackerKey ?? "Attacker", defenderKey ?? "Defender", damage ?? 0, evt);
        return;
      }
      if (type === "KO") {
        const character = getEventField(evt, "Character");
        this.playKo(character ?? "Defender");
        return;
      }
      if (type === "Victory") {
        const winner = getEventField(evt, "Winner");
        this.showVictory(winner ?? "Attacker");
      }
    }
    /* ────────────────────── Attack Animation ───────────────────────── */
    playAttack(attackerKey, defenderKey, damage, evt) {
      const attacker = attackerKey === "Defender" ? this.characterSprites.defender : this.characterSprites.attacker;
      const defender = defenderKey === "Attacker" ? this.characterSprites.attacker : this.characterSprites.defender;
      if (!attacker || !defender) return;
      const direction = attackerKey === "Defender" ? -1 : 1;
      const distance = Math.abs(defender.sprite.x - attacker.sprite.x);
      const lungeOffset = Math.min(220, distance * 0.6);
      const startX = attacker.originX;
      const startY = attacker.originY;
      const targetX = startX + direction * lungeOffset;
      const targetY = startY - 15;
      const damageValue = damage ?? 0;
      const isCritical = (evt == null ? void 0 : evt.isCritical) === true || (evt == null ? void 0 : evt.IsCritical) === true;
      const isBlocked = (evt == null ? void 0 : evt.isBlocked) === true || (evt == null ? void 0 : evt.IsBlocked) === true;
      const isDodged = (evt == null ? void 0 : evt.isDodged) === true || (evt == null ? void 0 : evt.IsDodged) === true;
      const isBoosted = (evt == null ? void 0 : evt.isBoosted) === true || (evt == null ? void 0 : evt.IsBoosted) === true;
      this._playSound(isBlocked || isDodged ? "block" : isCritical ? "critical" : "attack");
      const auraSprite = attackerKey !== "Defender" && this.attackerAura ? this.attackerAura : null;
      const lungeDuration = isCritical ? 150 : 200;
      animateTo(this, attacker.sprite, {
        x: targetX,
        y: targetY,
        rotation: direction * (isCritical ? 18 : 12) * Math.PI / 180
      }, lungeDuration, () => {
        animateTo(this, attacker.sprite, {
          x: startX,
          y: startY,
          rotation: 0
        }, 240);
        if (auraSprite) {
          animateTo(this, auraSprite, { x: startX, y: startY }, 240);
        }
      });
      if (auraSprite) {
        animateTo(this, auraSprite, { x: targetX, y: targetY }, lungeDuration);
      }
      if (isBlocked || isDodged) {
        defender.sprite.tint = 58879;
        const id = setTimeout(() => {
          defender.sprite.tint = 16777215;
        }, 300 / this.battleSpeed);
        this._timeoutIds.push(id);
      } else {
        const defenderTintColor = isCritical ? 16711680 : 16733525;
        defender.sprite.tint = defenderTintColor;
        const id = setTimeout(() => {
          defender.sprite.tint = 16777215;
        }, 200 / this.battleSpeed);
        this._timeoutIds.push(id);
      }
      this._playSound("hit");
      if (!isBlocked && !isDodged) {
        const defenderStartX = defender.originX;
        const recoilDistance = isCritical ? 30 : 20;
        animateTo(this, defender.sprite, {
          x: defenderStartX + direction * recoilDistance
        }, isCritical ? 100 : 120, () => {
          animateTo(this, defender.sprite, { x: defenderStartX }, 100);
        });
      }
      const impactX = defender.sprite.x;
      const impactY = defender.sprite.y - defender.sprite.height * 0.4;
      const impactColor = isBlocked || isDodged ? 58879 : isCritical ? 16776960 : 16766287;
      const impactSize = isCritical ? 25 : 18;
      const impact = new PIXI.Graphics();
      impact.circle(impactX, impactY, impactSize);
      impact.fill({ color: impactColor, alpha: 0.9 });
      this.stage.addChild(impact);
      fadeOut(this, impact, isCritical ? 400 : 300, () => {
        var _a;
        (_a = this.stage) == null ? void 0 : _a.removeChild(impact);
      });
      if (!isBlocked && !isDodged) {
        const slash = new PIXI.Graphics();
        const slashColor = isCritical ? 16776960 : 16777215;
        slash.moveTo(attacker.sprite.x, attacker.sprite.y - attacker.sprite.height * 0.5);
        slash.lineTo(defender.sprite.x, defender.sprite.y - defender.sprite.height * 0.5);
        slash.stroke({ width: isCritical ? 6 : 4, color: slashColor, alpha: 0.9 });
        this.stage.addChild(slash);
        fadeOut(this, slash, isCritical ? 250 : 200, () => {
          var _a;
          (_a = this.stage) == null ? void 0 : _a.removeChild(slash);
        });
      }
      if (isDodged) {
        const dodgeText = getPooledText(this._textPool, "DODGE", {
          fontFamily: "Arial",
          fontSize: 28,
          fontWeight: "bold",
          fill: 58879,
          stroke: { color: 0, width: 4 }
        });
        if (dodgeText) {
          dodgeText.anchor.set(0.5);
          dodgeText.x = defender.sprite.x;
          dodgeText.y = defender.sprite.y - defender.sprite.height * 0.6;
          this.stage.addChild(dodgeText);
          animateTo(this, dodgeText, { y: dodgeText.y - 70, alpha: 0 }, 900, () => {
            releaseText(this._textPool, dodgeText);
          });
        }
      } else if (isBlocked) {
        const blockedText = getPooledText(this._textPool, "BLOCKED", {
          fontFamily: "Arial",
          fontSize: 28,
          fontWeight: "bold",
          fill: 58879,
          stroke: { color: 0, width: 4 }
        });
        if (blockedText) {
          blockedText.anchor.set(0.5);
          blockedText.x = defender.sprite.x;
          blockedText.y = defender.sprite.y - defender.sprite.height * 0.6;
          this.stage.addChild(blockedText);
          animateTo(this, blockedText, { y: blockedText.y - 70, alpha: 0 }, 900, () => {
            releaseText(this._textPool, blockedText);
          });
        }
      } else {
        showDamageText(this, damageValue, isCritical, defender.sprite.x, defender.sprite.y - defender.sprite.height * 0.6);
      }
      if (isBoosted) {
        const extraText = getPooledText(this._textPool, "EXTRA", {
          fontFamily: "Arial",
          fontSize: 26,
          fontWeight: "bold",
          fill: 16750592,
          stroke: { color: 0, width: 4 }
        });
        if (extraText) {
          extraText.anchor.set(0.5);
          extraText.x = attacker.sprite.x;
          extraText.y = attacker.sprite.y - attacker.sprite.height * 0.8;
          this.stage.addChild(extraText);
          animateTo(this, extraText, { y: extraText.y - 50, alpha: 0 }, 800, () => {
            releaseText(this._textPool, extraText);
          });
        }
      }
      if (!isBlocked && !isDodged) {
        const attackerText = getPooledText(this._textPool, `+${formatNum(damageValue)}`, {
          fontFamily: "Arial",
          fontSize: 18,
          fontWeight: "bold",
          fill: 5025616
        });
        if (attackerText) {
          attackerText.anchor.set(0.5);
          attackerText.x = attacker.sprite.x;
          attackerText.y = attacker.sprite.y - attacker.sprite.height * 0.6;
          this.stage.addChild(attackerText);
          animateTo(this, attackerText, { y: attackerText.y - 20, alpha: 0 }, 700, () => {
            releaseText(this._textPool, attackerText);
          });
        }
      }
    }
    /* ────────────────────── KO / Victory / Draw ────────────────────── */
    playKo(character) {
      const target = character === "Defender" ? this.characterSprites.defender : this.characterSprites.attacker;
      if (!target) return;
      this._playSound("ko");
      animateTo(this, target.sprite, {
        alpha: 0.4,
        rotation: (character === "Defender" ? 90 : -90) * Math.PI / 180,
        y: target.sprite.y + 30
      }, 600);
      const koText = new PIXI.Text({
        text: "K.O.!",
        style: {
          fontFamily: "Arial",
          fontSize: 36,
          fontWeight: "bold",
          fill: 16711680,
          stroke: { color: 0, width: 4 }
        }
      });
      koText.anchor.set(0.5);
      koText.x = target.sprite.x;
      koText.y = target.sprite.y - target.sprite.height - 30;
      koText.alpha = 0;
      this.stage.addChild(koText);
      animateTo(this, koText, { alpha: 1 }, 200, () => {
        const tid = setTimeout(() => {
          animateTo(this, koText, { alpha: 0 }, 200, () => {
            var _a;
            (_a = this.stage) == null ? void 0 : _a.removeChild(koText);
          });
        }, 400 / this.battleSpeed);
        this._timeoutIds.push(tid);
      });
    }
    showVictory(winner) {
      if (!this.stage || !this.app) return;
      const isAttackerWinner = winner === "Attacker" || winner === this.attackerName;
      const winnerSprite = isAttackerWinner ? this.characterSprites.attacker : this.characterSprites.defender;
      const winnerName = isAttackerWinner ? this.attackerName : this.defenderName;
      this._playSound("victory");
      if (winnerSprite) {
        const originalY = winnerSprite.sprite.y;
        const winnerAura = isAttackerWinner ? this.attackerAura : null;
        animateTo(this, winnerSprite.sprite, { y: originalY - 20 }, 200, () => {
          animateTo(this, winnerSprite.sprite, { y: originalY }, 200, () => {
            animateTo(this, winnerSprite.sprite, { y: originalY - 20 }, 200, () => {
              animateTo(this, winnerSprite.sprite, { y: originalY }, 200);
              if (winnerAura) animateTo(this, winnerAura, { y: originalY }, 200);
            });
            if (winnerAura) animateTo(this, winnerAura, { y: originalY - 20 }, 200);
          });
          if (winnerAura) animateTo(this, winnerAura, { y: originalY }, 200);
        });
        if (winnerAura) animateTo(this, winnerAura, { y: originalY - 20 }, 200);
      }
      const victoryText = new PIXI.Text({
        text: `${winnerName} vence!`,
        style: {
          fontFamily: "Arial",
          fontSize: 48,
          fontWeight: "bold",
          fill: 16766720,
          stroke: { color: 0, width: 6 },
          dropShadow: { color: 0, blur: 5, angle: Math.PI / 4, distance: 3 }
        }
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
          var _a;
          (_a = this.stage) == null ? void 0 : _a.removeChild(victoryText);
        });
      }, 1200 / this.battleSpeed);
      this._timeoutIds.push(tid);
    }
    showDraw() {
      if (!this.stage || !this.app) return;
      const drawText = new PIXI.Text({
        text: "Empate!",
        style: {
          fontFamily: "Arial",
          fontSize: 48,
          fontWeight: "bold",
          fill: 13421772,
          stroke: { color: 0, width: 6 }
        }
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
          var _a;
          (_a = this.stage) == null ? void 0 : _a.removeChild(drawText);
        });
      }, 1200 / this.battleSpeed);
      this._timeoutIds.push(tid);
    }
    /* ────────────────────── Battle End ─────────────────────────────── */
    finishBattle() {
      var _a;
      if (this._destroyed || this.battleFinished) return;
      this.battleFinished = true;
      if ((_a = this.dotNetRef) == null ? void 0 : _a.invokeMethodAsync) {
        this.dotNetRef.invokeMethodAsync("OnBattleFinished").catch((e) => {
          console.warn("Could not notify Blazor of battle finish:", e);
        });
      }
    }
    /* ────────────────────── Replay Controls ────────────────────────── */
    setupReplayLoop() {
      this.isPlaying = false;
      this.replayAccumulator = 0;
    }
    updateCharacterStates(eventIndex) {
      this.initializeHpFromEvents();
      for (let i = 0; i <= eventIndex && i < this.eventsList.length; i++) {
        const evt = this.eventsList[i];
        const type = getEventField(evt, "Type");
        if (type === "HPUpdate") {
          const character = getEventField(evt, "Character");
          const hp = getEventField(evt, "HP") ?? 0;
          if (character === "Attacker") this.currentHp.attacker = hp;
          else if (character === "Defender") this.currentHp.defender = hp;
        }
      }
      this.drawHpBars();
    }
    setReplayPlaying(isPlaying) {
      this.isPlaying = isPlaying;
    }
    setReplaySpeed(speed) {
      const validSpeed = speed === 5 ? 5 : 1;
      this.playbackSpeed = validSpeed;
      this._battleSpeed = validSpeed;
    }
    jumpToEvent(index) {
      if (index < 0 || index >= this.eventsList.length) return;
      this.replayIndex = index;
      this.updateCharacterStates(index);
      this.processEvent(this.eventsList[index]);
    }
    /* ────────────────────── Main Update Loop ───────────────────────── */
    update() {
      if (this._destroyed || !this.app) return;
      const deltaMs = this.app.ticker.deltaMS;
      for (const char of Object.values(this.characterSprites)) {
        const data = char.sprite.idleAnimationData;
        if (data) {
          data.breathTime += deltaMs / 1e3;
          data.scaleTime += deltaMs / 1e3;
          const breathOffset = Math.sin(data.breathTime * Math.PI / 1.8) * 8;
          char.sprite.y = data.originalY + breathOffset;
          const swayOffset = Math.sin(data.breathTime * 0.8) * 3;
          char.sprite.x = data.originalX + swayOffset;
          const scaleOffset = Math.sin(data.scaleTime * Math.PI / 2) * 0.02;
          const absScale = Math.abs(data.originalScale) * (1 + scaleOffset);
          char.sprite.scale.set(data.originalScale < 0 ? -absScale : absScale, absScale);
        }
      }
      if (this.attackerAura && !this.attackerAura.destroyed && this.characterSprites.attacker) {
        const curScaleX = this.characterSprites.attacker.sprite.scale.x;
        const curScaleY = this.characterSprites.attacker.sprite.scale.y;
        this.attackerAura.scale.set(curScaleX < 0 ? curScaleX * 1.25 : curScaleX * 1.25, curScaleY * 1.25);
        const time = performance.now() / 1e3;
        this.attackerAura.alpha = 0.65 + Math.sin(time * 1.2) * 0.15;
      }
      if (this.interactiveMode && !this.battleFinished && this.isPlaying) {
        const simDelta = deltaMs * this.battleSpeed;
        this.currentSimTime += simDelta;
        if (this.currentHp.attacker > 0) {
          if (this.currentHp.defender <= 0) {
            this.speedBarTimers.attacker = this.actionTime.attacker * 1e3;
          } else if (this._playerAttackPending) ;
          else {
            this.speedBarTimers.attacker = Math.max(0, this.speedBarTimers.attacker - simDelta);
            if (this.speedBarTimers.attacker <= 0) {
              this._playerAttackPending = true;
              this.requestPlayerAutoAttack();
            }
          }
        }
        if (this.currentHp.defender > 0) {
          if (this.currentHp.attacker <= 0) {
            this.speedBarTimers.defender = this.actionTime.defender * 1e3;
          } else if (this._enemyAttackPending) ;
          else {
            this.speedBarTimers.defender = Math.max(0, this.speedBarTimers.defender - simDelta);
            if (this.speedBarTimers.defender <= 0) {
              this._enemyAttackPending = true;
              this.requestEnemyAttack();
            }
          }
        }
        this.drawSpeedBars();
        return;
      }
      if (this.mode === "live" && !this.battleFinished && this.isPlaying && this.battleEvents) {
        const simDelta = deltaMs * this.battleSpeed;
        this.currentSimTime += simDelta;
        if (this.currentHp.attacker > 0) {
          this.speedBarTimers.attacker = Math.max(0, this.speedBarTimers.attacker - simDelta);
        }
        if (this.currentHp.defender > 0) {
          this.speedBarTimers.defender = Math.max(0, this.speedBarTimers.defender - simDelta);
        }
        while (this.currentEventIndex < this.battleEvents.length) {
          const eventData = this.battleEvents[this.currentEventIndex];
          if (eventData.simTime > this.currentSimTime) break;
          const evt = eventData.event;
          const type = getEventField(evt, "Type");
          if (type === "Attack") {
            const attackerField = getEventField(evt, "Attacker");
            if (attackerField === "Attacker") {
              this.speedBarTimers.attacker = this.actionTime.attacker * 1e3;
            } else if (attackerField === "Defender") {
              this.speedBarTimers.defender = this.actionTime.defender * 1e3;
            }
          }
          this.processEvent(evt);
          this.currentEventIndex++;
          if (type === "Victory" || type === "Draw") {
            this.isPlaying = false;
            const tid = setTimeout(() => this.finishBattle(), 2e3 / this.battleSpeed);
            this._timeoutIds.push(tid);
            break;
          }
        }
        this.drawSpeedBars();
      }
      if (this.mode === "replay" && this.isPlaying) {
        this.replayAccumulator += deltaMs * this.playbackSpeed;
        if (this.replayAccumulator >= 1e3) {
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
    destroy() {
      var _a, _b, _c;
      this._destroyed = true;
      this.battleFinished = true;
      this.isPlaying = false;
      stopMusic(this.musicState);
      this.musicState = null;
      if (this._onContextLost && ((_a = this.app) == null ? void 0 : _a.canvas)) {
        this.app.canvas.removeEventListener("webglcontextlost", this._onContextLost);
      }
      if (this._onVisibilityChange) {
        document.removeEventListener("visibilitychange", this._onVisibilityChange);
      }
      for (const id of this._timeoutIds) clearTimeout(id);
      for (const id of this._rafIds) cancelAnimationFrame(id);
      this._timeoutIds = [];
      this._rafIds = [];
      destroyTextPool(this._textPool);
      if (this.app) {
        this.app.ticker.stop();
        while (this.stage && this.stage.children && this.stage.children.length > 0) {
          const child = this.stage.children[0];
          this.stage.removeChild(child);
          if (child.destroy) {
            try {
              child.destroy({ children: true, texture: false });
            } catch {
            }
          }
        }
        try {
          if (this.app.renderer) {
            try {
              (_c = (_b = this.app.renderer).destroy) == null ? void 0 : _c.call(_b);
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
    }
  }
  let activeScene = null;
  let musicState = null;
  function resolveAttackerName(battleData) {
    if (!battleData || Array.isArray(battleData)) return "Attacker";
    const d = battleData;
    return d.attackerName ?? d.AttackerName ?? "Attacker";
  }
  function resolveDefenderName(battleData) {
    if (!battleData || Array.isArray(battleData)) return "Defender";
    const d = battleData;
    return d.defenderName ?? d.DefenderName ?? "Defender";
  }
  function createGame(hostId, battleData, mode) {
    const container = document.getElementById(hostId);
    if (!container) return null;
    const events = resolveEvents(battleData);
    const dotNetRef = resolveDotNetRef(battleData);
    const attackerName = resolveAttackerName(battleData);
    const defenderName = resolveDefenderName(battleData);
    const d = battleData;
    const hasShotBuff = d.HasShotBuff ?? d.hasShotBuff ?? false;
    const interactiveMode = d.InteractiveMode ?? d.interactiveMode ?? false;
    const playerHP = d.PlayerHP ?? d.playerHP ?? null;
    const playerMaxHP = d.PlayerMaxHP ?? d.playerMaxHP ?? null;
    const playerActionTime = d.PlayerActionTime ?? d.playerActionTime ?? null;
    const enemies = d.Enemies ?? d.enemies ?? [];
    const attackerSpritePath = d.AttackerSpritePath ?? d.attackerSpritePath ?? "";
    const defenderSpritePath = d.DefenderSpritePath ?? d.defenderSpritePath ?? "";
    return new ArenaBattleScene(container, {
      events,
      dotNetRef,
      mode,
      attackerName,
      defenderName,
      AttackerSpritePath: attackerSpritePath,
      DefenderSpritePath: defenderSpritePath,
      HasShotBuff: hasShotBuff,
      InteractiveMode: interactiveMode,
      PlayerHP: playerHP ?? void 0,
      PlayerMaxHP: playerMaxHP ?? void 0,
      PlayerActionTime: playerActionTime ?? void 0,
      Enemies: enemies
    });
  }
  function destroyBattle() {
    if (activeScene) {
      activeScene.destroy();
      activeScene = null;
    }
    stopMusic(musicState);
    musicState = null;
  }
  function applyBattleSpeed(battleData) {
    if (!activeScene) return;
    const d = battleData;
    const speed = d.BattleSpeed ?? d.battleSpeed ?? 1;
    if (speed !== 1) {
      const allowedSpeeds = [1, 5];
      const validSpeed = allowedSpeeds.includes(speed) ? speed : Math.min(5, Math.max(1, Math.round(speed)));
      activeScene._battleSpeed = validSpeed;
    }
  }
  window.myTunoGame = {
    startBattle: (hostId, battleData) => {
      destroyBattle();
      activeScene = createGame(hostId, battleData, "live");
      applyBattleSpeed(battleData);
    },
    startReplay: (hostId, battleData) => {
      destroyBattle();
      activeScene = createGame(hostId, battleData, "replay");
      applyBattleSpeed(battleData);
    },
    setReplayPlaying: (isPlaying) => {
      activeScene == null ? void 0 : activeScene.setReplayPlaying(isPlaying);
    },
    setReplaySpeed: (speed) => {
      activeScene == null ? void 0 : activeScene.setReplaySpeed(speed);
    },
    jumpToReplayEvent: (index) => {
      activeScene == null ? void 0 : activeScene.jumpToEvent(index);
    },
    setSpeed: (speed) => {
      if (activeScene) {
        const allowedSpeeds = [1, 5];
        const validSpeed = allowedSpeeds.includes(speed) ? speed : Math.min(5, Math.max(1, Math.round(speed)));
        activeScene._battleSpeed = validSpeed;
      }
    },
    toggleAudio: () => {
      return (activeScene == null ? void 0 : activeScene.toggleAudio()) ?? false;
    },
    setVolume: (musicVol, sfxVol) => {
      activeScene == null ? void 0 : activeScene.setVolume(musicVol, sfxVol);
    },
    destroyBattle
  };
})();
//# sourceMappingURL=pixiBattle.js.map
