/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The shell talks to the FactoryOS gateway. In dev we proxy the gateway's discovery and module
// endpoints so the SPA can call same-origin relative paths (/shell, /system, /m/*, /modules/*, /store/*).
const gateway = process.env.FACTORYOS_GATEWAY ?? "http://localhost:8080";

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: Object.fromEntries(
      ["/shell", "/system", "/tenant", "/modules", "/store", "/m"].map((path) => [
        path,
        { target: gateway, changeOrigin: true },
      ]),
    ),
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
  },
});
