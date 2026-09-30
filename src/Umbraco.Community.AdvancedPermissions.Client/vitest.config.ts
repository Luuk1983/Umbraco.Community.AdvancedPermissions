import { defineConfig } from 'vitest/config';

// Only the pure modules under src/live are tested. Everything else in this package is a Lit
// element whose behaviour is the DOM, and a browser harness for those would cost more to keep
// working than the bugs it would catch. These two are different: they decide whether somebody's
// unsaved work survives.
export default defineConfig({
  test: {
    include: ['src/live/**/*.test.ts'],
    environment: 'node',
  },
});
