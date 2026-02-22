/**
 * Arena Battle — PixiJS TypeScript entry point.
 * IIFE bundle exposing window.myTunoGame.
 */
import type { ArenaBattleData } from './types/battle-data';
import type { MyTunoGameApi } from './types/window-api';
import { resolveEvents, resolveDotNetRef, pick } from './shared/utils';
import { stopMusic } from './shared/audio';
import type { MusicState } from './shared/audio';
import { ArenaBattleScene } from './scenes/ArenaBattleScene';

let activeScene: ArenaBattleScene | null = null;
let musicState: MusicState | null = null;

function resolveAttackerName(battleData: unknown): string {
  if (!battleData || Array.isArray(battleData)) return 'Attacker';
  const d = battleData as Record<string, unknown>;
  return (d.attackerName ?? d.AttackerName ?? 'Attacker') as string;
}

function resolveDefenderName(battleData: unknown): string {
  if (!battleData || Array.isArray(battleData)) return 'Defender';
  const d = battleData as Record<string, unknown>;
  return (d.defenderName ?? d.DefenderName ?? 'Defender') as string;
}

function createGame(
  hostId: string,
  battleData: ArenaBattleData,
  mode: 'live' | 'replay',
): ArenaBattleScene | null {
  const container = document.getElementById(hostId);
  if (!container) return null;

  const events = resolveEvents(battleData);
  const dotNetRef = resolveDotNetRef(battleData);
  const attackerName = resolveAttackerName(battleData);
  const defenderName = resolveDefenderName(battleData);
  const d = battleData as Record<string, unknown>;
  const hasShotBuff = (d.HasShotBuff ?? d.hasShotBuff ?? false) as boolean;
  const interactiveMode = (d.InteractiveMode ?? d.interactiveMode ?? false) as boolean;
  const spells = (d.Spells ?? d.spells ?? []) as ArenaBattleData['Spells'];
  const playerHP = (d.PlayerHP ?? d.playerHP ?? null) as number | null;
  const playerMaxHP = (d.PlayerMaxHP ?? d.playerMaxHP ?? null) as number | null;
  const playerActionTime = (d.PlayerActionTime ?? d.playerActionTime ?? null) as number | null;
  const enemies = (d.Enemies ?? d.enemies ?? []) as ArenaBattleData['Enemies'];

  return new ArenaBattleScene(container, {
    events,
    dotNetRef,
    mode,
    attackerName,
    defenderName,
    HasShotBuff: hasShotBuff,
    InteractiveMode: interactiveMode,
    Spells: spells,
    PlayerHP: playerHP ?? undefined,
    PlayerMaxHP: playerMaxHP ?? undefined,
    PlayerActionTime: playerActionTime ?? undefined,
    Enemies: enemies,
  });
}

function destroyBattle(): void {
  if (activeScene) {
    activeScene.destroy();
    activeScene = null;
  }
  // Stop background music when leaving arena
  stopMusic(musicState);
  musicState = null;
}

window.myTunoGame = {
  startBattle: (hostId: string, battleData: ArenaBattleData) => {
    destroyBattle();
    activeScene = createGame(hostId, battleData, 'live');
  },
  startReplay: (hostId: string, battleData: ArenaBattleData) => {
    destroyBattle();
    activeScene = createGame(hostId, battleData, 'replay');
  },
  setReplayPlaying: (isPlaying: boolean) => {
    activeScene?.setReplayPlaying(isPlaying);
  },
  setReplaySpeed: (speed: number) => {
    activeScene?.setReplaySpeed(speed);
  },
  jumpToReplayEvent: (index: number) => {
    activeScene?.jumpToEvent(index);
  },
  setSpeed: (speed: number) => {
    if (activeScene) {
      const allowedSpeeds = [1, 5];
      const validSpeed = allowedSpeeds.includes(speed) ? speed : Math.min(5, Math.max(1, Math.round(speed)));
      activeScene.battleSpeed = validSpeed;
    }
  },
  toggleAudio: () => {
    return activeScene?.toggleAudio() ?? false;
  },
  setVolume: (musicVol: number, sfxVol: number) => {
    activeScene?.setVolume(musicVol, sfxVol);
  },
  destroyBattle,
};
