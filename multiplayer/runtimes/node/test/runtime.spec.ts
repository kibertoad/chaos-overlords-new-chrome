import { mkdtemp, rm } from 'node:fs/promises'
import type { AddressInfo } from 'node:net'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { defineHttpConformance } from '@chaos-overlords/conformance'
import { DEFAULT_RETENTION_DAYS } from '@chaos-overlords/kernel'
import { ManualClock } from '@chaos-overlords/kernel/testing'
import { type ServerType, serve } from '@hono/node-server'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { buildNodeRuntime, loadConfig, type NodeRuntime } from '../src'

/**
 * The facade over a real HTTP listener: the client SDK's fetch goes over TCP, so the streaming
 * headers, the abort path and the JSON round-trip are the production ones, not app.request().
 *
 * The clock is the one injected seam. A turn timer is at least thirty seconds by contract, so the
 * deadline path could otherwise only be tested by waiting; with a hand-driven clock the whole route
 * — expired turn, sweep, seal, next turn — runs against real storage over real HTTP.
 */
function defineFacadeSuite(name: string, databaseUrl: string | undefined): void {
  describe.skipIf(!databaseUrl)(name, () => {
    let runtime: NodeRuntime
    let server: ServerType
    let baseUrl = ''
    const clock = new ManualClock()

    beforeAll(async () => {
      runtime = await buildNodeRuntime(
        loadConfig({
          DATABASE_URL: databaseUrl,
          PUBLIC_LISTING: 'true',
          LOG_LEVEL: 'error',
          RATE_LIMIT_PER_MINUTE: '10000',
          MEMBER_RATE_LIMIT_PER_MINUTE: '10000',
          UPLOAD_RATE_LIMIT_PER_MINUTE: '10000',
          BUG_REPORT_RATE_LIMIT_PER_MINUTE: '10000',
        }),
        { clock },
      )
      server = serve({ fetch: runtime.app.fetch, hostname: '127.0.0.1', port: 0 })
      await new Promise<void>((resolve) => server.once('listening', () => resolve()))
      baseUrl = `http://127.0.0.1:${(server.address() as AddressInfo).port}`
    })

    afterAll(async () => {
      await new Promise<void>((resolve) => server.close(() => resolve()))
      await runtime.close()
    })

    defineHttpConformance({
      fetch: (input, init) => fetch(input.replace('http://conformance', baseUrl), init),
      publicListing: true,
      expireDeadlines: async () => {
        clock.advance(61_000)
        await runtime.kernel.turns.sweep()
      },
    })
  })
}

describe('node runtime configuration', () => {
  it('refuses budgets and intervals that would admit one call or spin', () => {
    expect(() => loadConfig({ RATE_LIMIT_PER_MINUTE: '0' })).toThrow(/at least 1/)
    expect(() => loadConfig({ MEMBER_RATE_LIMIT_PER_MINUTE: '0' })).toThrow(/at least 1/)
    expect(() => loadConfig({ SWEEP_INTERVAL_MS: '0' })).toThrow(/at least 1000/)
    expect(() => loadConfig({ MAX_EVENT_STREAMS: '0' })).toThrow(/at least 1/)
    expect(loadConfig({ RETENTION_DAYS: '0' }).retentionDays).toBe(0)
  })

  it('reads every retention window, defaulting to the kernel public-server values', () => {
    const defaults = loadConfig({})
    expect(defaults.retentionDays).toBe(DEFAULT_RETENTION_DAYS.finished)
    // Unset rather than defaulted, so the kernel can follow `RETENTION_DAYS=0` for lobbies.
    expect(defaults.lobbyRetentionDays).toBeUndefined()
    expect(defaults.abandonedRetentionDays).toBe(DEFAULT_RETENTION_DAYS.abandonedLive)
    // Unset rather than defaulted, so the kernel can derive it from the abandoned window.
    expect(defaults.silentRetentionDays).toBeUndefined()
    expect(defaults.retentionBatchSize).toBeUndefined()

    const tuned = loadConfig({
      RETENTION_DAYS: '365',
      LOBBY_RETENTION_DAYS: '0',
      ABANDONED_RETENTION_DAYS: '120',
      SILENT_RETENTION_DAYS: '400',
      RETENTION_BATCH_SIZE: '5',
      RETENTION_INTERVAL_MS: '300000',
    })
    expect(tuned).toMatchObject({
      retentionDays: 365,
      lobbyRetentionDays: 0,
      abandonedRetentionDays: 120,
      silentRetentionDays: 400,
      retentionBatchSize: 5,
      retentionIntervalMs: 300_000,
    })
    expect(() => loadConfig({ RETENTION_BATCH_SIZE: '0' })).toThrow(/at least 1/)
    expect(() => loadConfig({ RETENTION_INTERVAL_MS: '10' })).toThrow(/at least 1000/)
    expect(() => loadConfig({ LOBBY_RETENTION_DAYS: '-1' })).toThrow(/at least 0/)
  })

  it('serves the public lobby list unless it is explicitly turned off', () => {
    expect(loadConfig({}).publicListing).toBe(true)
    expect(loadConfig({ PUBLIC_LISTING: 'false' }).publicListing).toBe(false)
    expect(loadConfig({ PUBLIC_LISTING: 'true' }).publicListing).toBe(true)
    expect(loadConfig({ PUBLIC_LISTING: 'TRUE' }).publicListing).toBe(true)
    expect(loadConfig({ PUBLIC_LISTING: 'yes' }).publicListing).toBe(true)
    expect(loadConfig({ PUBLIC_LISTING: 'FALSE' }).publicListing).toBe(false)
    expect(loadConfig({ PUBLIC_LISTING: '0' }).publicListing).toBe(false)
    expect(() => loadConfig({ PUBLIC_LISTING: 'sometimes' })).toThrow(/Expected a boolean/)
  })

  it('reads browser origins as a trimmed list, none by default', () => {
    expect(loadConfig({}).corsOrigins).toEqual([])
    expect(
      loadConfig({ CORS_ORIGINS: ' https://a.example, https://b.example ,' }).corsOrigins,
    ).toEqual(['https://a.example', 'https://b.example'])
  })
})

