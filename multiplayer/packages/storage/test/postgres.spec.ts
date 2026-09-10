import { defineStorageConformance } from '@chaos-overlords/conformance'
import { afterAll, describe } from 'vitest'
import { type OpenedStorage, openPostgresStorage } from '../src/node'

const url = process.env.TEST_DATABASE_URL

/** Needs a reachable Postgres: `docker compose up -d` at the workspace root sets one up. */
describe.skipIf(!url)('postgres', () => {
  let opened: OpenedStorage | undefined
  afterAll(async () => {
    await opened?.close()
  })
  defineStorageConformance({
    createStorage: async () => {
      opened ??= await openPostgresStorage(url as string)
      return opened.storage
    },
  })
})
