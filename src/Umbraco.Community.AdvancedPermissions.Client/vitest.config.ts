import { defineConfig } from 'vitest/config';

// Pure modules are tested wherever they live. Lit elements are deliberately not: their behaviour
// is the DOM, and a browser harness would cost more to keep working than the bugs it would catch.
//
// The pattern is deliberately `src/**` rather than `src/live/**`. It was the narrower one, and a
// suite covering the API layer's 409 decoding sat in the tree passing nowhere near `npm test` —
// exactly the code whose failure crashed the conflict dialog instead of opening it. A test that
// does not run is worth less than no test, because it looks like coverage.
export default defineConfig({
  test: {
    include: ['src/**/*.test.ts'],
    environment: 'node',
  },
});
