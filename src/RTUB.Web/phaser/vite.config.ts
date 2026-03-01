import { defineConfig } from 'vite';
import { resolve } from 'path';

/**
 * Vite config for building Phaser TypeScript village scenes.
 *
 * Usage (from package.json scripts):
 *   ENTRY=villageMode OUTNAME=phaserVillage npx vite build --config phaser/vite.config.ts
 */
const entry = process.env.ENTRY ?? 'villageMode';
const outName = process.env.OUTNAME ?? 'phaserVillage';

export default defineConfig({
  root: __dirname,
  build: {
    outDir: resolve(__dirname, '../wwwroot/js'),
    emptyOutDir: false,
    sourcemap: true,
    minify: false,
    lib: {
      entry: resolve(__dirname, `${entry}.ts`),
      name: `__phaser_${outName}`,
      fileName: () => `${outName}.js`,
      formats: ['iife'],
    },
    rollupOptions: {
      external: ['phaser'],
      output: {
        globals: {
          phaser: 'Phaser',
        },
      },
    },
  },
  resolve: {
    alias: {
      '@village-types': resolve(__dirname, 'types'),
      '@village-scenes': resolve(__dirname, 'scenes'),
      '@village-shared': resolve(__dirname, 'shared'),
      '@village-ui': resolve(__dirname, 'ui'),
    },
  },
});
