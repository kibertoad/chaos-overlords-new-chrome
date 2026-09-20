import type { PgDatabase, PgQueryResultHKT } from 'drizzle-orm/pg-core'
import type * as schema from './schema'

/**
 * Any driver Drizzle wraps as a `PgDatabase` (node-postgres in the Node runtime).
 *
 * It lives here rather than beside the repositories so the modules those repositories are split
 * across can name it without importing each other.
 */
export type PostgresDatabase = PgDatabase<PgQueryResultHKT, typeof schema>
