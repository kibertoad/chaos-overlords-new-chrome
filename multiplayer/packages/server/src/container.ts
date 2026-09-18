import type { BugReportService } from '@chaos-overlords/bug-reports'
import type { EventStreamOpener, Kernel, RateLimiter } from '@chaos-overlords/kernel'
import type { Context } from 'hono'
import type { AppEnv } from './http/types'

export interface ServerConfig {
  /** Serve `GET /api/v1/matches` (public lobby browsing). Off by default for self-hosted servers. */
  publicListing: boolean
  /** Interval between SSE keepalive comments. */
  sseHeartbeatMs: number
  /**
   * Origins a browser client may call from, or none. The game is not a browser and sends no
   * `Origin`, so nothing is opened by default; a web front end lists its origins here, which
   * allows the preflighted `Authorization` and `Last-Event-ID` headers the TypeScript client sends.
   */
  corsOrigins: readonly string[]
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
  /**
   * The bug report door, which is the only unauthenticated one that accepts megabytes.
   *
   * Its own tier rather than the anonymous one: a player filing a report and a player retrying a
   * join are nothing alike in cost, and sharing a budget would mean a handful of reports locked
   * somebody out of a lobby (or, the other way round, that a lobby's generous budget also bought
   * thirty multi-megabyte uploads a minute from a stranger).
   */
  bugReport: RateLimiter
}

export interface ServerContainer {
  kernel: Kernel
  /**
   * Bug report intake, backed by its own database. Absent on a deployment that has not configured
   * one, where the route answers 404 rather than pretending to take reports.
   */
  bugReports?: BugReportService
  eventStream: EventStreamOpener
  rateLimiters: RateLimiters
  config: ServerConfig
  /** How the runtime identifies a caller for rate limiting; defaults to proxy headers. */
  clientAddress?: (c: Context<AppEnv>) => string
}

export const DEFAULT_SERVER_CONFIG: ServerConfig = {
  publicListing: false,
  sseHeartbeatMs: 20_000,
  corsOrigins: [],
}

/** Per-minute budgets. Generous for play, far below what a retry loop would spend. */
export const DEFAULT_RATE_LIMITS = {
  anonymousPerMinute: 30,
  memberPerMinute: 240,
  uploadPerMinute: 10,
  /**
   * Five reports a minute per address. A player filing one files one; a player filing a second
   * because the first did not seem to send is the case this has to leave room for, and everything
   * beyond that is a client in a loop uploading whole match journals.
   */
  bugReportPerMinute: 5,
} as const
