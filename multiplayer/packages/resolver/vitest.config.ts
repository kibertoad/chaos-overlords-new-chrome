import { defineConfig } from 'vitest/config'

// The hosts boot the real WebAssembly build and resolve whole matches, under Node and under workerd,
// so the timeouts are those of a match, not of a unit test.
export default defineConfig({
  test: {
    include: ['test/**/*.spec.ts'],
    // Writes the native transcript the host tests replay, once for every file; see the harness.
    globalSetup: ['test/harness/transcript.mjs'],
    testTimeout: 300_000,
    hookTimeout: 120_000,
  },
})
