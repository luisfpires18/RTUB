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
 * Global Phaser namespace (loaded from CDN, not bundled).
 */
declare const Phaser: typeof import('phaser');

/**
 * Augment Window with village game API so TS doesn't complain on assignment.
 */
interface Window {
  villageModeGame: import('./types/window-api').VillageModeGameApi;
}
