import type { BugReportService } from '@chaos-overlords/bug-reports'
import type { EventStreamOpener, Kernel, RateLimiter } from '@chaos-overlords/kernel'
import type { Context } from 'hono'
import type { AppEnv } from './http/types'

export interface ServerConfig {
  /**
   * Serve `GET /api/v1/matches` (public lobby browsing). On by default.
   *
   * It lists the matches whose host chose `visibility: "public"` and nothing else, so a server
   * that serves it still keeps every code-only lobby out of sight. Off, the route answers 404 and
   * the game's Browse screen has nothing to show on any server — which is worth choosing
   * deliberately, for a server that exists for one group of friends, and is the wrong thing to
   * land on by having configured nothing.
   */
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
  /**
   * How many attached journals one address may file in a day.
   *
   * The per-minute tier bounds a retry loop; it does nothing about a steady drip. Five reports a
   * minute at six megabytes each is thirty megabytes a minute, so one address could spend the whole
   * 512 MiB global journal budget in about seventeen minutes and every real player's report would
   * lose its journal for the next twenty-four hours. Over this budget the report is still accepted
   * and the journal is dropped, which is what the global budget does too: the description is the
   * part worth keeping.
   */
  bugReportState: RateLimiter
  /**
   * Matches this process creates per window, from every caller together, under one key.
   *
   * The anonymous tier is per address, so it bounds one stranger and nothing about many. Each
   * create is a stored lobby that lives until lobby retention collects it, and a PBKDF2 hash when it
   * carries a password, so a flood from a few thousand addresses at the per-address rate would grow
   * the matches table and hold the CPU without ever meeting a limit. This is the ceiling that
   * distributed case meets. Joining and browsing are not charged here, so an existing lobby stays
   * reachable while it is spent.
   */
  matchCreation: RateLimiter
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
  publicListing: true,
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
  /**
   * Attached journals per address per day. At about six megabytes each this is roughly six per cent
   * of the default global budget, so no single address can crowd everybody else out of it.
   */
  bugReportStatePerDay: 5,
  /**
   * Matches created per minute across every caller. Two a second is far beyond what players open by
   * hand on any server this is likely to run, and caps a flood at under three thousand lobbies an
   * hour for lobby retention to collect.
   */
  matchCreationPerMinute: 120,
} as const
