import { InMemoryStorage } from '@chaos-overlords/kernel/testing'
import { describe } from 'vitest'
import { defineStorageConformance } from '../src/storage'

/**
 * The reference implementation runs the same suite as the SQL ones. The kernel and HTTP tests are
 * fast because they use it, which is only worth anything if it behaves like the real thing: the
 * conditional writes, the refusals on a taken key, the gapless event sequence.
 */
describe('in-memory reference storage', () => {
  defineStorageConformance({ createStorage: async () => new InMemoryStorage() })
})
