import { defineStorageConformance } from '@chaos-overlords/conformance'
import pg from 'pg'
import { afterAll, beforeAll, describe } from 'vitest'
import { type OpenedStorage, openPostgresStorage } from '../src/node'

const url = process.env.TEST_DATABASE_URL

/** Needs a reachable Postgres: `docker compose up -d` at the workspace root sets one up. */
describe.skipIf(!url)('postgres', () => {
  let opened: OpenedStorage | undefined

  /**
   * The suite's contract is a migrated, EMPTY database. Unlike the in-memory SQLite file, a Postgres
   * service outlives the run, and leftovers from a previous one would drift into the global queries
   * (the lobby listing, expired turns) that this suite asserts on.
   */
  beforeAll(async () => {
    opened = await openPostgresStorage(url as string)
    const pool = new pg.Pool({ connectionString: url })
    try {
      await pool.query('TRUNCATE TABLE matches CASCADE')
    } finally {
      await pool.end()
    }
  })

  afterAll(async () => {
    await opened?.close()
  })

  defineStorageConformance({
    createStorage: async () => (opened as OpenedStorage).storage,
  })
})
