/**
 * The budget one rate limit enforces: at most `limit` calls per key in a fixed window of
 * `windowMs` that opens on the key's first call.
 */
export interface RateLimitPolicy {
  limit: number
  windowMs: number
}

/** A key's window after a {@link RateLimitStore.consume}. */
export interface RateLimitWindow {
  /** Whether the call that produced this answer was counted, i.e. the window had room for it. */
  allowed: boolean
  /** Calls counted in the window, never above the policy's limit. */
  count: number
  /** When the window opened, in epoch milliseconds. Names the window for a refund. */
  windowStart: number
  /** When the window rolls, in epoch milliseconds. */
  resetAt: number
}

/**
 * A fixed-window counter that every instance of a deployment shares.
 *
 * The in-process {@link MemoryRateLimiter} counts what one process or one isolate sees, which is
 * the whole picture for a single self-hosted server and a fraction of it anywhere else: two Node
 * instances behind a load balancer each grant the full budget, and a Cloudflare isolate is one of
 * many. A runtime that runs as more than one instance implements this port in storage every
 * instance reaches (a Postgres table, a Durable Object per key), and the kernel's
 * {@link SharedRateLimiter} keeps the limiter semantics on top of it.
 *
 * Keys arrive already namespaced by limiter, so one store holds every tier. Times are epoch
 * milliseconds from the caller's clock: instances of one deployment agree on the time to well
 * under a second, and a window is a minute or a day.
 */
export interface RateLimitStore {
  /**
   * Counts one call against `key` if its window has room, atomically with respect to every other
   * caller. A window that has rolled (its `resetAt` is at or before `now`) is replaced by a fresh
   * one opening at `now`, and the call is counted in that.
   */
  consume(key: string, policy: RateLimitPolicy, now: number): Promise<RateLimitWindow>
  /** The live window of `key` at `now`, spending nothing; null when it has rolled or never opened. */
  inspect(key: string, now: number): Promise<{ count: number; resetAt: number } | null>
  /**
   * Gives back one call counted in the window that opened at `windowStart`. A key whose window has
   * since rolled is left alone, so a late refund never credits a newer window.
   */
  refund(key: string, windowStart: number): Promise<void>
}
