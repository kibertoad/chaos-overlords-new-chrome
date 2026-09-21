import {
  type BugReportService,
  createBugReportService,
  createMemoryBlobStore,
  DEFAULT_BUG_REPORT_RETENTION,
} from '@chaos-overlords/bug-reports'
import { createFileBlobStore, openBugReportStorage } from '@chaos-overlords/bug-reports/node'
import {
  type Clock,
  createKernel,
  type Kernel,
  type Logger,
  RateLimiter,
} from '@chaos-overlords/kernel'
import {
  type AppEnv,
  createApp,
  DEFAULT_EVENT_HUB_LIMITS,
  DEFAULT_RATE_LIMITS,
  DEFAULT_SERVER_CONFIG,
  defaultClientAddress,
  LocalEventHub,
  type ServerContainer,
} from '@chaos-overlords/server'
import { type OpenedStorage, openStorage, parseStorageTarget } from '@chaos-overlords/storage/node'
import { getConnInfo } from '@hono/node-server/conninfo'
import type { Hono } from 'hono'
import type { NodeConfig } from './config.js'
import { createLogger } from './logger.js'
import { startSweeper, TimerDeadlineScheduler } from './TimerDeadlineScheduler.js'

export interface NodeRuntime {
  app: Hono<AppEnv>
  kernel: Kernel
  /** Bug report intake, when this server is configured to take them. */
  bugReports?: BugReportService
  /**
   * Ends every open event stream and stops the background timers, without closing the databases.
   *
   * The first half of a graceful shutdown. An event stream never ends on its own, so `server.close`
   * cannot return while one is open; ending them lets the HTTP server go quiet at once and lets
   * clients reconnect to the next process with their `Last-Event-ID` instead of seeing a reset.
   */
  closeStreams(): void
  /** Releases timers and both databases. */
  close(): Promise<void>
}

export interface NodeRuntimeOptions {
  /**
   * Overrides the system clock. Tests drive the deadline and retention paths through the real HTTP
   * facade with it; nothing else has a reason to.
   */
  clock?: Clock
}

const DAY_MS = 24 * 60 * 60 * 1000

/**
 * Builds the whole facade from config: storage (migrated), the kernel with Node ports, the app.
 * The deadline scheduler needs the turn service and the turn service needs the scheduler, so the
 * scheduler is bound through a late-set reference rather than a second container.
 */
export async function buildNodeRuntime(
  config: NodeConfig,
  options: NodeRuntimeOptions = {},
): Promise<NodeRuntime> {
  const logger = createLogger(config.logLevel)
  const opened: OpenedStorage = await openStorage(
    parseStorageTarget(config.databaseUrl),
    // Log it and carry on. The pool reconnects on the next query; without a listener the emitter
    // throws and the uncaught-exception handler takes the whole server down with it.
    (error) => logger.warn('database pool reported an error', { error: String(error) }),
  )
  const clock: Clock = options.clock ?? { now: () => new Date() }
  const hub = new LocalEventHub(
    opened.storage.events,
    DEFAULT_SERVER_CONFIG.sseHeartbeatMs,
    { ...DEFAULT_EVENT_HUB_LIMITS, perProcess: config.maxEventStreams },
    (matchId, seq) => logger.warn('skipped an unreadable stored event', { matchId, seq }),
  )

  let scheduler: TimerDeadlineScheduler | undefined
  let warnedAboutProxy = false
  const kernel = createKernel(
    {
      storage: opened.storage,
      notifier: hub,
      streams: hub,
      clock,
      logger,
      scheduler: { schedule: (input) => (scheduler as TimerDeadlineScheduler).schedule(input) },
    },
    {
      retention: {
        maxAgeMs: config.retentionDays * DAY_MS,
        abandonedLiveMaxAgeMs: config.abandonedRetentionDays * DAY_MS,
        // Twice the abandoned window, and without its roster test. The roster test alone never
        // collects an untimed match whose players' clients died without a `leave`, which is the
        // ordinary end of one.
        silentLiveMaxAgeMs: config.abandonedRetentionDays * 2 * DAY_MS,
        // Retention runs on the request thread when the driver is synchronous, and a match is
        // everything it owns: up to five megabytes of snapshot and its whole event log. Smaller
        // batches every sweep bound how long one pass can hold every stream and request still.
        batchSize: opened.dialect === 'sqlite' ? 10 : 50,
      },
    },
  )
  scheduler = new TimerDeadlineScheduler(kernel.turns, clock, logger)
  const bugReports = openBugReports(config, clock, logger)
  const stopSweeper = startSweeper(kernel, config.sweepIntervalMs, logger, bugReports?.service)

  const perMinute = (limit: number) => new RateLimiter(clock, { limit, windowMs: 60_000 })
  const container: ServerContainer = {
    kernel,
    ...(bugReports ? { bugReports: bugReports.service } : {}),
    eventStream: hub,
    rateLimiters: {
      anonymous: perMinute(config.rateLimitPerMinute),
      member: perMinute(config.memberRateLimitPerMinute),
      upload: perMinute(config.uploadRateLimitPerMinute),
      bugReport: perMinute(config.bugReportRateLimitPerMinute),
      bugReportState: new RateLimiter(clock, {
        limit: DEFAULT_RATE_LIMITS.bugReportStatePerDay,
        windowMs: DAY_MS,
      }),
    },
    config: {
      ...DEFAULT_SERVER_CONFIG,
      publicListing: config.publicListing,
      corsOrigins: config.corsOrigins,
    },
    // The socket address unless an operator has said how many proxies sit in front. It is the one
    // value a client cannot choose, so it is the default; `defaultClientAddress` explains what the
    // hop count buys and what getting it wrong costs.
    clientAddress: (c) => {
      if (config.trustedProxyHops > 0) {
        return defaultClientAddress(c, { hops: config.trustedProxyHops - 1 })
      }
      // Every caller behind an unannounced proxy shares the proxy's address, and with it one
      // rate-limit budget: one stranger's bad tokens lock everybody out of create and join. Say so
      // once, the first time a forwarded header arrives, rather than leaving it to be found.
      if (!warnedAboutProxy && c.req.header('x-forwarded-for') !== undefined) {
        warnedAboutProxy = true
        logger.warn(
          'X-Forwarded-For received but TRUST_PROXY is unset: every client shares one address',
        )
      }
      return getConnInfo(c).remote.address ?? 'unknown'
    },
  }
  const app = createApp(container)
  logger.info('runtime ready', {
    databaseUrl: redactUrl(config.databaseUrl),
    publicListing: config.publicListing,
    retentionDays: config.retentionDays,
    bugReports: bugReports ? 'on' : 'off',
  })
  const closeStreams = (): void => {
    stopSweeper()
    scheduler?.stop()
    hub.closeAll()
  }
  return {
    app,
    kernel,
    ...(bugReports ? { bugReports: bugReports.service } : {}),
    closeStreams,
    close: async () => {
      closeStreams()
      await opened.close()
      await bugReports?.close()
    },
  }
}

