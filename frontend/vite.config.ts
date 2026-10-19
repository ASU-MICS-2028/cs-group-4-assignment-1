import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    // The API serves the built UI, so the whole portal runs from one HTTPS origin.
    outDir: '../backend/StudentPortal.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': { target: 'https://localhost:7443', secure: false },
    },
  },
});
