/**
 * Stage/Boss Battle — PixiJS TypeScript entry point.
 * IIFE bundle exposing window.stageBattleGame.
 */
import type { StageBattleData } from './types/battle-data';
import type { StageBattleGameApi } from './types/window-api';
import { resolveEvents, pick } from './shared/utils';
import { StageBattleScene } from './scenes/StageBattleScene';

let stageScene: StageBattleScene | null = null;

function createGame(containerId: string, battleData: StageBattleData): void {
  const container = document.getElementById(containerId);
  if (!container) {
    console.error('Stage battle container not found:', containerId);
    return;
  }

  if (stageScene) {
    stageScene.destroy();
    stageScene = null;
  }

  const data = battleData as unknown as Record<string, unknown>;
  const events = resolveEvents(battleData);
  const dotNetRef = (data.dotNetRef ?? data.DotNetRef ?? null) as DotNet.DotNetObject | null;

  stageScene = new StageBattleScene(container, {
    events,
    dotNetRef,
    stageNumber: pick<number>(data, 'StageNumber', 'stageNumber', 1),
    enemyType: pick<string>(data, 'EnemyType', 'enemyType', 'normal'),
    enemyCount: pick<number>(data, 'EnemyCount', 'enemyCount', 1),
    playerName: pick<string>(data, 'PlayerName', 'playerName', 'Player'),
    enemyName: pick<string>(data, 'EnemyName', 'enemyName', 'Enemy'),
    backgroundPath: pick<string>(data, 'BackgroundPath', 'backgroundPath', ''),
    playerSpritePath: pick<string>(data, 'PlayerSpritePath', 'playerSpritePath', ''),
    enemySprites: data.enemySprites ?? data.EnemySprites,
    enemyPlacements: data.enemyPlacements ?? data.EnemyPlacements,
    HasShotBuff: pick<boolean>(data, 'HasShotBuff', 'hasShotBuff', false),
    interactiveMode: pick<boolean>(data, 'InteractiveMode', 'interactiveMode', false),
    spells: data.spells ?? data.Spells,
    playerHP: data.playerHP ?? data.PlayerHP,
    playerMaxHP: data.playerMaxHP ?? data.PlayerMaxHP,
    playerActionTime: data.playerActionTime ?? data.PlayerActionTime,
    enemies: data.enemies ?? data.Enemies,
    consumables: data.consumables ?? data.Consumables,
    consumableImages: data.consumableImages ?? data.ConsumableImages,
    activeBuffs: data.activeBuffs ?? data.ActiveBuffs,
    BattleSpeed: data.battleSpeed ?? data.BattleSpeed,
  });

  const initialSpeed = pick<number>(data, 'BattleSpeed', 'battleSpeed', 1);
  if (initialSpeed !== 1) {
    stageScene.setSpeed(initialSpeed);
  }
}

function destroyBattle(): void {
  if (stageScene) {
    stageScene.destroy();
    stageScene = null;
  }
  StageBattleScene.stopBackgroundMusic();
}

function destroySceneOnly(): void {
  if (stageScene) {
    stageScene.destroy();
    stageScene = null;
  }
}

function nextBattle(battleData: StageBattleData): void {
  if (!stageScene || !stageScene.app || !stageScene.stage) {
    console.warn('No active scene, using start() instead');
    if (stageScene) {
      try { stageScene.destroy(); } catch { /* ignore */ }
      stageScene = null;
    }
    createGame('phaserBattleContainer', battleData);
    return;
  }

  const data = battleData as unknown as Record<string, unknown>;
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
    spells: data.spells ?? data.Spells,
    playerHP: data.playerHP ?? data.PlayerHP,
    playerMaxHP: data.playerMaxHP ?? data.PlayerMaxHP,
    playerActionTime: data.playerActionTime ?? data.PlayerActionTime,
    enemies: data.enemies ?? data.Enemies,
    consumables: data.consumables ?? data.Consumables,
    consumableImages: data.consumableImages ?? data.ConsumableImages,
    consumableCooldowns: data.consumableCooldowns ?? data.ConsumableCooldowns,
    spellCooldowns: data.spellCooldowns ?? data.SpellCooldowns,
    activeBuffs: data.activeBuffs ?? data.ActiveBuffs,
  });
}

// ── Expose to window ──────────────────────────────────────────
const api: StageBattleGameApi = {
  start: createGame,
  destroy: destroyBattle,
  destroySceneOnly,
  setSpeed(speed: number) {
    if (stageScene) {
      stageScene.setSpeed(speed === 5 ? 5 : 1);
    }
  },
  setAudioEnabled(enabled: boolean) {
    if (stageScene) {
      stageScene.setAudioEnabled(enabled);
    }
    StageBattleScene.setGlobalAudioEnabled(enabled);
  },
  nextBattle,
};

(window as unknown as { stageBattleGame: StageBattleGameApi }).stageBattleGame = api;
