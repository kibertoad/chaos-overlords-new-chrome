import { env, runDurableObjectAlarm, runInDurableObject } from 'cloudflare:test'
import { defineRateLimitStoreConformance } from '@chaos-overlords/conformance'
import { createApp } from '@chaos-overlords/server'
import { describe, expect, it } from 'vitest'
import { buildContainer } from '../src/index'
import { durableObjectRateLimitStore, type RateLimitCounter } from '../src/RateLimitCounter'

describe('rate limit counter objects', () => {
  defineRateLimitStoreConformance({
    createStore: async () => durableObjectRateLimitStore(env.RATE_LIMITS),
  })

  it('keeps a minute window in memory and a day window in storage until its alarm', async () => {
    const store = durableObjectRateLimitStore(env.RATE_LIMITS)
    const now = Date.now()
    const minute = `test|minute-${crypto.randomUUID()}`
    const day = `test|day-${crypto.randomUUID()}`
    await store.consume(minute, { limit: 5, windowMs: 60_000 }, now)
    await store.consume(day, { limit: 5, windowMs: 24 * 60 * 60 * 1000 }, now)

    const stub = (key: string) => env.RATE_LIMITS.get(env.RATE_LIMITS.idFromName(key))
    const stored = (key: string) =>
      runInDurableObject(stub(key) as never, async (_instance: RateLimitCounter, state) => ({
        window: await state.storage.get('window'),
        alarm: await state.storage.getAlarm(),
      }))
    expect(await stored(minute)).toEqual({ window: undefined, alarm: null })
    const persisted = await stored(day)
    expect(persisted.window).toMatchObject({ count: 1, windowStart: now })
    expect(persisted.alarm).toBe(now + 24 * 60 * 60 * 1000)

    // An alarm that fires early re-arms for the window's end and keeps the count.
    expect(await runDurableObjectAlarm(stub(day) as never)).toBe(true)
    expect((await stored(day)).window).toMatchObject({ count: 1 })
  })

  it('forgets a persisted window when its alarm fires after it rolled', async () => {
    const store = durableObjectRateLimitStore(env.RATE_LIMITS)
    const key = `test|rolled-${crypto.randomUUID()}`
    // A long window that opened long enough ago to have rolled by now.
    const windowMs = 2 * 60_000
    await store.consume(key, { limit: 5, windowMs }, Date.now() - 3 * 60_000)
    const stub = env.RATE_LIMITS.get(env.RATE_LIMITS.idFromName(key))
    // The alarm was set in the past, so the runtime may already have run it; run it if not.
    await runDurableObjectAlarm(stub as never)
    const left = await runInDurableObject(stub as never, async (_i: RateLimitCounter, state) =>
      state.storage.get('window'),
    )
    expect(left).toBeUndefined()
    expect(await store.inspect(key, Date.now())).toBeNull()
  })
})

describe('rate limits across isolates', () => {
  /**
   * Two containers stand in for two isolates: each builds its own limiters, and with `RATE_LIMITS`
   * bound they count in the same objects, so a caller spends one budget between them.
   */
  it('spends one anonymous budget across two containers', async () => {
    const limited = { ...env, RATE_LIMIT_PER_MINUTE: '2' } as typeof env
    const apps = [createApp(buildContainer(limited)), createApp(buildContainer(limited))]
    const address = `198.51.100.${Math.floor(Math.random() * 250) + 1}`
    const join = (index: number) =>
      (apps[index] as ReturnType<typeof createApp>).request(
        '/api/v1/matches/join',
        {
          method: 'POST',
          body: JSON.stringify({ joinCode: 'ABCDEFGH', displayName: 'Mallory' }),
          headers: { 'content-type': 'application/json', 'cf-connecting-ip': address },
        },
        limited,
      )
    expect((await join(0)).status).toBe(404)
    expect((await join(1)).status).toBe(404)
    const refused = await join(0)
    expect(refused.status).toBe(429)
    expect(Number(refused.headers.get('retry-after'))).toBeGreaterThan(0)
    expect((await join(1)).status).toBe(429)
  })
})
