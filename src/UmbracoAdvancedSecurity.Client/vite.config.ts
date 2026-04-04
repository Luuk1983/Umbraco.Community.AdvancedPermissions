import { defineConfig } from "vite";

export default defineConfig({
  build: {
    lib: {
      entry: "src/manifests.ts",
      formats: ["es"],
      fileName: "uas",
    },
    outDir: "../UmbracoAdvancedSecurity/wwwroot/App_Plugins/UmbracoAdvancedSecurity",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco-cms\/.*/],
    },
  },
});
