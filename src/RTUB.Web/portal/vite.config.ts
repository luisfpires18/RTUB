import { defineConfig } from 'vite';
import { resolve } from 'path';

/**
 * React public-portal pilot (React track, task 001). See docs/react-portal-pilot.md.
 *
 *   npm run build:portal   -> wwwroot/portal/ (committed, like the PixiJS bundles, and rebuilt on publish)
 *   npm run check:portal   -> TypeScript check
 *
 * Served by the ASP.NET Core host: /portal/assets/* as static files, every other /portal path
 * falls back to wwwroot/portal/index.html (Program.cs). No @vitejs/plugin-react: esbuild's
 * automatic JSX runtime is all a production build needs.
 */
export default defineConfig({
  root: __dirname,
  base: '/portal/',
  esbuild: { jsx: 'automatic' },
  build: {
    outDir: resolve(__dirname, '../wwwroot/portal'),
    emptyOutDir: true,
    rollupOptions: {
      output: {
        // Third-party React in its own chunk: cached across portal releases, and the one file the
        // CSP guard tests treat as vendored (like wwwroot/lib). Portal code stays in index-*.js.
        manualChunks: (id) =>
          /[\\/]node_modules[\\/](react|react-dom|scheduler)[\\/]/.test(id) ? 'vendor-react' : undefined,
      },
    },
  },
});
