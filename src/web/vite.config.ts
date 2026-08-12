import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import svgr from 'vite-plugin-svgr'
import { resolve } from 'path'

// https://vite.dev/config/
const __dirname = import.meta.dirname

export default defineConfig({
  plugins: [
    react(),
    svgr({
      svgrOptions: { exportType: 'named', ref: true, svgo: false, titleProp: true },
      include: '**/*.svg?react',
    }),
  ],
  resolve: {
    alias: {
      '@/design-system': resolve(__dirname, './design-system/src/design-system'),
      '@/layout': resolve(__dirname, './design-system/src/layout'),
      '@/context': resolve(__dirname, './design-system/src/context'),
      '@/icons': resolve(__dirname, './design-system/src/icons'),
      '@/components': resolve(__dirname, './design-system/src/components'),
    },
  },
  publicDir: 'public',
})
