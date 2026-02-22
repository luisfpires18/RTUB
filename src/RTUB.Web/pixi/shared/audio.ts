/**
 * Shared audio engine for Arena and Stage battle scenes.
 * Uses Web Audio API oscillator synthesis for SFX and AudioBufferSource for music.
 *
 * Survive mode uses a different Audio() pool system — it does not use this module.
 */
import { audioBufferCache, audioCacheBuster } from './utils';

// ─── Module State ─────────────────────────────────────────────
let sharedAudioContext: AudioContext | null = null;

/** Get or create a shared AudioContext (reused across scenes). */
export function getSharedAudioContext(): AudioContext | null {
  if (!sharedAudioContext || sharedAudioContext.state === 'closed') {
    try {
      const Ctor = window.AudioContext || window.webkitAudioContext;
      sharedAudioContext = new Ctor();
    } catch (e) {
      console.warn('AudioContext not available:', e);
      return null;
    }
  }
  return sharedAudioContext;
}

// ─── Sound Effect Types ───────────────────────────────────────
export type SoundType =
  | 'attack'
  | 'hit'
  | 'critical'
  | 'ko'
  | 'victory'
  | 'defeat'
  | 'block';

/**
 * Play an oscillator-based SFX.
 * Union of Arena (has 'hit') and Stage (has 'defeat') cases.
 */
export function playSound(
  ctx: AudioContext,
  type: SoundType,
  sfxVolume: number,
): void {
  if (!sfxVolume) return;
  if (ctx.state === 'suspended') ctx.resume();

  const t = ctx.currentTime;
  const oscillator = ctx.createOscillator();
  const gainNode = ctx.createGain();
  oscillator.connect(gainNode);
  gainNode.connect(ctx.destination);

  switch (type) {
    case 'attack':
      oscillator.frequency.value = 200;
      oscillator.type = 'square';
      gainNode.gain.setValueAtTime(sfxVolume * 0.3, t);
      gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.1);
      oscillator.start(t);
      oscillator.stop(t + 0.1);
      break;

    case 'hit':
      oscillator.frequency.value = 150;
      oscillator.type = 'sawtooth';
      gainNode.gain.setValueAtTime(sfxVolume * 0.4, t);
      gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.15);
      oscillator.start(t);
      oscillator.stop(t + 0.15);
      break;

    case 'critical': {
      oscillator.frequency.value = 400;
      oscillator.type = 'sine';
      gainNode.gain.setValueAtTime(sfxVolume * 0.5, t);
      gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.2);

      const osc2 = ctx.createOscillator();
      const gain2 = ctx.createGain();
      osc2.connect(gain2);
      gain2.connect(ctx.destination);
      osc2.frequency.value = 600;
      osc2.type = 'sine';
      gain2.gain.setValueAtTime(sfxVolume * 0.3, t + 0.05);
      gain2.gain.exponentialRampToValueAtTime(0.01, t + 0.25);

      oscillator.start(t);
      oscillator.stop(t + 0.2);
      osc2.start(t + 0.05);
      osc2.stop(t + 0.25);
      break;
    }

    case 'ko':
      oscillator.frequency.setValueAtTime(300, t);
      oscillator.frequency.exponentialRampToValueAtTime(50, t + 0.5);
      oscillator.type = 'triangle';
      gainNode.gain.setValueAtTime(sfxVolume * 0.6, t);
      gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.5);
      oscillator.start(t);
      oscillator.stop(t + 0.5);
      break;

    case 'victory': {
      const notes = [262, 330, 392, 523];
      notes.forEach((freq, i) => {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.frequency.value = freq;
        osc.type = 'sine';
        const startTime = t + i * 0.15;
        gain.gain.setValueAtTime(sfxVolume * 0.4, startTime);
        gain.gain.exponentialRampToValueAtTime(0.01, startTime + 0.3);
        osc.start(startTime);
        osc.stop(startTime + 0.3);
      });
      break;
    }

    case 'defeat':
      oscillator.frequency.setValueAtTime(200, t);
      oscillator.frequency.exponentialRampToValueAtTime(80, t + 0.5);
      oscillator.type = 'sawtooth';
      gainNode.gain.setValueAtTime(sfxVolume * 0.5, t);
      gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.5);
      oscillator.start(t);
      oscillator.stop(t + 0.5);
      break;

    case 'block':
      oscillator.frequency.value = 150;
      oscillator.type = 'triangle';
      gainNode.gain.setValueAtTime(sfxVolume * 0.4, t);
      gainNode.gain.exponentialRampToValueAtTime(0.01, t + 0.15);
      oscillator.start(t);
      oscillator.stop(t + 0.15);
      break;
  }
}

/**
 * Play a spell-specific oscillator sound by attackId.
 * Identical across Arena and Stage.
 */
