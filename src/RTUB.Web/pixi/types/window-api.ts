/**
 * Window-level game API shapes exposed to Blazor via IJSRuntime.
 */
import type { ArenaBattleData, StageBattleData } from './battle-data';
import type { SurviveLevelData } from './survive-data';

export interface MyTunoGameApi {
  startBattle(hostId: string, battleData: ArenaBattleData): void;
  startReplay(hostId: string, battleData: ArenaBattleData): void;
  setReplayPlaying(isPlaying: boolean): void;
  setReplaySpeed(speed: number): void;
  jumpToReplayEvent(index: number): void;
  setSpeed(speed: number): void;
  toggleAudio(): boolean;
  setVolume(musicVol: number, sfxVol: number): void;
  destroyBattle(): void;
}

export interface StageBattleGameApi {
  start(hostId: string, battleData: StageBattleData): void;
  destroy(): void;
  destroySceneOnly(): void;
  setSpeed(speed: number): void;
  setAudioEnabled(enabled: boolean): void;
  nextBattle(battleData: StageBattleData): void;
}

export interface SurviveModeGameApi {
  start(containerId: string, levelData: SurviveLevelData, netRef: DotNet.DotNetObject): Promise<void>;
  nextLevel(levelData: SurviveLevelData): Promise<void>;
  pause(): void;
  resume(): void;
  setAudioEnabled(enabled: boolean): void;
  destroy(): void;
  isActive(): boolean;
}
