import type { Clock } from '../ports/runtime'

/**
 * Fixed-window counter per key, in memory. It protects the unauthenticated doors (create, join)
 * from brute force on one process; a horizontally scaled deployment puts a shared limiter (a WAF
 * rule, Cloudflare's rate limiting) in front and treats this as the last line.
 */
export class RateLimiter {
  private readonly windows = new Map<string, { windowStart: number; count: number }>()
  private lastPrune = Number.NEGATIVE_INFINITY

  constructor(
    private readonly clock: Clock,
    private readonly options: { limit: number; windowMs: number },
  ) {}

  /** Returns the retry delay in seconds when over the limit, or null when the call is allowed. */
  take(key: string): number | null {
    const now = this.clock.now().getTime()
    const entry = this.windows.get(key)
    if (!entry || now - entry.windowStart >= this.options.windowMs) {
      this.windows.set(key, { windowStart: now, count: 1 })
      this.prune(now)
      return null
    }
    if (entry.count >= this.options.limit) {
      return Math.ceil((entry.windowStart + this.options.windowMs - now) / 1000)
    }
    entry.count += 1
    return null
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
}
