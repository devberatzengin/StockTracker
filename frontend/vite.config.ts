import { fileURLToPath, URL } from 'node:url'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Backend started with `dotnet run --launch-profile http` listens on 5180.
const apiProxyTarget = process.env.API_PROXY_TARGET ?? 'http://localhost:5180'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    // The lazily loaded barcode reader chunk (@zxing) is ~510 kB on its own.
    chunkSizeWarningLimit: 600,
  },
  server: {
    port: 5273,
    strictPort: true,
    proxy: {
      '/api': {
        target: apiProxyTarget,
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
