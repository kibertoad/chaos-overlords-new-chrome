import type { Clock } from '../ports/runtime'

/**
 * How many distinct keys one limiter holds before the oldest are dropped.
 *
 * `prune` only collects windows that have EXPIRED, at most once per window period, which is right
 * for a minute-long window and wrong for a day-long one: the bug-report budget's window is
 * twenty-four hours, so every address that ever posted stayed resident for a day or two on an
 * unauthenticated route. A Map iterates in window-start order because a window that rolls moves to
 * the tail, so the head is the oldest window and evicting from there is one shift. Dropping a live
 * window forgives that key's remaining budget, which is the safe direction to fail on a memory
 * bound.
 */
const MAX_KEYS = 50_000

interface WindowEntry {
  windowStart: number
  count: number
}

/**
 * Fixed-window counter per key, in memory. It protects the unauthenticated doors (create, join)
 * from brute force on one process; a horizontally scaled deployment puts a shared limiter (a WAF
 * rule, Cloudflare's rate limiting) in front and treats this as the last line.
 */
export class RateLimiter {
  private readonly windows = new Map<string, WindowEntry>()
  private lastPrune = Number.NEGATIVE_INFINITY

  constructor(
    private readonly clock: Clock,
    private readonly options: { limit: number; windowMs: number },
  ) {}

  /** Returns the retry delay in seconds when over the limit, or null when the call is allowed. */
  take(key: string): number | null {
    const now = this.clock.now().getTime()
    const entry = this.live(key, now)
    if (!entry) {
      // Map#set preserves a key's old insertion position. A rolled window is new, so it must go
      // back at the tail; otherwise the memory bound drops a fresh, possibly spent budget first.
      this.windows.delete(key)
      this.windows.set(key, { windowStart: now, count: 1 })
      this.prune(now)
      this.evictOldest()
      return null
    }
    if (entry.count >= this.options.limit) return this.retryAfter(entry, now)
    entry.count += 1
    return null
  }

  /** What `take` would answer right now, spending nothing. */
  peek(key: string): number | null {
    const now = this.clock.now().getTime()
    const entry = this.live(key, now)
    if (!entry || entry.count < this.options.limit) return null
    return this.retryAfter(entry, now)
  }

  /** How much of `key`'s budget the current window has already spent; 0 once it has rolled. */
  spent(key: string): number {
    return this.live(key, this.clock.now().getTime())?.count ?? 0
  }

  /** The window `key` is spending from, or undefined once it has rolled or was never opened. */
  private live(key: string, now: number): WindowEntry | undefined {
    const entry = this.windows.get(key)
    if (!entry || now - entry.windowStart >= this.options.windowMs) return undefined
    return entry
  }

  /** Whole seconds until `entry`'s window rolls, rounded up. */
  private retryAfter(entry: WindowEntry, now: number): number {
    return Math.ceil((entry.windowStart + this.options.windowMs - now) / 1000)
  }

  /**
   * Drop windows that have expired, at most once per window period.
   *
   * The sweep is O(size), so running it on every new key would be quadratic exactly when the map is
   * busiest — a burst of distinct keys is both what fills the map and what triggers the sweep. The
   * time guard makes the amortised cost one pass per window regardless of traffic, and expired
   * entries can never outlive two windows.
   */
  private prune(now: number): void {
    if (now - this.lastPrune < this.options.windowMs) return
    this.lastPrune = now
    for (const [key, entry] of this.windows) {
      if (now - entry.windowStart >= this.options.windowMs) this.windows.delete(key)
    }
  }

  /** Hard bound on residency, for windows too long for `prune` to be the only collector. */
  private evictOldest(): void {
    while (this.windows.size > MAX_KEYS) {
      const oldest = this.windows.keys().next()
      if (oldest.done) return
      this.windows.delete(oldest.value)
    }
  }
}
