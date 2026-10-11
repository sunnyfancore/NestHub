import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import type { Declaration } from 'postcss'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), Components({ resolvers: [ElementPlusResolver()], dts: false })],
  css: {
    postcss: {
      plugins: [{
        postcssPlugin: 'modern-icon-font',
        Declaration(declaration: Declaration) {
          // All supported browsers use WOFF2; avoid shipping five redundant font formats.
          if (declaration.prop === 'src' && declaration.value.includes('fontawesome-webfont')) {
            declaration.value = "url('../fonts/fontawesome-webfont.woff2') format('woff2')"
          }
        },
      }],
    },
  },
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
      '/downloads': {
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
