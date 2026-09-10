import { defineHttpConformance } from '@chaos-overlords/conformance'
import { createKernel, RateLimiter } from '@chaos-overlords/kernel'
import {
  InMemoryStorage,
  ManualClock,
  RecordingLogger,
  RecordingScheduler,
} from '@chaos-overlords/kernel/testing'
import { describe, expect, it } from 'vitest'
import { createApp, DEFAULT_SERVER_CONFIG, LocalEventHub, type ServerContainer } from '../src'

function build(
  overrides: Partial<ServerContainer['config']> = {},
  rateLimit = { limit: 1000, windowMs: 60_000 },
  memberRateLimit = { limit: 1000, windowMs: 60_000 },
) {
  const storage = new InMemoryStorage()
  const clock = new ManualClock()
  const hub = new LocalEventHub(storage.events, 50)
  const kernel = createKernel({
    storage,
    notifier: hub,
    scheduler: new RecordingScheduler(),
    clock,
    logger: new RecordingLogger(),
  })
  const container: ServerContainer = {
    kernel,
    eventStream: hub,
    rateLimiters: {
      anonymous: new RateLimiter(clock, rateLimit),
      member: new RateLimiter(clock, memberRateLimit),
      upload: new RateLimiter(clock, memberRateLimit),
    },
    config: { ...DEFAULT_SERVER_CONFIG, publicListing: true, ...overrides },
  }
  const app = createApp(container)
  return { app, clock, kernel, hub }
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
