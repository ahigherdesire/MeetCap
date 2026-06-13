import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  // Relative base so the production bundle loads over file:// inside Electron.
  base: './',
  server: {
    host: true,
    port: 5173,
  },
})
