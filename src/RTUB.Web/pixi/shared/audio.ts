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
