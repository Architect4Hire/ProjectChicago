import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

const __dirname = import.meta.dirname;

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@/design-system': resolve(__dirname, './design-system/src/design-system'),
      '@/layout': resolve(__dirname, './design-system/src/layout'),
      '@/context': resolve(__dirname, './design-system/src/context'),
      '@/icons': resolve(__dirname, './design-system/src/icons'),
      '@/components': resolve(__dirname, './design-system/src/components'),
      '@/api': resolve(__dirname, './src/api'),
      '@/auth': resolve(__dirname, './src/auth'),
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
  },
});
