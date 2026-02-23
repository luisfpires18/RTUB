var __defProp = Object.defineProperty;
var __defNormalProp = (obj, key, value) => key in obj ? __defProp(obj, key, { enumerable: true, configurable: true, writable: true, value }) : obj[key] = value;
var __publicField = (obj, key, value) => __defNormalProp(obj, typeof key !== "symbol" ? key + "" : key, value);
(function() {
  "use strict";
  const SESSION_CACHE_BUST = `?v=${Date.now()}`;
  const loadedAssetAliases = /* @__PURE__ */ new Set();
  const audioBufferCache = {};
  const audioCacheBuster = `?v=${Date.now()}`;
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
  function playSpellSound(ctx, attackId, sfxVolume) {
    if (!sfxVolume) return;
    if (ctx.state === "suspended") ctx.resume();
    const t = ctx.currentTime;
    const vol = sfxVolume;
    const playNote = (freq, type, start, dur, v = 0.3) => {
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.connect(g);
      g.connect(ctx.destination);
      osc.frequency.value = freq;
      osc.type = type;
      g.gain.setValueAtTime(vol * v, t + start);
      g.gain.exponentialRampToValueAtTime(0.01, t + start + dur);
      osc.start(t + start);
      osc.stop(t + start + dur);
    };
    switch (attackId) {
      case "heavy_attack":
        playNote(120, "sawtooth", 0, 0.15, 0.5);
        playNote(180, "square", 0.03, 0.12, 0.4);
        break;
      case "guitarra_barrage":
        for (let i = 0; i < 6; i++) {
          playNote(82 + i * 15, "sawtooth", i * 0.07, 0.08, 0.4);
          playNote(165 + i * 10, "square", i * 0.07 + 0.03, 0.06, 0.25);
        }
        break;
      case "bandolim_swiftchord":
        playNote(587, "triangle", 0, 0.12, 0.4);
        playNote(784, "triangle", 0.02, 0.1, 0.35);
        playNote(988, "sine", 0.04, 0.08, 0.3);
        break;
      case "cavaquinho_paralysis":
        for (let i = 0; i < 5; i++)
          playNote(800 + Math.random() * 400, "square", i * 0.06, 0.05, 0.3);
        playNote(200, "sawtooth", 0.35, 0.2, 0.4);
        break;
      case "acordeao_fear":
        playNote(130, "sawtooth", 0, 0.6, 0.4);
        playNote(138, "sawtooth", 0, 0.55, 0.35);
        playNote(98, "square", 0.1, 0.4, 0.3);
        playNote(65, "triangle", 0.2, 0.4, 0.25);
        break;
      case "contrabaixo_sonicboom": {
        const osc = ctx.createOscillator();
        const g = ctx.createGain();
        osc.connect(g);
        g.connect(ctx.destination);
        osc.type = "sine";
        osc.frequency.setValueAtTime(110, t);
        osc.frequency.exponentialRampToValueAtTime(35, t + 0.5);
        g.gain.setValueAtTime(vol * 0.6, t);
        g.gain.exponentialRampToValueAtTime(0.01, t + 0.5);
        osc.start(t);
        osc.stop(t + 0.5);
        playNote(55, "triangle", 0, 0.4, 0.3);
        playNote(220, "square", 0.05, 0.15, 0.2);
        break;
      }
      case "percussao_combo":
        playNote(200, "square", 0, 0.06, 0.5);
        playNote(300, "square", 0.08, 0.06, 0.4);
        playNote(250, "square", 0.16, 0.06, 0.45);
        playNote(400, "square", 0.24, 0.08, 0.5);
        playNote(150, "triangle", 0, 0.3, 0.25);
        break;
      case "pandeireta_boomerang":
        for (let i = 0; i < 8; i++) {
          playNote(400 + i * 50, "sine", i * 0.04, 0.06, 0.35);
        }
        for (let i = 0; i < 8; i++) {
          playNote(750 - i * 50, "sine", 0.32 + i * 0.04, 0.06, 0.3);
        }
        break;
      case "estandarte_rally":
        playNote(262, "triangle", 0, 0.15, 0.4);
        playNote(330, "triangle", 0.12, 0.15, 0.4);
        playNote(392, "triangle", 0.24, 0.2, 0.45);
        playNote(196, "sine", 0, 0.5, 0.2);
        break;
      case "violino_sleep":
        playNote(660, "sine", 0, 0.3, 0.35);
        playNote(600, "sine", 0.15, 0.3, 0.3);
        playNote(550, "sine", 0.3, 0.3, 0.25);
        playNote(500, "sine", 0.45, 0.35, 0.2);
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
    const adjustedDuration = speed > 0 ? duration / speed : duration;
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
  const STATUS_LABELS = {
    sleep: "💤 SLEEP",
    bleed: "🩸 BLEED",
    slow: "🐌 SLOW",
    vulnerable: "⚡ VULN",
    powerboost: "💪 POWER UP",
    haste: "⚡ HASTE",
    shield: "🛡️ SHIELD",
    defensebreak: "💥 DEF BREAK",
    defenseboost: "🛡️ DEF UP",
    regen: "💚 REGEN"
  };
  const STATUS_COLORS = {
    sleep: 10053375,
    bleed: 16724787,
    slow: 6724044,
    vulnerable: 16755200,
    powerboost: 16737792,
    haste: 65416,
    shield: 4491519,
    defensebreak: 16729156,
    defenseboost: 4491519,
    regen: 4521796
  };
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
    animateTo(owner, floatText, { y: floatText.y - 70, alpha: 0 }, 900, () => {
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
    damageText.x = x;
    damageText.y = y;
    owner.stage.addChild(damageText);
    animateTo(owner, damageText, { y: damageText.y - floatDistance, alpha: 0 }, duration, () => {
      releaseText(owner._textPool, damageText);
    });
  }
  function showEffectLabel(owner, effectName, sprite) {
    const label = STATUS_LABELS[effectName] || effectName.toUpperCase();
    const color = STATUS_COLORS[effectName] || 16777215;
    showFloatingText(
      owner,
      label,
      sprite.x,
      sprite.y - (sprite.height || 40) - 30,
      color
    );
  }
  function screenShake(owner) {
    if (!owner.stage) return;
    const intensity = 4;
    const originalX = owner.stage.x;
    const originalY = owner.stage.y;
    let count = 0;
    const shake = () => {
      if (count >= 6 || !owner.stage) {
        if (owner.stage) {
          owner.stage.x = originalX;
          owner.stage.y = originalY;
        }
        return;
      }
      owner.stage.x = originalX + (Math.random() - 0.5) * intensity * 2;
      owner.stage.y = originalY + (Math.random() - 0.5) * intensity * 2;
      count++;
      const id = setTimeout(shake, 30);
      owner._timeoutIds.push(id);
    };
    shake();
  }
  function playSpellVfx(owner, vfxType, color, source, target) {
    if (!source || !target || !owner.stage) return;
    const srcX = source.x;
    const srcY = source.y - (source.height || 40) / 2;
    const tgtX = target.x;
    const tgtY = target.y - (target.height || 40) / 2;
    switch (vfxType) {
      case 0:
        createProjectileVfx(owner, color, srcX, srcY, tgtX, tgtY);
        break;
      case 1:
        createBeamVfx(owner, color, tgtX, tgtY);
        break;
      case 2:
        createAoeVfx(owner, color, tgtX, tgtY);
        break;
      case 4:
        createMeleeStrikeVfx(owner, color, tgtX, tgtY);
        break;
      case 5:
        createSoundWaveVfx(owner, color, srcX, srcY, tgtX, tgtY);
        break;
      case 6:
        createMusicNotesVfx(owner, color, srcX, srcY, tgtX, tgtY);
        break;
      default:
        createProjectileVfx(owner, color, srcX, srcY, tgtX, tgtY);
    }
  }
  function createProjectileVfx(owner, color, srcX, srcY, tgtX, tgtY) {
    if (!owner.stage) return;
    const proj = new PIXI.Graphics();
    proj.circle(0, 0, 8);
    proj.fill({ color, alpha: 0.9 });
    proj.x = srcX;
    proj.y = srcY;
    owner.stage.addChild(proj);
    const glow = new PIXI.Graphics();
    glow.circle(0, 0, 14);
    glow.fill({ color, alpha: 0.3 });
    glow.x = srcX;
    glow.y = srcY;
    owner.stage.addChild(glow);
    const duration = 350 / owner.battleSpeed;
    const startTime = Date.now();
    const animate = () => {
      const t = Math.min((Date.now() - startTime) / duration, 1);
      proj.x = srcX + (tgtX - srcX) * t;
      proj.y = srcY + (tgtY - srcY) * t;
      glow.x = proj.x;
      glow.y = proj.y;
      glow.alpha = 0.3 * (1 - t * 0.5);
      if (t < 1) {
        requestAnimationFrame(animate);
      } else {
        const flash = new PIXI.Graphics();
        flash.circle(0, 0, 20);
        flash.fill({ color, alpha: 0.8 });
        flash.x = tgtX;
        flash.y = tgtY;
        owner.stage.addChild(flash);
        animateTo(owner, flash, { alpha: 0, scale: 2 }, 200, () => {
          if (flash.parent) flash.parent.removeChild(flash);
          flash.destroy();
        });
        if (proj.parent) proj.parent.removeChild(proj);
        proj.destroy();
        if (glow.parent) glow.parent.removeChild(glow);
        glow.destroy();
      }
    };
    requestAnimationFrame(animate);
  }
  function createBeamVfx(owner, color, tgtX, tgtY) {
    if (!owner.stage) return;
    const beam = new PIXI.Graphics();
    beam.rect(-4, -200, 8, 200);
    beam.fill({ color, alpha: 0.8 });
    beam.x = tgtX;
    beam.y = tgtY;
    beam.alpha = 0;
    owner.stage.addChild(beam);
    animateTo(owner, beam, { alpha: 1 }, 100, () => {
      animateTo(owner, beam, { alpha: 0 }, 400, () => {
        if (beam.parent) beam.parent.removeChild(beam);
        beam.destroy();
      });
    });
  }
  function createAoeVfx(owner, color, tgtX, tgtY) {
    if (!owner.stage) return;
    const ring = new PIXI.Graphics();
    ring.circle(0, 0, 10);
    ring.stroke({ color, width: 3, alpha: 0.9 });
    ring.x = tgtX;
    ring.y = tgtY;
    owner.stage.addChild(ring);
    animateTo(owner, ring, { scale: 6, alpha: 0 }, 500, () => {
      if (ring.parent) ring.parent.removeChild(ring);
      ring.destroy();
    });
  }
  function createMeleeStrikeVfx(owner, color, tgtX, tgtY) {
    if (!owner.stage) return;
    const slash = new PIXI.Graphics();
    slash.moveTo(-15, -15);
    slash.lineTo(15, 15);
    slash.moveTo(15, -15);
    slash.lineTo(-15, 15);
    slash.stroke({ color, width: 4, alpha: 0.9 });
    slash.x = tgtX;
    slash.y = tgtY;
    owner.stage.addChild(slash);
    animateTo(owner, slash, { alpha: 0, scale: 2 }, 350, () => {
      if (slash.parent) slash.parent.removeChild(slash);
      slash.destroy();
    });
  }
  function createSoundWaveVfx(owner, color, srcX, srcY, tgtX, tgtY) {
    if (!owner.stage) return;
    const midX = (srcX + tgtX) / 2;
    const midY = (srcY + tgtY) / 2;
    for (let i = 0; i < 3; i++) {
      const ring = new PIXI.Graphics();
      ring.circle(0, 0, 12);
      ring.stroke({ color, width: 3, alpha: 0.8 });
      ring.x = midX;
      ring.y = midY;
      ring.scale.set(0.3);
      ring.alpha = 0;
      owner.stage.addChild(ring);
      const delay = i * 120 / owner.battleSpeed;
      const capturedI = i;
      const id = setTimeout(() => {
        ring.alpha = 0.8;
        animateTo(owner, ring, { alpha: 0, scale: 3 + capturedI }, 500 / owner.battleSpeed, () => {
          if (ring.parent) ring.parent.removeChild(ring);
          ring.destroy();
        });
      }, delay);
      owner._timeoutIds.push(id);
    }
  }
  function createMusicNotesVfx(owner, color, srcX, srcY, tgtX, tgtY) {
    if (!owner.stage) return;
    const notes = ["♪", "♫", "♩", "♬"];
    for (let i = 0; i < 5; i++) {
      const note = new PIXI.Text({
        text: notes[i % notes.length],
        style: { fontSize: 18 + Math.random() * 8, fill: color, fontFamily: "serif" }
      });
      note.anchor.set(0.5);
      note.x = srcX + (Math.random() - 0.5) * 30;
      note.y = srcY + (Math.random() - 0.5) * 20;
      note.alpha = 0;
      owner.stage.addChild(note);
      const delay = i * 80 / owner.battleSpeed;
      const endX = tgtX + (Math.random() - 0.5) * 40;
      const endY = tgtY - 20 + (Math.random() - 0.5) * 30;
      const noteStartX = note.x;
      const noteStartY = note.y;
      const id = setTimeout(() => {
        note.alpha = 1;
        const duration = 450 / owner.battleSpeed;
        const startTime = Date.now();
        const animateNote = () => {
          const t = Math.min((Date.now() - startTime) / duration, 1);
          note.x = noteStartX + (endX - noteStartX) * t;
          note.y = noteStartY + (endY - noteStartY) * t - Math.sin(t * Math.PI) * 20;
          note.alpha = 1 - t * 0.6;
          note.rotation = Math.sin(t * Math.PI * 2) * 0.3;
          if (t < 1) {
            requestAnimationFrame(animateNote);
          } else {
            if (note.parent) note.parent.removeChild(note);
            note.destroy();
          }
        };
        animateNote();
      }, delay);
      owner._timeoutIds.push(id);
    }
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
      __publicField(this, "spells");
      __publicField(this, "interactivePlayerHP");
      __publicField(this, "interactivePlayerMaxHP");
      __publicField(this, "interactivePlayerActionTime");
      __publicField(this, "interactiveEnemies");
      __publicField(this, "_playerAttackPending", false);
      __publicField(this, "_enemyAttackPending", false);
      __publicField(this, "_spellPending", false);
      __publicField(this, "_cooldownTickAccum", 0);
      __publicField(this, "spellButtons", []);
      __publicField(this, "spellCooldowns", {});
      __publicField(this, "spellBarContainer", null);
      // Cleanup trackers (VfxOwner requirement)
      __publicField(this, "_timeoutIds", []);
      __publicField(this, "_rafIds", []);
      __publicField(this, "_textPool", { pool: [] });
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
      this.interactiveMode = data.InteractiveMode ?? data.interactiveMode ?? false;
      this.spells = data.Spells ?? data.spells ?? [];
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
    _playSpellSound(attackId) {
      if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
      playSpellSound(this.audioContext, attackId, this.sfxVolume);
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
      if (!loadedAssetAliases.has("attackerSprite")) {
        toLoad.push({ alias: "attackerSprite", src: SPRITE_PATHS.attacker + SESSION_CACHE_BUST });
      }
      if (!loadedAssetAliases.has("defenderSprite")) {
        toLoad.push({ alias: "defenderSprite", src: SPRITE_PATHS.defender + SESSION_CACHE_BUST });
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
        this.createSpellBar();
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
      const atkSprite = PIXI.Sprite.from("attackerSprite");
      atkSprite.anchor.set(0.5, 1);
      const atkScale = this.getSpriteScale(atkSprite, maxSpriteHeight);
      atkSprite.scale.set(atkScale);
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
        const aura = PIXI.Sprite.from("attackerSprite");
        aura.anchor.set(0.5, 1);
        aura.scale.set(atkScale * 1.12);
        aura.x = atkX;
        aura.y = atkY;
        aura.tint = 4504575;
        aura.alpha = 0.7;
        aura.filters = [new PIXI.BlurFilter({ strength: 8 })];
        const spriteIdx = this.stage.getChildIndex(atkSprite);
        this.stage.addChildAt(aura, spriteIdx);
        this.attackerAura = aura;
      }
      const defSprite = PIXI.Sprite.from("defenderSprite");
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
    /* ────────────────────── Spell Bar UI ───────────────────────────── */
    createSpellBar() {
      if (!this.spells || this.spells.length === 0 || !this.app || !this.stage) return;
      const { width, height } = this.app.screen;
      const isMobile = width < 768;
      const btnSize = isMobile ? 52 : 68;
      const btnGap = isMobile ? 10 : 14;
      const totalWidth = this.spells.length * btnSize + (this.spells.length - 1) * btnGap;
      const startX = (width - totalWidth) / 2;
      const barY = height - btnSize - 8;
      this.spellBarContainer = new PIXI.Container();
      this.stage.addChild(this.spellBarContainer);
      const backdrop = new PIXI.Graphics();
      backdrop.roundRect(startX - 8, barY - 6, totalWidth + 16, btnSize + 12, 8);
      backdrop.fill({ color: 0, alpha: 0.5 });
      this.spellBarContainer.addChild(backdrop);
      this.spellButtons = [];
      for (let i = 0; i < this.spells.length; i++) {
        const spell = this.spells[i];
        const attackId = spell.attackId ?? spell.AttackId ?? "";
        const name = spell.name ?? spell.Name ?? attackId;
        const icon = spell.icon ?? spell.Icon ?? "⚡";
        const cooldown = spell.cooldownSeconds ?? spell.CooldownSeconds ?? 10;
        const x = startX + i * (btnSize + btnGap);
        const btnContainer = new PIXI.Container();
        btnContainer.x = x;
        btnContainer.y = barY;
        const bg = new PIXI.Graphics();
        bg.roundRect(0, 0, btnSize, btnSize, 6);
        bg.fill({ color: 2763338, alpha: 0.9 });
        bg.stroke({ color: 6710954, width: 2 });
        btnContainer.addChild(bg);
        const iconText = new PIXI.Text({
          text: icon,
          style: { fontSize: isMobile ? 22 : 28, fontFamily: "Arial, sans-serif", fill: 16777215 }
        });
        iconText.anchor.set(0.5);
        iconText.x = btnSize / 2;
        iconText.y = btnSize / 2 - 4;
        btnContainer.addChild(iconText);
        const nameText = new PIXI.Text({
          text: name.length > 6 ? name.substring(0, 6) : name,
          style: { fontSize: isMobile ? 8 : 10, fontFamily: "Arial, sans-serif", fill: 13421772 }
        });
        nameText.anchor.set(0.5);
        nameText.x = btnSize / 2;
        nameText.y = btnSize - 6;
        btnContainer.addChild(nameText);
        const cdOverlay = new PIXI.Graphics();
        cdOverlay.roundRect(0, 0, btnSize, btnSize, 6);
        cdOverlay.fill({ color: 0, alpha: 0.7 });
        cdOverlay.visible = false;
        btnContainer.addChild(cdOverlay);
        const cdText = new PIXI.Text({
          text: "",
          style: { fontSize: 16, fontFamily: "Arial, sans-serif", fontWeight: "bold", fill: 16777215 }
        });
        cdText.anchor.set(0.5);
        cdText.x = btnSize / 2;
        cdText.y = btnSize / 2;
        cdText.visible = false;
        btnContainer.addChild(cdText);
        btnContainer.eventMode = "static";
        btnContainer.cursor = "pointer";
        btnContainer.on("pointerdown", () => this.onSpellButtonClick(attackId));
        this.spellBarContainer.addChild(btnContainer);
        this.spellButtons.push({
          container: btnContainer,
          bg,
          iconText,
          nameText,
          cdOverlay,
          cdText,
          attackId,
          cooldownSeconds: cooldown,
          spell
        });
      }
    }
    /* ────────────────── Interactive Battle Flow ────────────────────── */
    startInteractiveBattle() {
      this.battleStartTime = Date.now();
      this.currentSimTime = 0;
      this.battleFinished = false;
      this.isPlaying = true;
      this._playerAttackPending = false;
      this._enemyAttackPending = false;
      this._spellPending = false;
      this._cooldownTickAccum = 0;
    }
    onSpellButtonClick(attackId) {
      if (this.battleFinished || !this.isPlaying) return;
      if (this._spellPending) return;
      const cd = this.spellCooldowns[attackId] ?? 0;
      if (cd > 0) return;
      this._spellPending = true;
      this.requestPlayerSpell(attackId);
    }
    async requestPlayerAutoAttack() {
      if (!this.dotNetRef || this.battleFinished) {
        this._playerAttackPending = false;
        return;
      }
      try {
        const json = await this.dotNetRef.invokeMethodAsync("OnPlayerAutoAttack");
        if (json) this.processServerResult(JSON.parse(json));
      } catch (e) {
        console.warn("OnPlayerAutoAttack error:", e);
      } finally {
        this._playerAttackPending = false;
      }
    }
    async requestEnemyAttack() {
      if (!this.dotNetRef || this.battleFinished) {
        this._enemyAttackPending = false;
        return;
      }
      try {
        const json = await this.dotNetRef.invokeMethodAsync("OnEnemyAttack", 0);
        if (json) this.processServerResult(JSON.parse(json));
      } catch (e) {
        console.warn("OnEnemyAttack error:", e);
      } finally {
        this._enemyAttackPending = false;
      }
    }
    async requestPlayerSpell(attackId) {
      if (!this.dotNetRef || this.battleFinished) {
        this._spellPending = false;
        return;
      }
      try {
        const json = await this.dotNetRef.invokeMethodAsync("OnPlayerSpell", attackId);
        if (json) this.processServerResult(JSON.parse(json));
      } catch (e) {
        console.warn("OnPlayerSpell error:", e);
      } finally {
        this._spellPending = false;
      }
    }
    async requestTickCooldowns(elapsedSeconds) {
      if (!this.dotNetRef || this.battleFinished) return;
      try {
        const json = await this.dotNetRef.invokeMethodAsync("OnTickCooldowns", elapsedSeconds);
        if (json) {
          const data = JSON.parse(json);
          const spellCooldowns = data.spells ?? data;
          for (const [id, remaining] of Object.entries(spellCooldowns)) {
            this.spellCooldowns[id] = remaining;
          }
        }
      } catch (e) {
        console.warn("OnTickCooldowns error:", e);
      }
    }
    processServerResult(result) {
      if (!result) return;
      const events = result.events ?? result.Events ?? [];
      for (const evt of events) {
        this.processInteractiveEvent(evt);
      }
      const cooldowns = result.spellCooldowns ?? result.SpellCooldowns;
      if (cooldowns) {
        for (const [id, remaining] of Object.entries(cooldowns)) {
          this.spellCooldowns[id] = remaining;
        }
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
      const attackId = evt.attackId ?? evt.AttackId;
      switch (evtType) {
        case "HPUpdate":
          this.processEvent(evt);
          break;
        case "Attack":
          if (attackId) {
            this.handleSpellAttack(evt);
          } else {
            this.processEvent(evt);
          }
          break;
        case "KO":
          this.processEvent(evt);
          break;
        case "StatusEffect":
          this.handleStatusEffect(evt);
          break;
      }
    }
    /* ────────────────────── Spell / Status VFX ─────────────────────── */
    handleSpellAttack(evt) {
      const attacker = evt.attacker ?? evt.Attacker ?? "";
      const defender = evt.defender ?? evt.Defender ?? "";
      const damage = evt.damage ?? evt.Damage ?? 0;
      const isCritical = evt.isCritical ?? evt.IsCritical ?? false;
      const vfxType = evt.vfxType ?? evt.VfxType;
      const vfxColor = evt.vfxColor ?? evt.VfxColor ?? "#ff6600";
      const doScreenShake = evt.screenShake ?? evt.ScreenShake ?? false;
      const visualHint = evt.visualHint ?? evt.VisualHint;
      const abilityName = evt.abilityName ?? evt.AbilityName ?? "Spell";
      const effectName = evt.effectName ?? evt.EffectName;
      const attackId = evt.attackId ?? evt.AttackId;
      const color = typeof vfxColor === "string" && vfxColor.startsWith("#") ? parseInt(vfxColor.replace("#", ""), 16) : typeof vfxColor === "number" ? vfxColor : 16737792;
      if (doScreenShake || visualHint === "screenShake") screenShake(this);
      const atkChar = attacker === "Attacker" ? this.characterSprites.attacker : this.characterSprites.defender;
      const defChar = defender === "Defender" ? this.characterSprites.defender : this.characterSprites.attacker;
      if (!atkChar || !defChar) return;
      const atkSpr = atkChar.sprite;
      const defSpr = defChar.sprite;
      if (attacker === "Attacker") {
        const startX = atkChar.originX;
        animateTo(this, atkSpr, { x: startX + 40 }, 120, () => {
          animateTo(this, atkSpr, { x: startX }, 200);
        });
        showFloatingText(this, abilityName.toUpperCase(), atkSpr.x, atkSpr.y - atkSpr.height * 0.8, color);
        if (defender === "Attacker") {
          playBuffVfx(this, atkSpr, color);
          if (damage < 0) {
            showFloatingText(this, `+${formatNum(Math.abs(damage))}`, atkSpr.x, atkSpr.y - atkSpr.height * 0.6, 4521796);
          }
          if (effectName) showEffectLabel(this, effectName, atkSpr);
        } else {
          playSpellVfx(this, vfxType, color, atkSpr, defSpr);
          defSpr.tint = isCritical ? 16711680 : 16733525;
          const id = setTimeout(
            () => {
              if (!defSpr.destroyed) defSpr.tint = 16777215;
            },
            200 / this.battleSpeed
          );
          this._timeoutIds.push(id);
          if (damage > 0) {
            showDamageText(this, damage, isCritical, defSpr.x, defSpr.y - defSpr.height * 0.6);
          }
          if (effectName) showEffectLabel(this, effectName, defSpr);
        }
        if (attackId) {
          this._playSpellSound(attackId);
        } else {
          this._playSound(isCritical ? "critical" : "attack");
        }
      }
    }
    handleStatusEffect(evt) {
      var _a, _b;
      const character = evt.character ?? evt.Character ?? "";
      const effectName = evt.effectName ?? evt.EffectName ?? "";
      const damage = evt.damage ?? evt.Damage ?? 0;
      const target = character === "Attacker" ? (_a = this.characterSprites.attacker) == null ? void 0 : _a.sprite : (_b = this.characterSprites.defender) == null ? void 0 : _b.sprite;
      if (!target) return;
      showEffectLabel(this, effectName, target);
      if (damage > 0) {
        showDamageText(this, damage, false, target.x, target.y - target.height * 0.6);
      }
    }
    updateSpellCooldownVisuals() {
      for (const btn of this.spellButtons) {
        const cd = this.spellCooldowns[btn.attackId] ?? 0;
        if (cd > 0) {
          btn.cdOverlay.visible = true;
          btn.cdText.visible = true;
          btn.cdText.text = Math.ceil(cd).toString();
          btn.container.cursor = "not-allowed";
          btn.bg.alpha = 0.5;
        } else {
          btn.cdOverlay.visible = false;
          btn.cdText.visible = false;
          btn.container.cursor = "pointer";
          btn.bg.alpha = 0.9;
        }
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
      });
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
        animateTo(this, winnerSprite.sprite, { y: originalY - 20 }, 200, () => {
          animateTo(this, winnerSprite.sprite, { y: originalY }, 200, () => {
            animateTo(this, winnerSprite.sprite, { y: originalY - 20 }, 200, () => {
              animateTo(this, winnerSprite.sprite, { y: originalY }, 200);
            });
          });
        });
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
      if (this.battleFinished) return;
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
      if (!this.app) return;
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
          const newScale = data.originalScale * (1 + scaleOffset);
          char.sprite.scale.set(newScale);
        }
      }
      if (this.attackerAura && !this.attackerAura.destroyed && this.characterSprites.attacker) {
        const att = this.characterSprites.attacker;
        this.attackerAura.x = att.sprite.x;
        this.attackerAura.y = att.sprite.y;
        const curScale = att.sprite.scale.x;
        this.attackerAura.scale.set(curScale * 1.12);
        const time = performance.now() / 1e3;
        this.attackerAura.alpha = 0.55 + Math.sin(time * 1.2) * 0.2;
      }
      if (this.interactiveMode && !this.battleFinished && this.isPlaying) {
        const simDelta = deltaMs * this.battleSpeed;
        this.currentSimTime += simDelta;
        if (this.currentHp.attacker > 0) {
          this.speedBarTimers.attacker = Math.max(0, this.speedBarTimers.attacker - simDelta);
          if (this.speedBarTimers.attacker <= 0 && !this._playerAttackPending) {
            this._playerAttackPending = true;
            this.speedBarTimers.attacker = this.actionTime.attacker * 1e3;
            this.requestPlayerAutoAttack();
          }
        }
        if (this.currentHp.defender > 0) {
          this.speedBarTimers.defender = Math.max(0, this.speedBarTimers.defender - simDelta);
          if (this.speedBarTimers.defender <= 0 && !this._enemyAttackPending) {
            this._enemyAttackPending = true;
            this.speedBarTimers.defender = this.actionTime.defender * 1e3;
            this.requestEnemyAttack();
          }
        }
        this._cooldownTickAccum += simDelta;
        if (this._cooldownTickAccum >= 200) {
          const elapsed = this._cooldownTickAccum / 1e3;
          this._cooldownTickAccum = 0;
          for (const id of Object.keys(this.spellCooldowns)) {
            this.spellCooldowns[id] = Math.max(0, this.spellCooldowns[id] - elapsed);
          }
          this.updateSpellCooldownVisuals();
          this.requestTickCooldowns(elapsed);
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
      var _a;
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
      this.spellBarContainer = null;
      this.spellButtons = [];
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
          this.app.destroy(false);
        } catch (e) {
          console.warn("Error destroying PixiJS app:", e);
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
    const spells = d.Spells ?? d.spells ?? [];
    const playerHP = d.PlayerHP ?? d.playerHP ?? null;
    const playerMaxHP = d.PlayerMaxHP ?? d.playerMaxHP ?? null;
    const playerActionTime = d.PlayerActionTime ?? d.playerActionTime ?? null;
    const enemies = d.Enemies ?? d.enemies ?? [];
    return new ArenaBattleScene(container, {
      events,
      dotNetRef,
      mode,
      attackerName,
      defenderName,
      HasShotBuff: hasShotBuff,
      InteractiveMode: interactiveMode,
      Spells: spells,
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
  window.myTunoGame = {
    startBattle: (hostId, battleData) => {
      destroyBattle();
      activeScene = createGame(hostId, battleData, "live");
    },
    startReplay: (hostId, battleData) => {
      destroyBattle();
      activeScene = createGame(hostId, battleData, "replay");
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
