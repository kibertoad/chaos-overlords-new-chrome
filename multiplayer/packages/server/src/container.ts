import type { EventStreamOpener, Kernel, RateLimiter } from '@chaos-overlords/kernel'
import type { Context } from 'hono'
import type { AppEnv } from './http/types'

export interface ServerConfig {
  /** Serve `GET /api/v1/matches` (public lobby browsing). Off by default for self-hosted servers. */
  publicListing: boolean
  /** Interval between SSE keepalive comments. */
  sseHeartbeatMs: number
}

/**
 * The three doors that need throttling, each with its own budget and key.
 *
 * `anonymous` guards create and join per client address: the only calls a stranger can make, and the
 * only ones a join code can be guessed through. The other two are keyed by player token, because an
 * authenticated member is also a cost: order documents are a quarter of a megabyte each and snapshot
 * uploads four times that, so a buggy client in a retry loop must not be able to write without
 * bound. Windows are per process, which is all a single self-hosted server needs; a public
 * deployment puts its platform's rate limiting in front as the real gate.
 */
export interface RateLimiters {
  anonymous: RateLimiter
  member: RateLimiter
  upload: RateLimiter
}

export interface ServerContainer {
  kernel: Kernel
  eventStream: EventStreamOpener
  rateLimiters: RateLimiters
  config: ServerConfig
  /** How the runtime identifies a caller for rate limiting; defaults to proxy headers. */
  clientAddress?: (c: Context<AppEnv>) => string
}

export const DEFAULT_SERVER_CONFIG: ServerConfig = {
  publicListing: false,
  sseHeartbeatMs: 20_000,
}

/** Per-minute budgets. Generous for play, far below what a retry loop would spend. */
export const DEFAULT_RATE_LIMITS = {
  anonymousPerMinute: 30,
  memberPerMinute: 240,
  uploadPerMinute: 10,
} as const
