import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/manifests.ts",
      formats: ["es"],
      fileName: "uap",
    },
    outDir: "../LP.Umbraco.AdvancedPermissions/wwwroot/App_Plugins/UmbracoAdvancedPermissions",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco-cms\/.*/],
    },
  },
});
