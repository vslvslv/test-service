import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  // GitHub Pages: '/test-service/'. Docker/root: use VITE_BASE_PATH=/ so SPA works at root.
  base: process.env.VITE_BASE_PATH ?? (process.env.GITHUB_PAGES === 'true' ? '/test-service/' : '/testservice/ui/'),
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
        // Bundle react, react-dom, and react-router-dom together to avoid
        // TDZ (Cannot access 'X' before initialization) errors at runtime.
        // react-router-dom depends on react; splitting them into separate
        // chunks can cause ESM evaluation-order races in production.
        // See: https://github.com/vitejs/vite/issues/14048
        manualChunks: {
          'react-vendor': ['react', 'react-dom', 'react-router-dom'],
          'icons': ['lucide-react']
        }
      }
    }
  },
  optimizeDeps: {
    include: ['lucide-react']
  }
})
