import { defineStorageConformance } from '@chaos-overlords/conformance'
import { describe } from 'vitest'
import { openSqliteStorage } from '../src/node'

describe('better-sqlite3 (in-memory)', () => {
  const opened = openSqliteStorage(':memory:')
  defineStorageConformance({ createStorage: async () => opened.storage })
})
