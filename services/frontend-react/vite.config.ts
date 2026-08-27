import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  build: {
    // S3 へそのまま置ける静的成果物を出す。SSR は使わない。
    outDir: 'dist',
    sourcemap: true,
  },
});
