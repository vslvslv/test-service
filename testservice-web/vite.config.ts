import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  // Use repo name for GitHub Pages, or '/testservice/ui/' for other deployments
  base: process.env.GITHUB_PAGES === 'true' ? '/test-service/' : '/testservice/ui/',
  plugins: [react()],
  server: {
    port: 5173,
    host: true,
    proxy: {
      '/api': {
        target: 'http://localhost:5058',
        changeOrigin: true,
        secure: false,
      }
    },
    historyApiFallback: true
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
    rollupOptions: {
      output: {
        manualChunks: {
          'react-vendor': ['react', 'react-dom'],
          'router': ['react-router-dom'],
          'icons': ['lucide-react']
        }
      }
    }
  },
  optimizeDeps: {
    include: ['lucide-react']
  }
})
