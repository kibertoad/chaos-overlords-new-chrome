import {
  MemoryRateLimiter,
  MemoryRateLimitStore,
  type RateLimitStore,
  sharedRateLimiters,
} from '../src'
import { ManualClock, RecordingLogger } from '../src/testing'
import { describe, expect, it } from 'vitest'

describe('MemoryRateLimiter reservations', () => {
  it('refunds a failed request once without touching a later window', async () => {
    const clock = new ManualClock()
    const limiter = new MemoryRateLimiter(clock, { limit: 1, windowMs: 1_000 })
    const release = await limiter.reserve('key')
    expect(release).not.toBeNull()
    expect(await limiter.reserve('key')).toBeNull()

    await release?.()
    expect(await limiter.spent('key')).toBe(0)
    const held = await limiter.reserve('key')
    expect(held).not.toBeNull()
    await release?.()
    expect(await limiter.spent('key')).toBe(1)

    clock.advance(1_000)
    expect(await limiter.reserve('key')).not.toBeNull()
    await held?.()
    expect(await limiter.spent('key')).toBe(1)
  })
})

describe('MemoryRateLimiter residency bound', () => {
  it('evicts an older window rather than a rolled, currently limited one', async () => {
    const clock = new ManualClock()
    const limiter = new MemoryRateLimiter(clock, { limit: 1, windowMs: 1_000, maxKeys: 3 })
    await limiter.take('active')

    // Two windows open one millisecond after active's, filling the bound exactly without evicting.
    clock.advance(1)
    await limiter.take('older-a')
    await limiter.take('older-b')

    // Active rolls and its new window spends the whole budget; both older windows are still live.
    clock.advance(999)
    expect(await limiter.take('active')).toBeNull()

    // One more key forces a single eviction. The renewed window is the newest, so the head must be
    // older-a; leaving active at its first-seen position forgave its spent budget instead.
    await limiter.take('fresh')

    expect(await limiter.take('active')).toBe(1)
    expect(await limiter.spent('older-a')).toBe(0)
    expect(await limiter.spent('older-b')).toBe(1)
  })
})

describe('MemoryRateLimiter clock steps', () => {
  it('does not stretch a window when the wall clock steps backwards', async () => {
    const clock = new ManualClock()
    const limiter = new MemoryRateLimiter(clock, { limit: 1, windowMs: 1_000 })
    await limiter.take('key')

    clock.advance(-60_000)
    expect(await limiter.take('key')).toBe(1)

    // Measured on the raw wall clock this window would stay spent for another minute.
    clock.advance(1_000)
    expect(await limiter.take('key')).toBeNull()
  })
})

describe('SharedRateLimiter', () => {
  it('keeps the in-process contract on a shared store', async () => {
    const clock = new ManualClock()
    const limiters = sharedRateLimiters(new MemoryRateLimitStore(), clock, new RecordingLogger())
    const limiter = limiters('tier', { limit: 2, windowMs: 60_000 })

    expect(await limiter.take('key')).toBeNull()
    expect(await limiter.peek('key')).toBeNull()
    expect(await limiter.take('key')).toBeNull()
    expect(await limiter.spent('key')).toBe(2)
    clock.advance(500)
    // 59.5 s left, rounded up the way the in-process limiter rounds.
    expect(await limiter.take('key')).toBe(60)
    expect(await limiter.peek('key')).toBe(60)
    expect(await limiter.spent('key')).toBe(2)

    clock.advance(59_500)
    expect(await limiter.spent('key')).toBe(0)
    expect(await limiter.take('key')).toBeNull()
  })

  it('keeps two named budgets apart in one store', async () => {
    const clock = new ManualClock()
    const limiters = sharedRateLimiters(new MemoryRateLimitStore(), clock, new RecordingLogger())
    const one = limiters('one', { limit: 1, windowMs: 60_000 })
    const two = limiters('two', { limit: 1, windowMs: 60_000 })
    expect(await one.take('caller')).toBeNull()
    expect(await two.take('caller')).toBeNull()
    expect(await one.take('caller')).not.toBeNull()
  })

  it('refunds a reservation once and never into a later window', async () => {
    const clock = new ManualClock()
    const limiters = sharedRateLimiters(new MemoryRateLimitStore(), clock, new RecordingLogger())
    const limiter = limiters('tier', { limit: 1, windowMs: 1_000 })
    const release = await limiter.reserve('key')
    expect(release).not.toBeNull()
    expect(await limiter.reserve('key')).toBeNull()
    await release?.()
    await release?.()
    expect(await limiter.spent('key')).toBe(0)

    const held = await limiter.reserve('key')
    clock.advance(1_000)
    expect(await limiter.reserve('key')).not.toBeNull()
    await held?.()
    expect(await limiter.spent('key')).toBe(1)
  })

  it('lets calls through and warns once a minute while the store fails', async () => {
    const clock = new ManualClock()
    const logger = new RecordingLogger()
    const failing: RateLimitStore = {
      consume: async () => {
        throw new Error('down')
      },
      inspect: async () => {
        throw new Error('down')
      },
      refund: async () => {
        throw new Error('down')
      },
    }
    const limiter = sharedRateLimiters(
      failing,
      clock,
      logger,
    )('tier', {
      limit: 1,
      windowMs: 60_000,
    })
    expect(await limiter.take('key')).toBeNull()
    expect(await limiter.take('key')).toBeNull()
    expect(await limiter.peek('key')).toBeNull()
    expect(await limiter.spent('key')).toBe(0)
    const release = await limiter.reserve('key')
    expect(release).not.toBeNull()
    await release?.()
    expect(logger.lines.filter((line) => line.level === 'warn')).toHaveLength(1)
    clock.advance(60_000)
    await limiter.take('key')
    expect(logger.lines.filter((line) => line.level === 'warn')).toHaveLength(2)
  })
})
