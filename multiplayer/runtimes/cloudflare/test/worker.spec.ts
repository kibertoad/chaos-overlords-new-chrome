import { env, SELF } from 'cloudflare:test'
import { defineHttpConformance, defineStorageConformance } from '@chaos-overlords/conformance'
import { createSqliteStorage, sqliteSchema } from '@chaos-overlords/storage/sqlite'
import { drizzle } from 'drizzle-orm/d1'
import { describe, expect, it } from 'vitest'
import { containerFor } from '../src/index'

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

describe('isolate state', () => {
  /**
   * The container must outlive a request. A rate limiter counts within a window, so rebuilding it per
   * request (which an earlier version did whenever the limit was configured) would reset the window
   * every time and limit nothing; the router and the D1-backed kernel have nothing request-specific
   * in them either.
   */
  it('builds one container and router per isolate, not per request', () => {
    const first = containerFor(env)
    expect(containerFor(env)).toBe(first)
    expect(containerFor(env).container.rateLimiters.anonymous).toBe(
      first.container.rateLimiters.anonymous,
    )
  })
})
