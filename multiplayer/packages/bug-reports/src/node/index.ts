import { mkdir, readFile, rm, writeFile } from 'node:fs/promises'
import { dirname, isAbsolute, join, normalize, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import BetterSqlite3 from 'better-sqlite3'
import { drizzle } from 'drizzle-orm/better-sqlite3'
import { migrate } from 'drizzle-orm/better-sqlite3/migrator'
import type { BlobStore, BugReportRepository } from '../ports'
import { createBugReportRepository } from '../repository'
import * as schema from '../schema'

/** This area's own migration lineage, resolved from the package rather than guessed relatively. */
export const BUG_REPORT_MIGRATIONS_DIR = fileURLToPath(
  new URL('../../migrations/sqlite', import.meta.url),
)

export interface OpenedBugReportStorage {
  repository: BugReportRepository
  close(): Promise<void>
}

/**
 * Opens the bug report database — a *different* file from the multiplayer one — and migrates it.
 *
 * Two connections to two files rather than two schemas in one: a self-hosted operator who decides
 * they would rather not keep bug reports can delete the file, and one who wants to keep them
 * forever is not fighting the match retention sweep to do it.
 */
export function openBugReportStorage(filename: string): OpenedBugReportStorage {
  const connection = new BetterSqlite3(filename)
  connection.pragma('journal_mode = WAL')
  const db = drizzle(connection, { schema })
  migrate(db, { migrationsFolder: BUG_REPORT_MIGRATIONS_DIR })
  return {
    repository: createBugReportRepository(db),
    close: async () => {
      connection.close()
    },
  }
}

/**
 * A directory of archives, for a server hosting its own reports.
 *
 * Keys come from `archiveKey`, which builds them out of a date and a UUID, so nothing a reporter
 * sends reaches this path. The traversal guard is here anyway: this writes to a filesystem, and the
 * one place a key could ever start coming from somewhere else is a future change to that function.
 */
export function createFileBlobStore(root: string): BlobStore {
  const base = resolve(root)
  const pathFor = (key: string): string => {
    const full = normalize(join(base, key))
    if (!full.startsWith(base + sep) || isAbsolute(key)) {
      throw new Error(`Refusing a blob key that escapes the store: ${key}`)
    }
    return full
  }
  return {
    async put(key, bytes) {
      const path = pathFor(key)
      await mkdir(dirname(path), { recursive: true })
      await writeFile(path, bytes)
    },
    async get(key) {
      try {
        return new Uint8Array(await readFile(pathFor(key)))
      } catch (error) {
        if ((error as NodeJS.ErrnoException).code === 'ENOENT') return null
        throw error
      }
    },
    async delete(key) {
      await rm(pathFor(key), { force: true })
    },
  }
}
