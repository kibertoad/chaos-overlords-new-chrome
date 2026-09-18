import { fileURLToPath } from 'node:url'
import type { MultiplayerStorage } from '@chaos-overlords/kernel'
import BetterSqlite3 from 'better-sqlite3'
import { drizzle as drizzleSqlite } from 'drizzle-orm/better-sqlite3'
import { migrate as migrateSqlite } from 'drizzle-orm/better-sqlite3/migrator'
import { drizzle as drizzlePostgres } from 'drizzle-orm/node-postgres'
import { migrate as migratePostgres } from 'drizzle-orm/node-postgres/migrator'
import pg from 'pg'
import { createPostgresStorage } from '../postgres/repositories'
import * as postgresSchema from '../postgres/schema'
import { createSqliteStorage } from '../sqlite/repositories'
import * as sqliteSchema from '../sqlite/schema'

/** Migration folders, resolved from the package so a runtime never guesses a relative path. */
export const SQLITE_MIGRATIONS_DIR = fileURLToPath(
  new URL('../../migrations/sqlite', import.meta.url),
)
export const POSTGRES_MIGRATIONS_DIR = fileURLToPath(
  new URL('../../migrations/postgres', import.meta.url),
)

export interface OpenedStorage {
  storage: MultiplayerStorage
  /**
   * Which driver is behind the ports. better-sqlite3 is synchronous, so every statement it runs
   * blocks the event loop and every other request with it; a runtime sizes its background work
   * (the retention batch above all) by this.
   */
  dialect: 'sqlite' | 'postgres'
  close(): Promise<void>
}

/**
 * The lock every migrating process takes first. Two instances started against one database race
 * the migrator otherwise, each trying to create the same table; the second waits here instead and
 * finds nothing left to apply. Any fixed 64-bit key will do as long as every instance uses it.
 */
const MIGRATION_LOCK_KEY = 7_262_803_811_402_601n

/**
 * Opens a SQLite file (or `:memory:`), applies pending migrations, and returns the storage ports.
 * WAL mode keeps a self-hosted server responsive while the event stream reads during writes.
 */
export function openSqliteStorage(filename: string): OpenedStorage {
  const connection = new BetterSqlite3(filename)
  connection.pragma('journal_mode = WAL')
  // WAL is durable at `NORMAL` against a process crash (only a power loss can lose the last
  // commits), and it stops every event append from waiting on an fsync of the WAL. The driver is
  // synchronous, so that wait was the whole process waiting.
  connection.pragma('synchronous = NORMAL')
  connection.pragma('foreign_keys = ON')
  const db = drizzleSqlite(connection, { schema: sqliteSchema })
  migrateSqlite(db, { migrationsFolder: SQLITE_MIGRATIONS_DIR })
  return {
    storage: createSqliteStorage(db),
    dialect: 'sqlite',
    close: async () => {
      connection.close()
    },
  }
}

/** Opens a Postgres pool, applies pending migrations, and returns the storage ports. */
export async function openPostgresStorage(connectionString: string): Promise<OpenedStorage> {
  const pool = new pg.Pool({ connectionString })
  const db = drizzlePostgres(pool, { schema: postgresSchema })
  const lock = await pool.connect()
  try {
    await lock.query('select pg_advisory_lock($1)', [MIGRATION_LOCK_KEY.toString()])
    await migratePostgres(db, { migrationsFolder: POSTGRES_MIGRATIONS_DIR })
    await lock.query('select pg_advisory_unlock($1)', [MIGRATION_LOCK_KEY.toString()])
  } finally {
    lock.release()
  }
  return {
    storage: createPostgresStorage(db),
    dialect: 'postgres',
    close: () => pool.end(),
  }
}

export type StorageTarget =
  | { kind: 'sqlite'; filename: string }
  | { kind: 'postgres'; connectionString: string }

/**
 * `DATABASE_URL` grammar: `postgres://…` / `postgresql://…` for Postgres, `sqlite:<path>` or
 * `sqlite::memory:` for SQLite. Anything else is refused rather than guessed.
 */
export function parseStorageTarget(databaseUrl: string): StorageTarget {
  if (/^postgres(ql)?:\/\//.test(databaseUrl)) {
    return { kind: 'postgres', connectionString: databaseUrl }
  }
  if (databaseUrl.startsWith('sqlite:')) {
    return { kind: 'sqlite', filename: databaseUrl.slice('sqlite:'.length) || ':memory:' }
  }
  throw new Error(
    `Unsupported DATABASE_URL "${databaseUrl}": expected postgres://… or sqlite:<path>`,
  )
}

export function openStorage(target: StorageTarget): Promise<OpenedStorage> {
  return target.kind === 'postgres'
    ? openPostgresStorage(target.connectionString)
    : Promise.resolve(openSqliteStorage(target.filename))
}
