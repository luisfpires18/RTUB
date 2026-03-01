/**
 * Village Mode — Entry point.
 * Built by Vite into an IIFE at wwwroot/js/phaserVillage.js.
 *
 * Exposes `window.villageModeGame` API for Blazor JS interop.
 */
import Phaser from 'phaser';
import { VillageScene } from '@village-scenes/VillageScene';
import type { VillageModeGameApi } from '@village-types/window-api';

let game: Phaser.Game | null = null;

function createVillageApi(): VillageModeGameApi {
  return {
    async start(containerId: string, netRef: DotNet.DotNetObject): Promise<void> {
      // Destroy any existing game
      if (game) {
        game.destroy(true);
        game = null;
      }

      const parent = document.getElementById(containerId);
      if (!parent) {
        console.error(`[VillageMode] Container #${containerId} not found`);
        return;
      }

      game = new Phaser.Game({
        type: Phaser.AUTO,
        parent: containerId,
        width: parent.clientWidth,
        height: parent.clientHeight,
        backgroundColor: '#2a4a20',
        scene: [VillageScene],
        scale: {
          mode: Phaser.Scale.RESIZE,
          autoCenter: Phaser.Scale.CENTER_BOTH,
        },
        render: {
          antialias: true,
          pixelArt: false,
        },
        input: {
          mouse: {
            preventDefaultWheel: true,
          },
        },
      });

      // Pass netRef to scene via scene data
      game.scene.start('VillageScene', { netRef });
    },

    destroy(): void {
      if (game) {
        game.destroy(true);
        game = null;
      }
    },

    isActive(): boolean {
      return game !== null;
    },
  };
}

// Expose to window for Blazor JS interop
window.villageModeGame = createVillageApi();
