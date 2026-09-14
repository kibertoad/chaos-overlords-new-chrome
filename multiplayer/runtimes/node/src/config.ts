import { DEFAULT_EVENT_HUB_LIMITS } from '@chaos-overlords/server'

export interface NodeConfig {
  host: string
  port: number
  /** `postgres://…` or `sqlite:<path>`; defaults to a SQLite file beside the process. */
  databaseUrl: string
  /**
   * Where bug reports go: `sqlite:<path>`, a *different* database from `databaseUrl`.
   *
   * Empty is the DEFAULT and turns the intake off, so the route answers 404. The game never posts
   * here — it is wired to the central service — so a lobby server for a few friends would otherwise
   * be exposing an unauthenticated route that takes 8 MiB bodies for nobody's benefit. An operator
   * running the public intake sets this deliberately.
   *
   * Postgres is not accepted: the bug report schema has one SQLite lineage, shared with D1, and a
   * second dialect for a single table nobody queries in a hot path would be two migration lineages
   * to keep in step for no gain.
   */
  bugReportDatabaseUrl: string
  /**
   * A directory to keep compressed match journals in, instead of in the bug report database.
   *
   * Empty keeps small archives in the row and turns large ones away; a public deployment on
   * Cloudflare uses an R2 bucket instead of this. See `BugReportService`.
   */
  bugReportBlobDirectory: string
  publicListing: boolean
  logLevel: 'debug' | 'info' | 'warn' | 'error'
  /** How often the safety-net sweep looks for expired turns and interrupted seals. */
  sweepIntervalMs: number
  /** Unauthenticated create/join attempts allowed per client address per minute. */
  rateLimitPerMinute: number
  /** Authenticated calls allowed per player per minute, and snapshot uploads within that. */
  memberRateLimitPerMinute: number
  uploadRateLimitPerMinute: number
  /** Bug reports accepted per client address per minute. */
  bugReportRateLimitPerMinute: number
  /**
   * Days after which a finished, abandoned or never-started match is deleted with everything it
   * owns. 0 keeps every match forever, which a long-lived server will feel in its database size.
   */
  retentionDays: number
  /**
   * Days after which a RUNNING match that nobody is in any more is deleted. 0 keeps them forever.
   *
   * A match stays running when the last player leaves so that anyone can come back and rejoin, and
   * most public matches end that way rather than by being played out. The window is long because
   * collecting one says "nobody is coming back".
   */
  abandonedRetentionDays: number
  /** Days a bug report and its archive are kept. 0 keeps them forever. */
  bugReportRetentionDays: number
  /**
   * Attached journal megabytes the intake accepts per rolling day, across every reporter. 0 lifts
   * the ceiling. A report over budget is still filed; only its journal is dropped.
   */
  bugReportDailyStateMb: number
  /**
   * How many trusted proxies sit in front of this server; 0 means none.
   *
   * 0 reads the socket address, which is the only value a client cannot choose. Any higher number
   * reads `X-Forwarded-For` from the RIGHT: with 1 the last entry is the address the one trusted
   * proxy wrote, with 2 the second from last, and so on. Everything further left is whatever the
   * client sent, which is why the count has to be stated rather than assumed — most proxies append
   * to this header rather than replacing it.
   *
   * Set it only when the proxy chain is actually that long. Too high a number reads an address the
   * client wrote and the rate limits stop binding.
   */
  trustedProxyHops: number
  /** How long an in-flight request (or an open event stream) may delay shutdown. */
  shutdownGraceMs: number
  /**
   * Event streams this process will hold at once, across every match; further opens answer 429.
   *
   * One stream per player per match is what a healthy client uses, so the default covers a server
   * hosting a few hundred players at once. Raise it for a bigger one, but raise the file descriptor
   * limit with it: the point of the ceiling is that a server refuses the stream it cannot serve
   * rather than degrading for everybody already on it. The per-player and per-match caps around it
   * are not configurable; they follow from the six seats and from what a reconnect needs.
   */
  maxEventStreams: number
}

/**
 * Reads the environment once. Every knob has a self-hosting default: an operator can run the
 * server with no configuration at all and get a SQLite file, a private lobby list and port 8787.
 */
export function loadConfig(env: NodeJS.ProcessEnv = process.env): NodeConfig {
  return {
    host: env.HOST ?? '0.0.0.0',
    port: integer(env.PORT, 8787),
    databaseUrl: env.DATABASE_URL ?? 'sqlite:./chaos-overlords.db',
    bugReportDatabaseUrl: env.BUG_REPORT_DATABASE_URL ?? '',
    bugReportBlobDirectory: env.BUG_REPORT_BLOB_DIR ?? '',
    publicListing: flag(env.PUBLIC_LISTING, false),
    logLevel: level(env.LOG_LEVEL),
    sweepIntervalMs: integer(env.SWEEP_INTERVAL_MS, 15_000),
    rateLimitPerMinute: integer(env.RATE_LIMIT_PER_MINUTE, 30),
    memberRateLimitPerMinute: integer(env.MEMBER_RATE_LIMIT_PER_MINUTE, 240),
    uploadRateLimitPerMinute: integer(env.UPLOAD_RATE_LIMIT_PER_MINUTE, 10),
    bugReportRateLimitPerMinute: integer(env.BUG_REPORT_RATE_LIMIT_PER_MINUTE, 5),
    retentionDays: integer(env.RETENTION_DAYS, 30),
    abandonedRetentionDays: integer(env.ABANDONED_RETENTION_DAYS, 90),
    bugReportRetentionDays: integer(env.BUG_REPORT_RETENTION_DAYS, 90),
    bugReportDailyStateMb: integer(env.BUG_REPORT_DAILY_STATE_MB, 512),
    trustedProxyHops: proxyHops(env.TRUST_PROXY),
    shutdownGraceMs: integer(env.SHUTDOWN_GRACE_MS, 5_000),
    maxEventStreams: integer(env.MAX_EVENT_STREAMS, DEFAULT_EVENT_HUB_LIMITS.perProcess),
  }
}

function integer(raw: string | undefined, fallback: number): number {
  if (raw === undefined || raw === '') return fallback
  const value = Number(raw)
  if (!Number.isInteger(value) || value < 0)
    throw new Error(`Expected a non-negative integer, got "${raw}"`)
  return value
}

/**
 * `TRUST_PROXY` as a count of trusted hops.
 *
 * `true` and `1` both mean one proxy, which is what the boolean it replaces used to mean; a larger
 * integer names a longer chain (a CDN in front of a load balancer). `false`, `0` and an empty value
 * all mean none.
 */
function proxyHops(raw: string | undefined): number {
  if (raw === undefined || raw === '' || raw === 'false') return 0
  if (raw === 'true') return 1
  const value = Number(raw)
  if (!Number.isInteger(value) || value < 0) {
    throw new Error(`Expected TRUST_PROXY to be true, false or a hop count, got "${raw}"`)
  }
  return value
}

function flag(raw: string | undefined, fallback: boolean): boolean {
  if (raw === undefined || raw === '') return fallback
  return raw === 'true' || raw === '1'
}

function level(raw: string | undefined): NodeConfig['logLevel'] {
  if (raw === undefined || raw === '') return 'info'
  if (raw === 'debug' || raw === 'info' || raw === 'warn' || raw === 'error') return raw
  throw new Error(`Unknown LOG_LEVEL "${raw}"`)
}
