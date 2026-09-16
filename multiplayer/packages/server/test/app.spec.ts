import type { BugReportRepository, StoredBugReport } from '@chaos-overlords/bug-reports'
import {
  type BugReportService,
  createBugReportService,
  createMemoryBlobStore,
} from '@chaos-overlords/bug-reports'
import { defineHttpConformance } from '@chaos-overlords/conformance'
import { createKernel, RateLimiter, sha256Hex } from '@chaos-overlords/kernel'
import {
  InMemoryStorage,
  ManualClock,
  RecordingLogger,
  RecordingScheduler,
} from '@chaos-overlords/kernel/testing'
import { describe, expect, it } from 'vitest'
import { createApp, DEFAULT_SERVER_CONFIG, LocalEventHub, type ServerContainer } from '../src'

/** The repository contract without a database; the bug report placement rules are tested in its own package. */
function inMemoryBugReports(): BugReportRepository & { rows: StoredBugReport[] } {
  const rows: StoredBugReport[] = []
  return {
    rows,
    async insert(report) {
      rows.push(report)
    },
    async get(id) {
      return rows.find((row) => row.id === id) ?? null
    },
    async list(limit) {
      return rows.slice(0, limit)
    },
    async bytesSince(since) {
      return rows
        .filter((row) => row.receivedAt >= since)
        .reduce((total, row) => total + (row.state?.compressedBytes ?? 0), 0)
    },
    async deleteBefore(before, limit) {
      const doomed = rows.filter((row) => row.receivedAt < before).slice(0, limit)
      for (const row of doomed) rows.splice(rows.indexOf(row), 1)
      return doomed.flatMap((row) => (row.state?.blobKey ? [row.state.blobKey] : []))
    },
  }
}

function build(
  overrides: Partial<ServerContainer['config']> = {},
  rateLimit = { limit: 1000, windowMs: 60_000 },
  memberRateLimit = { limit: 1000, windowMs: 60_000 },
  bugReportOptions: { enabled?: boolean; limit?: number } = {},
) {
  const storage = new InMemoryStorage()
  const clock = new ManualClock()
  const hub = new LocalEventHub(storage.events, 50)
  const kernel = createKernel({
    storage,
    notifier: hub,
    streams: hub,
    scheduler: new RecordingScheduler(),
    clock,
    logger: new RecordingLogger(),
  })
  const reports = inMemoryBugReports()
  const bugReports: BugReportService | undefined =
    bugReportOptions.enabled === false
      ? undefined
      : createBugReportService({
          repository: reports,
          clock,
          logger: new RecordingLogger(),
          blobs: createMemoryBlobStore(),
        })
  const container: ServerContainer = {
    kernel,
    ...(bugReports ? { bugReports } : {}),
    eventStream: hub,
    rateLimiters: {
      anonymous: new RateLimiter(clock, rateLimit),
      member: new RateLimiter(clock, memberRateLimit),
      upload: new RateLimiter(clock, memberRateLimit),
      bugReport: new RateLimiter(clock, {
        limit: bugReportOptions.limit ?? 1000,
        windowMs: 60_000,
      }),
    },
    config: { ...DEFAULT_SERVER_CONFIG, publicListing: true, ...overrides },
  }
  const app = createApp(container)
  return { app, clock, kernel, hub, reports }
}