/**
 * Opens the bug report side, or reports that it stays shut.
 *
 * Its own database file, opened separately from the match one and closed separately: the two areas
 * share a process and nothing else. A configuration this cannot honour (a Postgres URL, an
 * unopenable file) turns the intake off and logs why, rather than refusing to start a server whose
 * actual job is hosting matches.
 */
function openBugReports(
  config: NodeConfig,
  clock: Clock,
  logger: Logger,
): { service: BugReportService; close: () => Promise<void> } | undefined {
  const url = config.bugReportDatabaseUrl.trim()
  if (url === '') return undefined
  if (!url.startsWith('sqlite:')) {
    logger.warn('bug report intake is off: BUG_REPORT_DATABASE_URL must be sqlite:<path>', {
      url: redactUrl(url),
    })
    return undefined
  }
  const filename = url.slice('sqlite:'.length) || ':memory:'
  try {
    const opened = openBugReportStorage(filename)
    // A configured directory wins. Failing that, an in-memory database has no file for an archive
    // to outlive, so it gets an in-memory store; a real file keeps small archives in its own rows.
    const blobs = blobStoreFor(config.bugReportBlobDirectory, filename)
    return {
      service: createBugReportService({
        repository: opened.repository,
        clock,
        logger,
        retention: {
          ...DEFAULT_BUG_REPORT_RETENTION,
          dailyStateBytes: config.bugReportDailyStateMb * 1024 * 1024,
          maxAgeMs: config.bugReportRetentionDays * DAY_MS,
        },
        ...(blobs ? { blobs } : {}),
      }),
      close: opened.close,
    }
  } catch (error) {
    logger.warn('bug report intake is off: its database could not be opened', {
      error: String(error),
    })
    return undefined
  }
}

function blobStoreFor(directory: string, filename: string) {
  if (directory !== '') return createFileBlobStore(directory)
  if (filename === ':memory:') return createMemoryBlobStore()
  return undefined
}

/**
 * A database URL with its password removed, for the log.
 *
 * A URL the parser refuses is replaced outright rather than returned as it came. `new URL` throws
 * on a password carrying an unescaped `#` or `/`, which is exactly the URL whose password must not
 * reach a log collector.
 */
function redactUrl(url: string): string {
  try {
    const parsed = new URL(url)
    if (parsed.password) parsed.password = '***'
    return parsed.toString()
  } catch {
    return '(unparseable url)'
  }
}
