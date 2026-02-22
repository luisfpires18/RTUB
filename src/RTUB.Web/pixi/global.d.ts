/* eslint-disable @typescript-eslint/no-explicit-any */

/**
 * Blazor JS Interop — DotNetObjectReference passed from C# to JS.
 */
declare namespace DotNet {
  interface DotNetObject {
    invokeMethodAsync<T = string>(methodName: string, ...args: unknown[]): Promise<T>;
    dispose(): void;
  }
}

/**
 * Global PIXI namespace (loaded from CDN, not bundled).
 */
declare const PIXI: typeof import('pixi.js');

/**
 * Augment Window with our game APIs so TS doesn't complain on assignment.
 */
interface Window {
  myTunoGame: import('./types/window-api').MyTunoGameApi;
  stageBattleGame: import('./types/window-api').StageBattleGameApi;
  surviveModeGame: import('./types/window-api').SurviveModeGameApi;
  AudioContext: typeof AudioContext;
  webkitAudioContext: typeof AudioContext;
}
