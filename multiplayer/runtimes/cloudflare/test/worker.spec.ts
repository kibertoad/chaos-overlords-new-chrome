import { env, SELF } from 'cloudflare:test'
import { defineHttpConformance, defineStorageConformance } from '@chaos-overlords/conformance'
import { createSqliteStorage, sqliteSchema } from '@chaos-overlords/storage/sqlite'
import { drizzle } from 'drizzle-orm/d1'
import { describe } from 'vitest'

describe('D1', () => {
  defineStorageConformance({
    createStorage: async () => createSqliteStorage(drizzle(env.DB, { schema: sqliteSchema })),
  })
})

describe('worker facade', () => {
  defineHttpConformance({
    fetch: (input, init) => SELF.fetch(input, init),
    publicListing: true,
  })
})
