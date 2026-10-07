import type { RateLimitPolicy, RateLimitStore, RateLimitWindow } from '../ports/rateLimits'
import type { Clock, Logger } from '../ports/runtime'

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

export interface RateLimiterOptions extends RateLimitPolicy {
  /** Residency bound of the in-process limiter; defaults to {@link DEFAULT_MAX_KEYS}. */
  maxKeys?: number
}

/**
 * One budget of the server, keyed by caller. Every method is asynchronous because a deployment of
 * more than one instance keeps the counts in shared storage; see {@link RateLimitStore}.
 */
export interface RateLimiter {
  /** Returns the retry delay in seconds when over the limit, or null when the call is allowed. */
  take(key: string): Promise<number | null>
  /**
   * Reserves one unit for work that may be rejected after validation. The returned release function
   * refunds exactly this reservation, once, while its window is still live. A rolled or evicted
   * window is never decremented on behalf of an older request. Null when the budget is spent.
   */
  reserve(key: string): Promise<(() => Promise<void>) | null>
  /** What `take` would answer right now, spending nothing. */
  peek(key: string): Promise<number | null>
  /** How much of `key`'s budget the current window has already spent; 0 once it has rolled. */
  spent(key: string): Promise<number>
}

/**
 * Builds the limiter for one named budget. The name keeps budgets apart when they share a store,
 * so two limiters given the same caller key never spend each other's allowance.
 */
export type RateLimiterFactory = (name: string, options: RateLimiterOptions) => RateLimiter

/** Limiters that count in this process: every call a single process takes, and nothing more. */
export function memoryRateLimiters(clock: Clock): RateLimiterFactory {
  return (_name, options) => new MemoryRateLimiter(clock, options)
}

/**
 * Limiters that count in a {@link RateLimitStore} every instance of the deployment shares.
 *
 * A store that fails (the database is unreachable, a Durable Object is overloaded) lets the call
 * through and logs, at most once a minute. The limiters are abuse controls in front of a game, and
 * refusing every player because the counter is down would be the outage the abuse was going to
 * cause; when the failure is shared with the match storage, the request fails there on its own.
 */
export function sharedRateLimiters(
  store: RateLimitStore,
  clock: Clock,
  logger: Logger,
): RateLimiterFactory {
  const report = throttledWarning(clock, logger)
  return (name, options) =>
    new SharedRateLimiter(store, clock, {
      name,
      policy: { limit: options.limit, windowMs: options.windowMs },
      report,
    })
}

/**
 * What a fixed window answers a call at `now`: the window to keep, and whether the call was
 * counted. A window that has rolled is replaced by one opening at `now`. Every store applies this,
 * so the budgets mean the same thing in a Postgres row, a Durable Object and a test.
 */
export function consumeWindow(
  current: { windowStart: number; resetAt: number; count: number } | undefined,
  policy: RateLimitPolicy,
  now: number,
): RateLimitWindow {
  if (current === undefined || current.resetAt <= now) {
    return { allowed: true, count: 1, windowStart: now, resetAt: now + policy.windowMs }
  }
  const allowed = current.count < policy.limit
  return {
    allowed,
    count: allowed ? current.count + 1 : current.count,
    windowStart: current.windowStart,
    resetAt: current.resetAt,
  }
}

/** Whole seconds until a window that rolls at `resetAt` has rolled; at least one. */
function secondsUntil(resetAt: number, now: number): number {
  return Math.max(1, Math.ceil((resetAt - now) / 1000))
}

/**
 * A limiter whose windows live in a {@link RateLimitStore}.
 *
 * It keeps the in-process limiter's contract on top of the store's three operations, so a budget
 * answers the same on every runtime. It measures on the wall clock: the windows are compared across
 * instances, and the wall clock is the only one they have in common.
 */
export class SharedRateLimiter implements RateLimiter {
  private readonly name: string
  private readonly policy: RateLimitPolicy
  private readonly report: (operation: string, error: unknown) => void

  constructor(
    private readonly store: RateLimitStore,
    private readonly clock: Clock,
    options: {
      /** Keeps this budget's keys apart from every other budget in the store. */
      name: string
      policy: RateLimitPolicy
      /** Told when the store fails; the call goes through regardless. */
      report: (operation: string, error: unknown) => void
    },
  ) {
    this.name = options.name
    this.policy = options.policy
    this.report = options.report
  }

  async take(key: string): Promise<number | null> {
    const now = this.clock.now().getTime()
    const window = await this.guard('consume', () =>
      this.store.consume(this.key(key), this.policy, now),
    )
    if (window === undefined || window.allowed) return null
    return secondsUntil(window.resetAt, now)
  }