defineFacadeSuite('node runtime over sqlite', 'sqlite::memory:')
// `REQUIRE_POSTGRES` turns a missing URL from a green skip into a failure, for the CI and release
// jobs that stand a Postgres service up. See the note in `packages/storage/test/postgres.spec.ts`.
if (process.env.REQUIRE_POSTGRES === '1' && !process.env.TEST_DATABASE_URL) {
  throw new Error('REQUIRE_POSTGRES=1 but TEST_DATABASE_URL is empty.')
}
defineFacadeSuite('node runtime over postgres', process.env.TEST_DATABASE_URL)

/**
 * Bug reports, over the same listener and into a second database file.
 *
 * On disk rather than in memory, because the thing worth asserting is that the intake opens and
 * migrates a database of its own beside the match one rather than sharing it.
 */
describe('node runtime bug report intake', () => {
  let directory = ''
  let runtime: NodeRuntime
  let server: ServerType
  let baseUrl = ''

  beforeAll(async () => {
    directory = await mkdtemp(join(tmpdir(), 'chaos-node-bug-reports-'))
    runtime = await buildNodeRuntime(
      loadConfig({
        DATABASE_URL: 'sqlite::memory:',
        BUG_REPORT_DATABASE_URL: `sqlite:${join(directory, 'bug-reports.db')}`,
        BUG_REPORT_BLOB_DIR: join(directory, 'journals'),
        LOG_LEVEL: 'error',
      }),
    )
    server = serve({ fetch: runtime.app.fetch, hostname: '127.0.0.1', port: 0 })
    await new Promise<void>((resolve) => server.once('listening', () => resolve()))
    baseUrl = `http://127.0.0.1:${(server.address() as AddressInfo).port}`
  })

  afterAll(async () => {
    await new Promise<void>((resolve) => server.close(() => resolve()))
    await runtime.close()
    await rm(directory, { recursive: true, force: true })
  })

  it('accepts a report without a token and keeps the journal outside the database', async () => {
    const bytes = new Uint8Array(64).fill(9)
    const sha256 = [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))]
      .map((byte) => byte.toString(16).padStart(2, '0'))
      .join('')

    const response = await fetch(`${baseUrl}/api/v1/bug-reports`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        message: 'Gangs stopped moving.',
        client: { version: '0.9.1', platform: 'Unix' },
        context: {
          scenario: 'Greed',
          matchType: 'single',
          turn: 4,
          phase: 'Command',
          humanPlayers: 1,
          computerPlayers: 5,
          aiPolicy: 'Advanced',
        },
        state: {
          codec: 'brotli',
          replayFormatVersion: 24,
          uncompressedBytes: 2_048,
          sha256,
          anonymized: true,
          body: Buffer.from(bytes).toString('base64'),
        },
      }),
    })

    expect(response.status).toBe(201)
    const receipt = (await response.json()) as { id: string; stateStored: string }
    expect(receipt.stateStored).toBe('stored')

    const stored = await runtime.bugReports?.get(receipt.id)
    expect(stored?.message).toBe('Gangs stopped moving.')
    expect(stored?.state?.body).toBeNull()
    expect(stored?.state?.blobKey).toMatch(/^bug-reports\//)
    expect(await runtime.bugReports?.archive(receipt.id)).toEqual(bytes)
  })

  it('refuses a journal whose digest does not match its bytes', async () => {
    const response = await fetch(`${baseUrl}/api/v1/bug-reports`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        message: 'Truncated.',
        client: { version: '0.9.1', platform: 'Unix' },
        state: {
          codec: 'brotli',
          replayFormatVersion: 24,
          uncompressedBytes: 2_048,
          sha256: 'a'.repeat(64),
          anonymized: true,
          body: Buffer.from(new Uint8Array(8)).toString('base64'),
        },
      }),
    })

    expect(response.status).toBe(422)
  })
})

/** An intake that is switched off says so, rather than half-working. */
describe('node runtime without a bug report database', () => {
  let runtime: NodeRuntime
  let server: ServerType
  let baseUrl = ''

  beforeAll(async () => {
    runtime = await buildNodeRuntime(
      loadConfig({
        DATABASE_URL: 'sqlite::memory:',
        BUG_REPORT_DATABASE_URL: '',
        LOG_LEVEL: 'error',
      }),
    )
    server = serve({ fetch: runtime.app.fetch, hostname: '127.0.0.1', port: 0 })
    await new Promise<void>((resolve) => server.once('listening', () => resolve()))
    baseUrl = `http://127.0.0.1:${(server.address() as AddressInfo).port}`
  })

  afterAll(async () => {
    await new Promise<void>((resolve) => server.close(() => resolve()))
    await runtime.close()
  })

  it('answers 404 and leaves the rest of the server alone', async () => {
    expect(runtime.bugReports).toBeUndefined()

    const response = await fetch(`${baseUrl}/api/v1/bug-reports`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        message: 'Nowhere to put this.',
        client: { version: '0.9.1', platform: 'Unix' },
      }),
    })

    expect(response.status).toBe(404)
    expect(await response.json()).toMatchObject({
      error: { details: { reason: 'bug_reports_disabled' } },
    })
    expect((await fetch(`${baseUrl}/health`)).status).toBe(200)
  })
})
