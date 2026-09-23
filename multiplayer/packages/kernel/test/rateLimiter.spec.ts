import { RateLimiter } from '../src'
import { ManualClock } from '../src/testing'
import { describe, expect, it } from 'vitest'

describe('RateLimiter reservations', () => {
  it('refunds a failed request once without touching a later window', () => {
    const clock = new ManualClock()
    const limiter = new RateLimiter(clock, { limit: 1, windowMs: 1_000 })
    const release = limiter.reserve('key')
    expect(release).not.toBeNull()
    expect(limiter.reserve('key')).toBeNull()

    release?.()
    expect(limiter.spent('key')).toBe(0)
    expect(limiter.reserve('key')).not.toBeNull()
    release?.()
    expect(limiter.spent('key')).toBe(1)

    clock.advance(1_000)
    expect(limiter.reserve('key')).not.toBeNull()
    release?.()
    expect(limiter.spent('key')).toBe(1)
  })
})

describe('RateLimiter residency bound', () => {
  it('evicts an older window rather than a rolled, currently limited one', () => {
    const clock = new ManualClock()
    const limiter = new RateLimiter(clock, { limit: 1, windowMs: 1_000, maxKeys: 3 })
    limiter.take('active')

    // Two windows open one millisecond after active's, filling the bound exactly without evicting.
    clock.advance(1)
    limiter.take('older-a')
    limiter.take('older-b')

    // Active rolls and its new window spends the whole budget; both older windows are still live.
    clock.advance(999)
    expect(limiter.take('active')).toBeNull()

    // One more key forces a single eviction. The renewed window is the newest, so the head must be
    // older-a; leaving active at its first-seen position forgave its spent budget instead.
    limiter.take('fresh')

    expect(limiter.take('active')).toBe(1)
    expect(limiter.spent('older-a')).toBe(0)
    expect(limiter.spent('older-b')).toBe(1)
  })
})

describe('RateLimiter clock steps', () => {
  it('does not stretch a window when the wall clock steps backwards', () => {
    const clock = new ManualClock()
    const limiter = new RateLimiter(clock, { limit: 1, windowMs: 1_000 })
    limiter.take('key')

    clock.advance(-60_000)
    expect(limiter.take('key')).toBe(1)

    // Measured on the raw wall clock this window would stay spent for another minute.
    clock.advance(1_000)
    expect(limiter.take('key')).toBeNull()
  })
})
