import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/manifests.ts",
      formats: ["es"],
      fileName: "uap",
    },
    outDir: "../Umbraco.Community.AdvancedPermissions/wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco-cms\/.*/],
    },
  },
});
