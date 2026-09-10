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
  close(): Promise<void>
}

/**
 * Opens a SQLite file (or `:memory:`), applies pending migrations, and returns the storage ports.
 * WAL mode keeps a self-hosted server responsive while the event stream reads during writes.
 */
export function openSqliteStorage(filename: string): OpenedStorage {
  const connection = new BetterSqlite3(filename)
  connection.pragma('journal_mode = WAL')
  connection.pragma('foreign_keys = ON')
  const db = drizzleSqlite(connection, { schema: sqliteSchema })
  migrateSqlite(db, { migrationsFolder: SQLITE_MIGRATIONS_DIR })
  return {
    storage: createSqliteStorage(db),
    close: async () => {
      connection.close()
    },
  }
}

/** Opens a Postgres pool, applies pending migrations, and returns the storage ports. */
export async function openPostgresStorage(connectionString: string): Promise<OpenedStorage> {
  const pool = new pg.Pool({ connectionString })
  const db = drizzlePostgres(pool, { schema: postgresSchema })
  await migratePostgres(db, { migrationsFolder: POSTGRES_MIGRATIONS_DIR })
  return {
    storage: createPostgresStorage(db),
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
