import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

// Tauri serves the built files from app/dist; dev server port must match tauri.conf.json devUrl.
export default defineConfig({
  plugins: [svelte()],
  clearScreen: false,
  server: { port: 5173, strictPort: true },
  build: { target: 'chrome105', outDir: 'dist', emptyOutDir: true },
});
