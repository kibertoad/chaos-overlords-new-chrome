export interface NodeConfig {
  host: string
  port: number
  /** `postgres://…` or `sqlite:<path>`; defaults to a SQLite file beside the process. */
  databaseUrl: string
  /**
   * Where bug reports go: `sqlite:<path>`, a *different* database from `databaseUrl`.
   *
   * Empty turns the intake off and the route answers 404. Postgres is deliberately not accepted
   * here: the bug report schema has one SQLite lineage, shared with D1, and adding a second dialect
   * for a single table nobody queries in a hot path would be two migration lineages to keep in step
   * for no gain.
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
  /** Trust `X-Forwarded-For` for the client address (set when behind a reverse proxy). */
  trustProxy: boolean
  /** How long an in-flight request (or an open event stream) may delay shutdown. */
  shutdownGraceMs: number
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
    bugReportDatabaseUrl: env.BUG_REPORT_DATABASE_URL ?? 'sqlite:./chaos-overlords-bug-reports.db',
    bugReportBlobDirectory: env.BUG_REPORT_BLOB_DIR ?? '',
    publicListing: flag(env.PUBLIC_LISTING, false),
    logLevel: level(env.LOG_LEVEL),
    sweepIntervalMs: integer(env.SWEEP_INTERVAL_MS, 15_000),
    rateLimitPerMinute: integer(env.RATE_LIMIT_PER_MINUTE, 30),
    memberRateLimitPerMinute: integer(env.MEMBER_RATE_LIMIT_PER_MINUTE, 240),
    uploadRateLimitPerMinute: integer(env.UPLOAD_RATE_LIMIT_PER_MINUTE, 10),
    bugReportRateLimitPerMinute: integer(env.BUG_REPORT_RATE_LIMIT_PER_MINUTE, 5),
    retentionDays: integer(env.RETENTION_DAYS, 30),
    trustProxy: flag(env.TRUST_PROXY, false),
    shutdownGraceMs: integer(env.SHUTDOWN_GRACE_MS, 5_000),
  }
}

function integer(raw: string | undefined, fallback: number): number {
  if (raw === undefined || raw === '') return fallback
  const value = Number(raw)
  if (!Number.isInteger(value) || value < 0)
    throw new Error(`Expected a non-negative integer, got "${raw}"`)
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
