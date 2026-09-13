import { cloudflareTest, readD1Migrations } from '@cloudflare/vitest-pool-workers'
import { defineConfig } from 'vitest/config'

// Runs inside workerd against a real local D1 and the real Durable Object, so the SSE fan-out and
// the alarm path are the production ones. The SQLite migrations are the storage package's; bug
// reports have a lineage and a database of their own, applied separately for the same reason they
// are stored separately.
export default defineConfig(async () => {
  const migrations = await readD1Migrations('../../packages/storage/migrations/sqlite')
  const bugReportMigrations = await readD1Migrations('../../packages/bug-reports/migrations/sqlite')
  return {
    plugins: [
      cloudflareTest({
        wrangler: { configPath: './wrangler.dev.toml' },
        miniflare: {
          bindings: {
            TEST_MIGRATIONS: migrations,
            TEST_BUG_REPORT_MIGRATIONS: bugReportMigrations,
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