export function playSpellSound(
  ctx: AudioContext,
  attackId: string,
  sfxVolume: number,
): void {
  if (!sfxVolume) return;
  if (ctx.state === 'suspended') ctx.resume();

  const t = ctx.currentTime;
  const vol = sfxVolume;

  const playNote = (
    freq: number,
    type: OscillatorType,
    start: number,
    dur: number,
    v = 0.3,
  ): void => {
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
    case 'heavy_attack':
      playNote(120, 'sawtooth', 0, 0.15, 0.5);
      playNote(180, 'square', 0.03, 0.12, 0.4);
      break;
    case 'guitarra_barrage':
      for (let i = 0; i < 6; i++) {
        playNote(82 + i * 15, 'sawtooth', i * 0.07, 0.08, 0.4);
        playNote(165 + i * 10, 'square', i * 0.07 + 0.03, 0.06, 0.25);
      }
      break;
    case 'bandolim_swiftchord':
      playNote(587, 'triangle', 0, 0.12, 0.4);
      playNote(784, 'triangle', 0.02, 0.1, 0.35);
      playNote(988, 'sine', 0.04, 0.08, 0.3);
      break;
    case 'cavaquinho_paralysis':
      for (let i = 0; i < 5; i++)
        playNote(800 + Math.random() * 400, 'square', i * 0.06, 0.05, 0.3);
      playNote(200, 'sawtooth', 0.35, 0.2, 0.4);
      break;
    case 'acordeao_fear':
      playNote(130, 'sawtooth', 0, 0.6, 0.4);
      playNote(138, 'sawtooth', 0, 0.55, 0.35);
      playNote(98, 'square', 0.1, 0.4, 0.3);
      playNote(65, 'triangle', 0.2, 0.4, 0.25);
      break;
    case 'contrabaixo_sonicboom': {
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.connect(g);
      g.connect(ctx.destination);
      osc.type = 'sine';
      osc.frequency.setValueAtTime(110, t);
      osc.frequency.exponentialRampToValueAtTime(35, t + 0.5);
      g.gain.setValueAtTime(vol * 0.6, t);
      g.gain.exponentialRampToValueAtTime(0.01, t + 0.5);
      osc.start(t);
      osc.stop(t + 0.5);
      playNote(55, 'triangle', 0, 0.4, 0.3);
      playNote(220, 'square', 0.05, 0.15, 0.2);
      break;
    }
    case 'percussao_combo':
      playNote(200, 'square', 0, 0.06, 0.5);
      playNote(300, 'square', 0.08, 0.06, 0.4);
      playNote(250, 'square', 0.16, 0.06, 0.45);
      playNote(400, 'square', 0.24, 0.08, 0.5);
      playNote(150, 'triangle', 0, 0.3, 0.25);
      break;
    case 'pandeireta_boomerang':
      for (let i = 0; i < 8; i++) {
        playNote(400 + i * 50, 'sine', i * 0.04, 0.06, 0.35);
      }
      for (let i = 0; i < 8; i++) {
        playNote(750 - i * 50, 'sine', 0.32 + i * 0.04, 0.06, 0.3);
      }
      break;
    case 'estandarte_rally':
      playNote(262, 'triangle', 0, 0.15, 0.4);
      playNote(330, 'triangle', 0.12, 0.15, 0.4);
      playNote(392, 'triangle', 0.24, 0.2, 0.45);
      playNote(196, 'sine', 0, 0.5, 0.2);
      break;
    case 'violino_sleep':
      playNote(660, 'sine', 0, 0.3, 0.35);
      playNote(600, 'sine', 0.15, 0.3, 0.3);
      playNote(550, 'sine', 0.3, 0.3, 0.25);
      playNote(500, 'sine', 0.45, 0.35, 0.2);
      break;
  }
}

// ─── Background Music ─────────────────────────────────────────

export interface MusicState {
  source: AudioBufferSourceNode | null;
  gainNode: GainNode | null;
  currentTrack: string | null;
}

/**
 * Load and play looped background music.
 * Returns a MusicState handle for volume/stop control.
 */
export async function loadBackgroundMusic(
  ctx: AudioContext,
  musicUrl: string,
  volume: number,
  audioEnabled: boolean,
): Promise<MusicState> {
  const state: MusicState = { source: null, gainNode: null, currentTrack: musicUrl };
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
    console.warn('Could not load background music:', e);
  }
  return state;
}

/** Stop and clean up a music source. */
export function stopMusic(state: MusicState | null): void {
  if (!state?.source) return;
  try {
    state.source.stop();
  } catch {
    // already stopped
  }
  state.source = null;
  state.gainNode = null;
  state.currentTrack = null;
}

/** Set music volume (0 if muted). */
export function setMusicVolume(
  state: MusicState | null,
  volume: number,
  enabled: boolean,
): void {
  if (state?.gainNode) {
    state.gainNode.gain.value = enabled ? volume : 0;
  }
}