  async reserve(key: string): Promise<(() => Promise<void>) | null> {
    const stored = this.key(key)
    const now = this.clock.now().getTime()
    const window = await this.guard('consume', () => this.store.consume(stored, this.policy, now))
    // The store failed and the call goes through; there is nothing to give back.
    if (window === undefined) return async () => {}
    if (!window.allowed) return null
    let released = false
    return async () => {
      if (released) return
      released = true
      await this.guard('refund', () => this.store.refund(stored, window.windowStart))
    }
  }

  async peek(key: string): Promise<number | null> {
    const now = this.clock.now().getTime()
    const live = await this.guard('inspect', () => this.store.inspect(this.key(key), now))
    if (!live || live.count < this.policy.limit) return null
    return secondsUntil(live.resetAt, now)
  }

  async spent(key: string): Promise<number> {
    const now = this.clock.now().getTime()
    const live = await this.guard('inspect', () => this.store.inspect(this.key(key), now))
    return Math.min(live?.count ?? 0, this.policy.limit)
  }

  private key(key: string): string {
    return `${this.name}|${key}`
  }

  private async guard<T>(operation: string, run: () => Promise<T>): Promise<T | undefined> {
    try {
      return await run()
    } catch (error: unknown) {
      this.report(operation, error)
      return undefined
    }
  }
}

/** At most one warning a minute about a failing store, however many calls meet the failure. */
function throttledWarning(
  clock: Clock,
  logger: Logger,
): (operation: string, error: unknown) => void {
  let lastWarned = Number.NEGATIVE_INFINITY
  return (operation, error) => {
    const now = clock.now().getTime()
    if (now - lastWarned < 60_000) return
    lastWarned = now
    logger.warn('rate limit store failed; letting the call through', {
      operation,
      error: String(error),
    })
  }
}

/**
 * A {@link RateLimitStore} in this process: the reference the shared stores are tested against,
 * and the store for a test that needs one without a database. It never forgets a key, so it is no
 * substitute for {@link MemoryRateLimiter} in a server.
 */
export class MemoryRateLimitStore implements RateLimitStore {
  private readonly windows = new Map<
    string,
    { windowStart: number; resetAt: number; count: number }
  >()

  async consume(key: string, policy: RateLimitPolicy, now: number): Promise<RateLimitWindow> {
    const window = consumeWindow(this.windows.get(key), policy, now)
    this.windows.set(key, {
      windowStart: window.windowStart,
      resetAt: window.resetAt,
      count: window.count,
    })
    return window
  }

  async inspect(key: string, now: number): Promise<{ count: number; resetAt: number } | null> {
    const window = this.windows.get(key)
    if (!window || window.resetAt <= now) return null
    return { count: window.count, resetAt: window.resetAt }
  }

  async refund(key: string, windowStart: number): Promise<void> {
    const window = this.windows.get(key)
    if (window && window.windowStart === windowStart && window.count > 0) window.count -= 1
  }
}

interface WindowEntry {
  windowStart: number
  count: number
}

/**
 * Fixed-window counter per key, in memory. It counts what one process sees, which is every call a
 * single self-hosted server takes; a deployment of more than one instance uses
 * {@link sharedRateLimiters} instead.
 */
export class MemoryRateLimiter implements RateLimiter {
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

  async take(key: string): Promise<number | null> {
    const now = this.tick()
    const entry = this.open(key, now)
    if (entry.count >= this.options.limit) return this.retryAfter(entry, now)
    entry.count += 1
    return null
  }

  async reserve(key: string): Promise<(() => Promise<void>) | null> {
    const reserved = this.open(key, this.tick())
    if (reserved.count >= this.options.limit) return null
    reserved.count += 1
    let released = false
    return async () => {
      if (released) return
      released = true
      if (this.windows.get(key) === reserved) reserved.count -= 1
    }
  }

  async peek(key: string): Promise<number | null> {
    const now = this.tick()
    const entry = this.live(key, now)
    if (!entry || entry.count < this.options.limit) return null
    return this.retryAfter(entry, now)
  }

  async spent(key: string): Promise<number> {
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

  /** The window `key` is spending from, opening an empty one at the tail when none is live. */
  private open(key: string, now: number): WindowEntry {
    const live = this.live(key, now)
    if (live) return live
    // `live` removed any rolled window, so this appends at the tail and keeps the Map ordered.
    const entry = { windowStart: now, count: 0 }
    this.windows.set(key, entry)
    this.prune(now)
    this.evictOldest()
    return entry
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
