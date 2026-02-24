import { defineConfig } from 'vite';
import { resolve } from 'path';

/**
 * Vite config for building PixiJS TypeScript battle scenes.
 *
 * Usage (from package.json scripts):
 *   ENTRY=arena   OUTNAME=pixiBattle       npx vite build --config pixi/vite.config.ts
 *   ENTRY=stageBattle OUTNAME=pixiStageBattle npx vite build --config pixi/vite.config.ts
 *   ENTRY=surviveMode OUTNAME=pixiSurviveMode npx vite build --config pixi/vite.config.ts
 *
 * The build:pixi script in package.json calls all three sequentially.
 */
const entry = process.env.ENTRY ?? 'arena';
const outName = process.env.OUTNAME ?? 'pixiBattle';

export default defineConfig({
  root: __dirname,
  build: {
    outDir: resolve(__dirname, '../wwwroot/js'),
    emptyOutDir: false,
    sourcemap: true,
    minify: false,
    lib: {
      entry: resolve(__dirname, `${entry}.ts`),
      name: `__pixi_${outName}`,
      fileName: () => `${outName}.js`,
      formats: ['iife'],
    },
    rollupOptions: {
      external: ['pixi.js'],
      output: {
        globals: {
          'pixi.js': 'PIXI',
        },
      },
    },
  },
  resolve: {
    alias: {
      '@shared': resolve(__dirname, 'shared'),
      '@types': resolve(__dirname, 'types'),
      '@scenes': resolve(__dirname, 'scenes'),
    },
  },
});
