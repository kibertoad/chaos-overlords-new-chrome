import { RateLimiter } from '../src'
import { ManualClock } from '../src/testing'
import { describe, expect, it } from 'vitest'

describe('RateLimiter residency bound', () => {
  it('evicts an older window rather than a rolled, currently limited one', () => {
    const clock = new ManualClock()
    const limiter = new RateLimiter(clock, { limit: 1, windowMs: 1_000 })
    limiter.take('active')

    // These all begin later than active, so they remain live when active rolls one millisecond
    // before them. Filling the exact bound makes the next fresh window choose one to forgive.
    clock.advance(1)
    for (let index = 0; index < 50_000; index += 1) limiter.take(`older-${index}`)

    clock.advance(999)
    limiter.take('active')

    // The renewed window is fresh and spent. It must therefore remain while the earlier window at
    // the Map head is forgiven; leaving active at its first-seen position did the reverse.
    expect(limiter.take('active')).toBe(1)
    expect(limiter.spent('older-0')).toBe(0)
  })
})
