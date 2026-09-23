import { env, SELF } from 'cloudflare:test'
import { defineHttpConformance, defineStorageConformance } from '@chaos-overlords/conformance'
import { createSqliteStorage, sqliteSchema } from '@chaos-overlords/storage/sqlite'
import { drizzle } from 'drizzle-orm/d1'
import { describe, expect, it } from 'vitest'
import { buildContainer, containerFor } from '../src/index'
import { retentionPolicyFor } from '../src/kernel'

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

describe('bug reports', () => {
  /**
   * The intake is bound to its own D1 instance, so the row lands in `BUG_DB` and nothing about the
   * report touches the database holding matches.
   */
  it('files a report into its own database, without a token', async () => {
    const response = await SELF.fetch('https://worker/api/v1/bug-reports', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        message: 'Hire offers were empty on turn 3.',
        client: { version: '0.9.1', platform: 'Unix' },
      }),
    })

    expect(response.status).toBe(201)
    expect(await response.json()).toMatchObject({ stateStored: 'not_sent' })
    const stored = await env.BUG_DB.prepare(
      "select message from bug_reports where message = 'Hire offers were empty on turn 3.'",
    ).first<{ message: string }>()
    expect(stored?.message).toBe('Hire offers were empty on turn 3.')
    // The two lineages are applied to two databases: the match one has never heard of this table.
    await expect(env.DB.prepare('select 1 from bug_reports').first()).rejects.toThrow()
  })

  it('keeps a journal out of the row when a bucket is bound', async () => {
    const bytes = new Uint8Array(64).fill(7)
    const digest = await crypto.subtle.digest('SHA-256', bytes)
    const sha256 = [...new Uint8Array(digest)]
      .map((byte) => byte.toString(16).padStart(2, '0'))
      .join('')

    const response = await SELF.fetch('https://worker/api/v1/bug-reports', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        message: 'With a journal.',
        client: { version: '0.9.1', platform: 'Unix' },
        state: {
          codec: 'brotli',
          replayFormatVersion: 24,
          uncompressedBytes: 4_096,
          sha256,
          anonymized: true,
          body: btoa(String.fromCharCode(...bytes)),
        },
      }),
    })

    expect(response.status).toBe(201)
    expect(await response.json()).toMatchObject({ stateStored: 'stored' })
    const row = await env.BUG_DB.prepare(
      "select blob_key, body from bug_reports where message = 'With a journal.'",
    ).first<{ blob_key: string | null; body: string | null }>()
    expect(row?.body).toBeNull()
    expect(row?.blob_key).toMatch(/^bug-reports\//)
    const object = await env.BUG_BLOBS?.get(row?.blob_key ?? '')
    expect(new Uint8Array((await object?.arrayBuffer()) ?? new ArrayBuffer(0))).toEqual(bytes)
  })
})

describe('worker configuration', () => {
  /**
   * A deployment that never set the var still serves the list the game's Browse screen reads; only
   * an explicit `"false"` turns the route off.
   */
  it('serves the public lobby list unless PUBLIC_LISTING says otherwise', () => {
    const { PUBLIC_LISTING: _unset, ...unconfigured } = env
    expect(buildContainer(unconfigured).config.publicListing).toBe(true)
    expect(buildContainer({ ...env, PUBLIC_LISTING: 'true' }).config.publicListing).toBe(true)
    expect(buildContainer({ ...env, PUBLIC_LISTING: 'false' }).config.publicListing).toBe(false)
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

describe('retention vars', () => {
  const DAY_MS = 24 * 60 * 60 * 1000

  it('derives a mistyped silent window from the abandoned one instead of a fixed default', () => {
    const policy = retentionPolicyFor({
      ...env,
      ABANDONED_RETENTION_DAYS: '120',
      SILENT_RETENTION_DAYS: '360d',
    })
    expect(policy.silentLiveMaxAgeMs).toBe(360 * DAY_MS)
  })

  it('keeps lobbies when RETENTION_DAYS=0 unless LOBBY_RETENTION_DAYS says otherwise', () => {
    const { LOBBY_RETENTION_DAYS: _lobby, ...unset } = env
    expect(retentionPolicyFor({ ...unset, RETENTION_DAYS: '0' }).lobbyMaxAgeMs).toBe(0)
    expect(
      retentionPolicyFor({ ...unset, RETENTION_DAYS: '0', LOBBY_RETENTION_DAYS: '2' })
        .lobbyMaxAgeMs,
    ).toBe(2 * DAY_MS)
  })
})
