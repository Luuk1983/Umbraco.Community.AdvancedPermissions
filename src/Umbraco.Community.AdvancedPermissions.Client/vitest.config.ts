import { defineConfig } from 'vitest/config';

// Pure modules are tested wherever they live. Lit elements are deliberately not: their behaviour
// is the DOM, and a browser harness would cost more to keep working than the bugs it would catch.
// Those elements are kept thin for that reason, and anything in them that can be decided without a
// DOM is moved into a pure module under src/live and tested there.
//
// The pattern is deliberately `src/**` rather than `src/live/**`. It was the narrower one, and a
// test sitting outside the pattern would pass nowhere near `npm test` — which is worth less than
// no test, because it looks like coverage. There is none such today; the pattern is wide so that
// there never is.
export default defineConfig({
  test: {
    include: ['src/**/*.test.ts'],
    environment: 'node',
  },
});
