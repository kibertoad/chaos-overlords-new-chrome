import { DEFAULT_RETENTION_DAYS } from '@chaos-overlords/kernel'
import {
  configFlag,
  configInteger,
  configList,
  DEFAULT_EVENT_HUB_LIMITS,
  DEFAULT_RATE_LIMITS,
} from '@chaos-overlords/server'

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
  /**
   * Serve `GET /api/v1/matches`, the list the game's Browse screen reads. On by default.
   *
   * Only matches whose host chose to be publicly discoverable are ever in it; a join-code lobby is
   * never listed either way. Set `PUBLIC_LISTING=false` on a server that should not answer the
   * route at all.
   */
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
  /** Matches created per minute across every caller together; see `RateLimiters.matchCreation`. */
  matchCreationRateLimitPerMinute: number
  /**
   * Days after which a finished or abandoned match is deleted with everything it owns. 0 keeps
   * them forever, which a long-lived server will feel in its database size.
   */
  retentionDays: number
  /**
   * Days after which a lobby that was never started is deleted. 0 keeps them forever. Unset is the
   * kernel's default, except that `RETENTION_DAYS=0` keeps lobbies too, as it did before lobbies
   * had a window of their own.
   */
  lobbyRetentionDays: number | undefined
  /**
   * Days after which a RUNNING match that nobody is in any more is deleted. 0 keeps them forever.
   *
   * A match stays running when the last player leaves so that anyone can come back and rejoin, and
   * most public matches end that way rather than by being played out. The window is long because
   * collecting one says "nobody is coming back".
   */
  abandonedRetentionDays: number
  /**
   * Days after which a running match is deleted even though players are still seated in it, because
   * nothing has happened in it for that long. Unset follows `abandonedRetentionDays` (three times
   * as long); 0 keeps them forever.
   */
  silentRetentionDays: number | undefined
  /** Matches each retention window deletes per pass. Unset picks a size for the database dialect. */
  retentionBatchSize: number | undefined
  /** How often the cleanup job runs: match retention, then bug report retention. */
  retentionIntervalMs: number
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
  /**
   * Connections the HTTP server holds at once; sockets over it are closed as they arrive.
   *
   * Every open event stream is a connection, so this has to leave room above `maxEventStreams` for
   * ordinary requests, and it is refused at startup when it does not. Without it the only ceiling on
   * idle or trickling sockets is the process file descriptor limit, and reaching that fails the
   * database and the log files as well as the API.
   */
  maxConnections: number
  /**
   * How long a client may take to send a request's headers. Node's own default is a minute, which
   * lets a slowloris client hold a socket for sixty seconds per header it drips.
   */
  headersTimeoutMs: number
  /**
   * How long a client may take to send a whole request, body included. Sized for the largest body
   * the API takes (a bug report's base64 journal) on a slow uplink. An event stream is not affected:
   * its request is complete as soon as its headers are, however long the response runs.
   */
  requestTimeoutMs: number
  /** `CORS_ORIGINS`: browser origins allowed to call the API, comma separated. None by default. */
  corsOrigins: string[]
}

/**
 * Reads the environment once. Every knob has a self-hosting default: an operator can run the
 * server with no configuration at all and get a SQLite file, a browsable list of the matches whose
 * hosts chose to be discoverable, and port 8787.
 */
