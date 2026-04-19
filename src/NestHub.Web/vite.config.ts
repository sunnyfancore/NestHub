import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5186',
        changeOrigin: true,
      },
      '/uploads': {
        target: 'http://localhost:5186',
        changeOrigin: true,
      },
    },
  },
  build: {
    chunkSizeWarningLimit: 1100,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules/element-plus') || id.includes('@element-plus')) {
            return 'element'
          }

          if (id.includes('node_modules/vue') || id.includes('node_modules/pinia')) {
            return 'vue'
          }

          if (id.includes('node_modules/axios')) {
            return 'http'
          }

          return undefined
        },
      },
    },
  },
})
