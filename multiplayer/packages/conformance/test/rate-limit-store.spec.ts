import { MemoryRateLimitStore } from '@chaos-overlords/kernel'
import { describe } from 'vitest'
import { defineRateLimitStoreConformance } from '../src/rateLimits'

/** The reference store runs the suite the Postgres table and the Durable Object run. */
describe('in-memory reference rate limit store', () => {
  defineRateLimitStoreConformance({ createStore: async () => new MemoryRateLimitStore() })
})