export function loadConfig(env: NodeJS.ProcessEnv = process.env): NodeConfig {
  const maxEventStreams = configInteger(
    env.MAX_EVENT_STREAMS,
    DEFAULT_EVENT_HUB_LIMITS.perProcess,
    1,
  )
  return {
    host: env.HOST ?? '0.0.0.0',
    port: configInteger(env.PORT, 8787),
    databaseUrl: env.DATABASE_URL ?? 'sqlite:./chaos-overlords.db',
    bugReportDatabaseUrl: env.BUG_REPORT_DATABASE_URL ?? '',
    bugReportBlobDirectory: env.BUG_REPORT_BLOB_DIR ?? '',
    publicListing: configFlag(env.PUBLIC_LISTING, true),
    logLevel: level(env.LOG_LEVEL),
    sweepIntervalMs: configInteger(env.SWEEP_INTERVAL_MS, 15_000, MIN_SWEEP_INTERVAL_MS),
    rateLimitPerMinute: configInteger(
      env.RATE_LIMIT_PER_MINUTE,
      DEFAULT_RATE_LIMITS.anonymousPerMinute,
      1,
    ),
    memberRateLimitPerMinute: configInteger(
      env.MEMBER_RATE_LIMIT_PER_MINUTE,
      DEFAULT_RATE_LIMITS.memberPerMinute,
      1,
    ),
    uploadRateLimitPerMinute: configInteger(
      env.UPLOAD_RATE_LIMIT_PER_MINUTE,
      DEFAULT_RATE_LIMITS.uploadPerMinute,
      1,
    ),
    bugReportRateLimitPerMinute: configInteger(
      env.BUG_REPORT_RATE_LIMIT_PER_MINUTE,
      DEFAULT_RATE_LIMITS.bugReportPerMinute,
      1,
    ),
    matchCreationRateLimitPerMinute: configInteger(
      env.MATCH_CREATION_RATE_LIMIT_PER_MINUTE,
      DEFAULT_RATE_LIMITS.matchCreationPerMinute,
      1,
    ),
    retentionDays: configInteger(env.RETENTION_DAYS, DEFAULT_RETENTION_DAYS.finished),
    lobbyRetentionDays: optionalInteger(env.LOBBY_RETENTION_DAYS),
    abandonedRetentionDays: configInteger(
      env.ABANDONED_RETENTION_DAYS,
      DEFAULT_RETENTION_DAYS.abandonedLive,
    ),
    silentRetentionDays: optionalInteger(env.SILENT_RETENTION_DAYS),
    retentionBatchSize: optionalInteger(env.RETENTION_BATCH_SIZE, 1),
    retentionIntervalMs: configInteger(env.RETENTION_INTERVAL_MS, 60_000, MIN_SWEEP_INTERVAL_MS),
    bugReportRetentionDays: configInteger(env.BUG_REPORT_RETENTION_DAYS, 90),
    bugReportDailyStateMb: configInteger(env.BUG_REPORT_DAILY_STATE_MB, 512),
    trustedProxyHops: proxyHops(env.TRUST_PROXY),
    shutdownGraceMs: configInteger(env.SHUTDOWN_GRACE_MS, 5_000),
    maxEventStreams,
    maxConnections: connectionCap(env.MAX_CONNECTIONS, maxEventStreams),
    ...requestTimeouts(env),
    corsOrigins: configList(env.CORS_ORIGINS),
  }
}

/**
 * Room for ordinary requests above the stream ceiling: a busy server's API calls ride keep-alive
 * sockets of their own, one or two per connected player, beside that player's stream.
 */
const CONNECTION_HEADROOM_PER_STREAM = 2
const MIN_CONNECTIONS = 1_024

/** `MAX_CONNECTIONS`, which must leave room for requests above every allowed event stream. */
function connectionCap(raw: string | undefined, maxEventStreams: number): number {
  const fallback = Math.max(MIN_CONNECTIONS, maxEventStreams * CONNECTION_HEADROOM_PER_STREAM)
  const value = configInteger(raw, fallback, 1)
  if (value <= maxEventStreams) {
    throw new Error(
      `MAX_CONNECTIONS (${value}) must be above MAX_EVENT_STREAMS (${maxEventStreams}), or the streams alone can take every connection`,
    )
  }
  return value
}

/**
 * The two request deadlines. Node's `createServer` refuses a header deadline past the request one,
 * and would do it with an error naming its own option rather than the variable; this says which.
 */
function requestTimeouts(
  env: NodeJS.ProcessEnv,
): Pick<NodeConfig, 'headersTimeoutMs' | 'requestTimeoutMs'> {
  const headersTimeoutMs = configInteger(env.HTTP_HEADERS_TIMEOUT_MS, 15_000, 1_000)
  const requestTimeoutMs = configInteger(env.HTTP_REQUEST_TIMEOUT_MS, 120_000, 1_000)
  if (headersTimeoutMs > requestTimeoutMs) {
    throw new Error(
      `HTTP_HEADERS_TIMEOUT_MS (${headersTimeoutMs}) must not exceed HTTP_REQUEST_TIMEOUT_MS (${requestTimeoutMs})`,
    )
  }
  return { headersTimeoutMs, requestTimeoutMs }
}

/**
 * The floor under the sweep and the cleanup job: `0` would be a hot loop over the database, and
 * anything under a second has nothing to find that the previous pass did not.
 */
const MIN_SWEEP_INTERVAL_MS = 1_000

/** An integer when the variable is set, `undefined` when it is not, so a derived default can apply. */
function optionalInteger(raw: string | undefined, minimum = 0): number | undefined {
  return raw === undefined || raw === '' ? undefined : configInteger(raw, 0, minimum)
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

function level(raw: string | undefined): NodeConfig['logLevel'] {
  if (raw === undefined || raw === '') return 'info'
  if (raw === 'debug' || raw === 'info' || raw === 'warn' || raw === 'error') return raw
  throw new Error(`Unknown LOG_LEVEL "${raw}"`)
}
