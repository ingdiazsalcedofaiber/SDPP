import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'auto',
      // devOptions.enabled stays false (the default) — the service worker is only tested against
      // a real production build (`vite preview` or the Docker `web` image), never `vite dev`.
      workbox: {
        // Default globPatterns miss .mjs — without it, pdfjs-dist's pdf.worker.min.mjs (loaded by
        // react-pdf on the conversion/signing pages) never gets precached, silently breaking the
        // PDF viewer offline even though the rest of the app shell works.
        globPatterns: ['**/*.{js,mjs,css,html,ico,png,svg,woff2}'],
      },
      manifest: {
        name: 'SDPP — Secure Document Processing Platform',
        short_name: 'SDPP',
        description: 'Plataforma de procesamiento seguro de documentos: conversión, clasificación y firma electrónica.',
        // White, matching the real in-app AppBar (see app/AppShell.tsx: bgcolor:"#fff" with a thin
        // teal→orange→magenta gradient stripe underneath) — a solid saturated teal title bar read
        // as too heavy/flat compared to that, and clashed with the app icon's own teal arc. Keep
        // in sync by hand with sdppTheme.palette.background.default in src/shared/theme.ts — this
        // config runs outside Vite's module graph for the app itself, so importing that file here
        // isn't worth the coupling for one or two hex values.
        theme_color: '#FFFFFF',
        background_color: '#F5F8F7',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        icons: [
          { src: '/pwa-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/pwa-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/pwa-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
    }),
  ],
})
