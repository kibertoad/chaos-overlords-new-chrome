import { cloudflareTest, readD1Migrations } from '@cloudflare/vitest-pool-workers'
import { defineConfig } from 'vitest/config'

// Runs inside workerd against a real local D1 and the real Durable Object, so the SSE fan-out and
// the alarm path are the production ones. The SQLite migrations are the storage package's.
export default defineConfig(async () => {
  const migrations = await readD1Migrations('../../packages/storage/migrations/sqlite')
  return {
    plugins: [
      cloudflareTest({
        wrangler: { configPath: './wrangler.toml' },
        miniflare: {
          bindings: {
            TEST_MIGRATIONS: migrations,
            PUBLIC_LISTING: 'true',
            RATE_LIMIT_PER_MINUTE: '10000',
          },
        },
      }),
    ],
    test: {
      include: ['test/**/*.spec.ts'],
      setupFiles: ['./test/apply-migrations.ts'],
      testTimeout: 30_000,
      hookTimeout: 30_000,
    },
  }
})