describe('server app over in-memory storage', () => {
  const { app, clock, kernel } = build()
  defineHttpConformance({
    fetch: async (input, init) => app.request(input, init),
    publicListing: true,
    expireDeadlines: async () => {
      clock.advance(60_000)
      await kernel.turns.sweep()
    },
  })

  it('rate-limits the unauthenticated doors per client address', async () => {
    const limited = build({}, { limit: 2, windowMs: 60_000 })
    const body = JSON.stringify({ joinCode: 'ABCDEFGH', displayName: 'x' })
    const headers = {
      'content-type': 'application/json',
      'x-forwarded-for': '203.0.113.9, 10.0.0.1',
    }
    const first = await limited.app.request('/api/v1/matches/join', {
      method: 'POST',
      body,
      headers,
    })
    expect(first.status).toBe(404)
    await limited.app.request('/api/v1/matches/join', { method: 'POST', body, headers })
    const third = await limited.app.request('/api/v1/matches/join', {
      method: 'POST',
      body,
      headers,
    })
    expect(third.status).toBe(429)
    expect(third.headers.get('retry-after')).toMatch(/^\d+$/)
    expect(await third.json()).toMatchObject({
      error: { code: 'rate_limited', details: { reason: 'rate_limited' } },
    })
    const otherClient = await limited.app.request('/api/v1/matches/join', {
      method: 'POST',
      body,
      headers: { ...headers, 'x-forwarded-for': '198.51.100.1' },
    })
    expect(otherClient.status).toBe(404)
  })

  /**
   * The member limiter is keyed by player and cannot run before there is one, so a bad token used to
   * cost a SHA-256 and an indexed lookup against no budget at all. Guessing a 256-bit token is not
   * the worry; driving database reads at line rate is.
   */
  it('charges a failed authentication to the caller address', async () => {
    const limited = build({}, { limit: 2, windowMs: 60_000 })
    const headers = { authorization: 'Bearer not-a-real-token', 'x-forwarded-for': '203.0.113.5' }
    const path = '/api/v1/matches/00000000-0000-4000-8000-000000000000'
    expect((await limited.app.request(path, { headers })).status).toBe(401)
    expect((await limited.app.request(path, { headers })).status).toBe(401)
    const third = await limited.app.request(path, { headers })
    expect(third.status).toBe(429)

    // A caller who never sent a credential at all is charged the same: the cost is the same.
    const missing = build({}, { limit: 1, windowMs: 60_000 })
    expect((await missing.app.request(path, { headers: {} })).status).toBe(401)
    expect((await missing.app.request(path, { headers: {} })).status).toBe(429)
  })

  it('rate-limits an authenticated member per player, not per address', async () => {
    const limited = build({}, { limit: 1000, windowMs: 60_000 }, { limit: 2, windowMs: 60_000 })
    const created = await limited.app.request('/api/v1/matches', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        settings: {
          name: 'x',
          maxPlayers: 2,
          turnTimerSeconds: 0,
          visibility: 'private',
          gameSettings: {},
        },
        hostDisplayName: 'h',
      }),
    })
    const { token, match, joinCode } = (await created.json()) as {
      token: string
      match: { id: string }
      joinCode: string
    }
    const read = () =>
      limited.app.request(`/api/v1/matches/${match.id}`, {
        headers: { authorization: `Bearer ${token}` },
      })
    expect((await read()).status).toBe(200)
    expect((await read()).status).toBe(200)
    const throttled = await read()
    expect(throttled.status).toBe(429)
    expect(throttled.headers.get('retry-after')).toMatch(/^\d+$/)

    // Another member of the same match has their own budget.
    const joined = await limited.app.request('/api/v1/matches/join', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ joinCode, displayName: 'g' }),
    })
    const guest = (await joined.json()) as { token: string }
    const guestRead = await limited.app.request(`/api/v1/matches/${match.id}`, {
      headers: { authorization: `Bearer ${guest.token}` },
    })
    expect(guestRead.status).toBe(200)
  })

  /**
   * `use(path, ...)` matches its path verbatim, so `/matches` and `/matches/join` cover neither
   * each other nor this third unauthenticated door. It was left with no throttle and no cap on a
   * body that is buffered before anything validates it.
   */
  it('rate-limits and caps the body of the late-join door', async () => {
    const limited = build({}, { limit: 2, windowMs: 60_000 })
    const body = JSON.stringify({ match: 'ABCDEFGH', displayName: 'x', slot: 3 })
    const headers = {
      'content-type': 'application/json',
      'x-forwarded-for': '203.0.113.21, 10.0.0.1',
    }
    const first = await limited.app.request('/api/v1/matches/join-running', {
      method: 'POST',
      body,
      headers,
    })
    expect(first.status).toBe(404)
    await limited.app.request('/api/v1/matches/join-running', { method: 'POST', body, headers })
    const third = await limited.app.request('/api/v1/matches/join-running', {
      method: 'POST',
      body,
      headers,
    })
    expect(third.status).toBe(429)

    const huge = await limited.app.request('/api/v1/matches/join-running', {
      method: 'POST',
      headers: { ...headers, 'x-forwarded-for': '198.51.100.7' },
      body: JSON.stringify({ match: 'ABCDEFGH', displayName: 'y'.repeat(64 * 1024), slot: 3 }),
    })
    expect(huge.status).toBe(413)
    expect(await huge.json()).toMatchObject({ error: { code: 'payload_too_large' } })
  })

  it('refuses an oversized order document before parsing it', async () => {
    const hostRes = await app.request('/api/v1/matches', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        settings: {
          name: 'x',
          maxPlayers: 2,
          turnTimerSeconds: 0,
          visibility: 'private',
          gameSettings: {},
        },
        hostDisplayName: 'h',
      }),
    })
    const { token, match } = (await hostRes.json()) as { token: string; match: { id: string } }
    const huge = await app.request(`/api/v1/matches/${match.id}/turns/1/orders`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
      body: JSON.stringify({
        ready: true,
        orders: { schemaVersion: 1, ops: [{ op: 'x', args: { s: 'y'.repeat(300 * 1024) } }] },
      }),
    })
    expect(huge.status).toBe(413)
    expect(await huge.json()).toMatchObject({ error: { code: 'payload_too_large' } })
  })

  it('closes the stream and drops the listener when the client disconnects', async () => {
    const { app: fresh, hub } = build()
    const created = await fresh.request('/api/v1/matches', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        settings: {
          name: 'x',
          maxPlayers: 2,
          turnTimerSeconds: 0,
          visibility: 'private',
          gameSettings: {},
        },
        hostDisplayName: 'h',
      }),
    })
    const { token, match } = (await created.json()) as { token: string; match: { id: string } }
    const controller = new AbortController()
    const response = await fresh.request(`/api/v1/matches/${match.id}/stream`, {
      headers: { authorization: `Bearer ${token}` },
      signal: controller.signal,
    })
    expect(response.headers.get('content-type')).toContain('text/event-stream')
    const reader = (response.body as ReadableStream<Uint8Array>).getReader()
    const first = await reader.read()
    expect(new TextDecoder().decode(first.value)).toContain('connected')
    expect(hub.connectionCount(match.id)).toBe(1)
    controller.abort()
    // The wrapped body drains what was already queued, then learns the source closed.
    let done = false
    for (let reads = 0; reads < 5 && !done; reads += 1) done = (await reader.read()).done
    expect(done).toBe(true)
    expect(hub.connectionCount(match.id)).toBe(0)
  })
})

