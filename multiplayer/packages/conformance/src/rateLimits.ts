import type { RateLimitStore } from '@chaos-overlords/kernel'
import { describe, expect, it } from 'vitest'

export interface RateLimitStoreConformanceHarness {
  /** A store per call, or one whose keys never collide across calls; the suite uses fresh keys. */
  createStore(): Promise<RateLimitStore>
}

let counter = 0
const freshKey = (): string => {
  counter += 1
  return `conformance|${Date.now().toString(36)}-${counter}-${crypto.randomUUID()}`
}

/**
 * The contract every {@link RateLimitStore} keeps, so a budget means the same thing in a Postgres
 * row, a Durable Object and the in-memory reference. Times are passed in, so nothing here waits.
 */
export function defineRateLimitStoreConformance(harness: RateLimitStoreConformanceHarness): void {
  // Far from the wall clock, so a sweep another suite runs against the same table never reaches it.
  const T0 = 4_000_000_000_000
  const policy = { limit: 3, windowMs: 60_000 }

  describe('rate limit store conformance', () => {
    it('counts calls up to the limit, then refuses without counting', async () => {
      const store = await harness.createStore()
      const key = freshKey()
      const answers = []
      for (let i = 0; i < 5; i++) answers.push(await store.consume(key, policy, T0 + i))
      expect(answers.map((a) => a.allowed)).toEqual([true, true, true, false, false])
      expect(answers.map((a) => a.count)).toEqual([1, 2, 3, 3, 3])
      for (const answer of answers) {
        expect(answer.windowStart).toBe(T0)
        expect(answer.resetAt).toBe(T0 + policy.windowMs)
      }
      expect(await store.inspect(key, T0 + 10)).toEqual({ count: 3, resetAt: T0 + policy.windowMs })
    })

    it('opens a fresh window once the old one has rolled', async () => {
      const store = await harness.createStore()
      const key = freshKey()
      for (let i = 0; i < 3; i++) await store.consume(key, policy, T0)
      expect(await store.inspect(key, T0 + policy.windowMs)).toBeNull()
      const next = await store.consume(key, policy, T0 + policy.windowMs)
      expect(next).toEqual({
        allowed: true,
        count: 1,
        windowStart: T0 + policy.windowMs,
        resetAt: T0 + 2 * policy.windowMs,
      })
    })

    it('keeps keys apart', async () => {
      const store = await harness.createStore()
      const a = freshKey()
      const b = freshKey()
      for (let i = 0; i < 3; i++) await store.consume(a, policy, T0)
      expect((await store.consume(b, policy, T0)).allowed).toBe(true)
      expect(await store.inspect(b, T0)).toEqual({ count: 1, resetAt: T0 + policy.windowMs })
    })

    it('answers nothing for a key never seen', async () => {
      const store = await harness.createStore()
      expect(await store.inspect(freshKey(), T0)).toBeNull()
    })

    it('refunds into the same window only', async () => {
      const store = await harness.createStore()
      const key = freshKey()
      const first = await store.consume(key, policy, T0)
      await store.consume(key, policy, T0)
      await store.refund(key, first.windowStart)
      expect(await store.inspect(key, T0)).toEqual({ count: 1, resetAt: T0 + policy.windowMs })

      // A refund that arrives after the window rolled leaves the new window alone.
      const later = T0 + policy.windowMs
      await store.consume(key, policy, later)
      await store.refund(key, first.windowStart)
      expect(await store.inspect(key, later)).toEqual({
        count: 1,
        resetAt: later + policy.windowMs,
      })
    })

    it('never refunds below zero', async () => {
      const store = await harness.createStore()
      const key = freshKey()
      const first = await store.consume(key, policy, T0)
      await store.refund(key, first.windowStart)
      await store.refund(key, first.windowStart)
      expect((await store.inspect(key, T0))?.count ?? 0).toBe(0)
      expect((await store.consume(key, policy, T0)).count).toBe(1)
    })

    it('counts concurrent calls exactly', async () => {
      const store = await harness.createStore()
      const key = freshKey()
      const wide = { limit: 10, windowMs: 60_000 }
      const answers = await Promise.all(
        Array.from({ length: 25 }, () => store.consume(key, wide, T0)),
      )
      expect(answers.filter((a) => a.allowed)).toHaveLength(10)
      expect(await store.inspect(key, T0)).toEqual({ count: 10, resetAt: T0 + wide.windowMs })
    })
  })
}
