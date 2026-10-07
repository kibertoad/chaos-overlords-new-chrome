import {
  consumeWindow,
  type RateLimitPolicy,
  type RateLimitStore,
  type RateLimitWindow,
} from '@chaos-overlords/kernel'
import type { DurableObjectNamespace, DurableObjectState } from '@cloudflare/workers-types'

interface StoredWindow {
  windowStart: number
  resetAt: number
  count: number
}

const WINDOW_KEY = 'window'

/**
 * The longest window kept in memory alone.
 *
 * Cloudflare evicts an idle object after 70 to 140 seconds without a request, so a minute-long
 * window has rolled before its object can be evicted for idleness, and keeping it in memory loses
 * nothing but the rare window a deployment or a runtime restart interrupts, which forgives that
 * caller the rest of one minute. A longer window (the day-long journal budget) would be forgiven
 * by simply waiting two minutes, so it is written to the object's storage and deleted by an alarm
 * when it rolls.
 */
const MEMORY_ONLY_WINDOW_MS = 60_000

const PATHS = { consume: '/consume', inspect: '/inspect', refund: '/refund' } as const

/**
 * One rate limit window, for one budget and one caller, wherever in the world the caller's
 * requests land.
 *
 * Each key gets an object of its own (`idFromName(key)`), so every isolate that counts a caller
 * reaches the same counter and a budget holds across the whole deployment; the object is created
 * near the caller's first request, so the round trip stays short. Cloudflare's rate limiting
 * binding was not usable for this: it counts per Cloudflare location, only over ten or sixty
 * seconds, and answers only yes or no, with no time to wait, no inspection and no refund, which the
 * day-long journal budget, `Retry-After`, the match-creation peek and the journal reservation need.
 *
 * An object processes one event at a time and its storage calls hold its input gate, so the read,
 * decision and write of a `consume` cannot interleave with another caller's.
 */
export class RateLimitCounter {
  private current: StoredWindow | undefined
  /** Whether `current` is also in storage, so a refund knows to write it back. */
  private persisted = false
  /**
   * The one read of storage, shared by every call that arrives before it settles, so a call that
   * reads late never overwrites a window another call has already counted in.
   */
  private loading: Promise<void> | undefined

  constructor(
    private readonly state: DurableObjectState,
    _env: unknown,
  ) {}

  async fetch(request: Request): Promise<Response> {
    const path = new URL(request.url).pathname
    const body = (await request.json()) as {
      policy?: RateLimitPolicy
      now?: number
      windowStart?: number
    }
    await this.load()
    if (path === PATHS.consume && body.policy && typeof body.now === 'number') {
      const window = consumeWindow(this.current, body.policy, body.now)
      // A refused call leaves the window as it was, so there is nothing to write.
      if (window.allowed) {
        await this.save(
          { windowStart: window.windowStart, resetAt: window.resetAt, count: window.count },
          body.policy.windowMs > MEMORY_ONLY_WINDOW_MS,
        )
      }
      return Response.json(window)
    }
    if (path === PATHS.inspect && typeof body.now === 'number') {
      const live = this.current && this.current.resetAt > body.now ? this.current : undefined
      return Response.json(live ? { count: live.count, resetAt: live.resetAt } : null)
    }
    if (path === PATHS.refund && typeof body.windowStart === 'number') {
      const window = this.current
      if (window && window.windowStart === body.windowStart && window.count > 0) {
        await this.save({ ...window, count: window.count - 1 }, this.persisted)
      }
      return Response.json(null)
    }
    return new Response('not found', { status: 404 })
  }

  /** Forgets a persisted window once it has rolled; a fresh one is opened by the next call. */
  async alarm(): Promise<void> {
    await this.load()
    if (!this.persisted) return
    if (this.current && this.current.resetAt > Date.now()) {
      await this.state.storage.setAlarm(this.current.resetAt)
      return
    }
    this.current = undefined
    this.persisted = false
    await this.state.storage.deleteAll()
  }

  private load(): Promise<void> {
    this.loading ??= this.state.storage.get<StoredWindow>(WINDOW_KEY).then(
      (stored) => {
        this.current = stored ?? undefined
        this.persisted = this.current !== undefined
      },
      (error: unknown) => {
        // A failed read is retried by the next call rather than remembered.
        this.loading = undefined
        throw error
      },
    )
    return this.loading
  }

  private async save(window: StoredWindow, persist: boolean): Promise<void> {
    const opened = this.current?.windowStart !== window.windowStart
    this.current = window
    if (!persist) {
      // A minute window that replaced a persisted day window: drop the stored one and its alarm.
      if (this.persisted) await this.state.storage.deleteAll()
      this.persisted = false
      return
    }
    await this.state.storage.put(WINDOW_KEY, window)
    if (opened || !this.persisted) await this.state.storage.setAlarm(window.resetAt)
    this.persisted = true
  }
}

/**
 * How long a Worker waits on a counter before letting the call through. A counter that does not
 * answer must not hold every request behind it; see `sharedRateLimiters` for why a failure lets
 * the call through.
 */
const COUNTER_TIMEOUT_MS = 2_000

/** The {@link RateLimitStore} the Worker counts in: one {@link RateLimitCounter} per key. */
export function durableObjectRateLimitStore(namespace: DurableObjectNamespace): RateLimitStore {
  const call = async <T>(key: string, path: string, body: unknown): Promise<T> => {
    const stub = namespace.get(namespace.idFromName(key))
    const response = await stub.fetch(`https://counter${path}`, {
      method: 'POST',
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(COUNTER_TIMEOUT_MS),
    })
    if (!response.ok) throw new Error(`rate limit counter answered ${response.status}`)
    return (await response.json()) as T
  }
  return {
    consume: (key, policy, now) => call<RateLimitWindow>(key, PATHS.consume, { policy, now }),
    inspect: (key, now) =>
      call<{ count: number; resetAt: number } | null>(key, PATHS.inspect, { now }),
    refund: async (key, windowStart) => {
      await call<null>(key, PATHS.refund, { windowStart })
    },
  }
}
