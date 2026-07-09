/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

// Injected at build time via Vite `define` (see vite.config.ts).
declare const __APP_VERSION__: string
declare const __GIT_SHA__: string
declare const __BUILD_DATE__: string
