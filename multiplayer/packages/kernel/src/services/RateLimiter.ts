import type { Clock } from '../ports/runtime'

/**
 * How many distinct keys one limiter holds by default before the oldest are dropped.
 *
 * `prune` only collects windows that have EXPIRED, at most once per window period, which is right
 * for a minute-long window and wrong for a day-long one: the bug-report budget's window is
 * twenty-four hours, so every address that ever posted stayed resident for a day or two on an
 * unauthenticated route. The Map iterates in window-start order — windows open on a clock that
 * never runs backwards, and a rolled window is removed before it reopens at the tail — so the head
 * is the oldest window and evicting from there is one shift. Dropping a live window forgives that
 * key's remaining budget, which is the safe direction to fail on a memory bound.
 */
const DEFAULT_MAX_KEYS = 50_000

export interface RateLimiterOptions {
  limit: number
  windowMs: number
  /** Residency bound; defaults to {@link DEFAULT_MAX_KEYS}. */
  maxKeys?: number
}

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
  private readonly maxKeys: number
  private lastPrune = Number.NEGATIVE_INFINITY
  private lastWall = Number.NaN
  private elapsed = 0

  constructor(
    private readonly clock: Clock,
    private readonly options: RateLimiterOptions,
  ) {
    this.maxKeys = options.maxKeys ?? DEFAULT_MAX_KEYS
  }

  /** Returns the retry delay in seconds when over the limit, or null when the call is allowed. */
  take(key: string): number | null {
    const now = this.tick()
    const entry = this.live(key, now)
    if (!entry) {
      // `live` removed any rolled window, so this appends at the tail and keeps the Map ordered.
      this.windows.set(key, { windowStart: now, count: 1 })
      this.prune(now)
      this.evictOldest()
      return null
    }
    if (entry.count >= this.options.limit) return this.retryAfter(entry, now)
    entry.count += 1
    return null
  }

  /**
   * Reserves one unit for work that may be rejected after validation. The returned release function
   * refunds exactly this reservation, once, while its window is still live. A rolled or evicted
   * window is never decremented on behalf of an older request.
   */
  reserve(key: string): (() => void) | null {
    const now = this.tick()
    let entry = this.live(key, now)
    if (!entry) {
      entry = { windowStart: now, count: 0 }
      this.windows.set(key, entry)
      this.prune(now)
      this.evictOldest()
    }
    if (entry.count >= this.options.limit) return null
    entry.count += 1
    const reserved = entry
    let released = false
    return () => {
      if (released) return
      released = true
      if (this.windows.get(key) === reserved) reserved.count -= 1
    }
  }

  /** What `take` would answer right now, spending nothing. */
  peek(key: string): number | null {
    const now = this.tick()
    const entry = this.live(key, now)
    if (!entry || entry.count < this.options.limit) return null
    return this.retryAfter(entry, now)
  }

  /** How much of `key`'s budget the current window has already spent; 0 once it has rolled. */
  spent(key: string): number {
    return this.live(key, this.tick())?.count ?? 0
  }

  /**
   * Milliseconds on a clock that only moves forward, accumulated from the wall clock's forward steps.
   *
   * Production passes the wall clock, which NTP can step backwards. Measured directly, a step back
   * would give new windows an earlier start than older ones already at the head, breaking the
   * eviction order, and make elapsed time negative, keeping spent windows alive past `windowMs`.
   * A backward step here just pauses the limiter's time until the wall clock moves forward again.
   */
  private tick(): number {
    const wall = this.clock.now().getTime()
    if (wall > this.lastWall) this.elapsed += wall - this.lastWall
    this.lastWall = wall
    return this.elapsed
  }

  /**
   * The window `key` is spending from, or undefined once it has rolled or was never opened.
   *
   * A rolled window is removed here rather than overwritten later: Map#set keeps an existing key's
   * insertion position, so overwriting would leave a fresh, possibly spent window at the head, first
   * in line for eviction.
   */
  private live(key: string, now: number): WindowEntry | undefined {
    const entry = this.windows.get(key)
    if (!entry || !this.expired(entry, now)) return entry
    this.windows.delete(key)
    return undefined
  }

  private expired(entry: WindowEntry, now: number): boolean {
    return now - entry.windowStart >= this.options.windowMs
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
      if (this.expired(entry, now)) this.windows.delete(key)
    }
  }

  /** Hard bound on residency, for windows too long for `prune` to be the only collector. */
  private evictOldest(): void {
    while (this.windows.size > this.maxKeys) {
      const oldest = this.windows.keys().next()
      if (oldest.done) return
      this.windows.delete(oldest.value)
    }
  }
}