describe('bug report intake', () => {
  const encodeBase64 = (bytes: Uint8Array) => {
    let binary = ''
    for (const byte of bytes) binary += String.fromCharCode(byte)
    return btoa(binary)
  }

  const post = (app: ReturnType<typeof build>['app'], body: unknown, headers = {}) =>
    app.request('/api/v1/bug-reports', {
      method: 'POST',
      headers: { 'content-type': 'application/json', ...headers },
      body: JSON.stringify(body),
    })

  const report = (extra: Record<string, unknown> = {}) => ({
    message: 'Hire offers were empty on turn 3.',
    client: { version: '0.9.1', platform: 'Unix' },
    ...extra,
  })

  it('takes a report without a token and answers a receipt', async () => {
    const { app, reports } = build()
    const response = await post(app, report())

    expect(response.status).toBe(201)
    expect(await response.json()).toMatchObject({ stateStored: 'not_sent' })
    expect(reports.rows[0]?.message).toBe('Hire offers were empty on turn 3.')
  })

  it('stores the attached journal and reports that it did', async () => {
    const { app, reports } = build()
    const bytes = new Uint8Array(96).fill(4)
    const response = await post(
      app,
      report({
        state: {
          codec: 'brotli',
          replayFormatVersion: 24,
          uncompressedBytes: 4_096,
          sha256: await sha256Hex(bytes),
          anonymized: true,
          body: encodeBase64(bytes),
        },
      }),
    )

    expect(response.status).toBe(201)
    expect(await response.json()).toMatchObject({ stateStored: 'stored' })
    expect(reports.rows[0]?.state).toMatchObject({ compressedBytes: 96, anonymized: true })
  })

  it('refuses a journal whose digest does not match the bytes', async () => {
    const { app, reports } = build()
    const response = await post(
      app,
      report({
        state: {
          codec: 'brotli',
          replayFormatVersion: 24,
          uncompressedBytes: 4_096,
          sha256: 'f'.repeat(64),
          anonymized: true,
          body: encodeBase64(new Uint8Array(8).fill(1)),
        },
      }),
    )

    expect(response.status).toBe(422)
    expect(await response.json()).toMatchObject({
      error: { code: 'validation_failed', details: { reason: 'state_digest_mismatch' } },
    })
    expect(reports.rows).toHaveLength(0)
  })

  it('refuses an empty message through the contract rather than storing one', async () => {
    const { app, reports } = build()
    const response = await post(app, report({ message: '   ' }))

    expect(response.status).toBe(422)
    expect(reports.rows).toHaveLength(0)
  })

  it('answers 404 on a server that does not take reports', async () => {
    const { app } = build({}, undefined, undefined, { enabled: false })
    const response = await post(app, report())

    expect(response.status).toBe(404)
    expect(await response.json()).toMatchObject({
      error: { code: 'not_found', details: { reason: 'bug_reports_disabled' } },
    })
  })

  it('spends its own budget rather than the lobby one', async () => {
    const limited = build({}, { limit: 1000, windowMs: 60_000 }, undefined, { limit: 1 })
    const headers = { 'x-forwarded-for': '203.0.113.42' }

    expect((await post(limited.app, report(), headers)).status).toBe(201)
    const throttled = await post(limited.app, report(), headers)
    expect(throttled.status).toBe(429)

    // The unauthenticated lobby door is untouched by the reports above.
    const join = await limited.app.request('/api/v1/matches/join', {
      method: 'POST',
      headers: { 'content-type': 'application/json', ...headers },
      body: JSON.stringify({ joinCode: 'ABCDEFGH', displayName: 'x' }),
    })
    expect(join.status).toBe(404)
  })
})
