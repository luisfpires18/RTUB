/**
 * Survive Mode — Audio Manager.
 *
 * Manages background music and SFX via HTML5 Audio element pools.
 */
import { audioCacheBuster } from '@shared/utils';

// ─── SFX Configuration ───────────────────────────────────────

const SFX_POOL_SIZE = 4;

const SFX_MAP: Record<string, string> = {
  win: '/audio/games/my-tuno/survive/win.mp3',
  death: '/audio/games/my-tuno/survive/death.mp3',
  hit: '/audio/games/my-tuno/survive/hit.mp3',
  levelup: '/audio/games/my-tuno/survive/levelup.mp3',
  lightning: '/audio/games/my-tuno/survive/lightning.mp3',
  explosion: '/audio/games/my-tuno/survive/explosion.mp3',
};

// ─── Audio Manager ────────────────────────────────────────────

export class AudioManager {
  private sfxPools: Record<string, HTMLAudioElement[]> = {};
  private bgMusic: HTMLAudioElement | null = null;
  private bgMusicLoaded = false;
  private _enabled = true;

  get enabled(): boolean { return this._enabled; }

  setEnabled(enabled: boolean): void {
    this._enabled = enabled;
    if (this.bgMusic) {
      if (enabled) this.bgMusic.play().catch(() => { /* noop */ });
      else this.bgMusic.pause();
    }
  }

  playMusic(): void {
    if (!this._enabled) return;
    try {
      if (this.bgMusic) {
        this.bgMusic.currentTime = 0;
        this.bgMusic.play().catch(() => { /* noop */ });
        return;
      }
      this.bgMusic = new Audio('/sound/survival_battle.mp3' + audioCacheBuster);
      this.bgMusic.loop = true;
      this.bgMusic.volume = 0.3;
      this.bgMusic.play().catch(() => { /* noop */ });
      this.bgMusicLoaded = true;
    } catch { /* noop */ }
  }

  playSFX(type: string): void {
    if (!this._enabled) return;
    try {
      const audio = this._getPooledAudio(type);
      if (audio) audio.play().catch(() => { /* noop */ });
    } catch { /* noop */ }
  }

  stopMusic(): void {
    if (this.bgMusic) {
      try {
        this.bgMusic.pause();
        this.bgMusic.currentTime = 0;
      } catch { /* noop */ }
    }
  }

  destroy(): void {
    this.stopMusic();
    this.bgMusic = null;
    this.bgMusicLoaded = false;
    this.sfxPools = {};
  }

  private _getPooledAudio(type: string): HTMLAudioElement | null {
    const src = SFX_MAP[type];
    if (!src) return null;

    if (!this.sfxPools[type]) {
      this.sfxPools[type] = [];
      for (let i = 0; i < SFX_POOL_SIZE; i++) {
        const a = new Audio(src + audioCacheBuster);
        a.volume = 0.5;
        this.sfxPools[type].push(a);
      }
    }

    for (const a of this.sfxPools[type]) {
      if (a.paused || a.ended) {
        a.currentTime = 0;
        return a;
      }
    }

    const a = this.sfxPools[type][0];
    a.currentTime = 0;
    return a;
  }
}
