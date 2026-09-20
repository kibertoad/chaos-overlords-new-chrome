import type { BaseSQLiteDatabase } from 'drizzle-orm/sqlite-core'
import type * as schema from './schema'

/**
 * Either SQLite driver Drizzle offers: better-sqlite3 (`'sync'`) or D1 (`'async'`). Awaiting a
 * synchronous result is a no-op, so one implementation serves both. Every conditional write ends
 * in `.returning()` because the two drivers report affected rows in different shapes.
 *
 * It lives here rather than beside the repositories so the modules those repositories are split
 * across can name it without importing each other.
 */
export type SqliteDatabase = BaseSQLiteDatabase<'sync' | 'async', unknown, typeof schema>
