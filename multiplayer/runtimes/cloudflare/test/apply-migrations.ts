import { applyD1Migrations, env } from 'cloudflare:test'

await applyD1Migrations(env.DB, env.TEST_MIGRATIONS)
await applyD1Migrations(env.BUG_DB, env.TEST_BUG_REPORT_MIGRATIONS)
