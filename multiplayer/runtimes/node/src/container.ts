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
  const opened: OpenedStorage = await openStorage(parseStorageTarget(config.databaseUrl))
  const clock: Clock = options.clock ?? { now: () => new Date() }
  const hub = new LocalEventHub(opened.storage.events, DEFAULT_SERVER_CONFIG.sseHeartbeatMs, {
    ...DEFAULT_EVENT_HUB_LIMITS,
    perProcess: config.maxEventStreams,
  })

  let scheduler: TimerDeadlineScheduler | undefined
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
        batchSize: 50,
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
    },
    config: { ...DEFAULT_SERVER_CONFIG, publicListing: config.publicListing },
    // The socket address unless an operator has said how many proxies sit in front. It is the one
    // value a client cannot choose, so it is the default; `defaultClientAddress` explains what the
    // hop count buys and what getting it wrong costs.
    clientAddress: (c) =>
      config.trustedProxyHops > 0
        ? defaultClientAddress(c, { hops: config.trustedProxyHops - 1 })
        : (getConnInfo(c).remote.address ?? 'unknown'),
  }
  const app = createApp(container)
  logger.info('runtime ready', {
    databaseUrl: redactUrl(config.databaseUrl),
    publicListing: config.publicListing,
    retentionDays: config.retentionDays,
    bugReports: bugReports ? 'on' : 'off',
  })
  return {
    app,
    kernel,
    ...(bugReports ? { bugReports: bugReports.service } : {}),
    close: async () => {
      stopSweeper()
      scheduler?.stop()
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
    logger.warn('bug report intake is off: BUG_REPORT_DATABASE_URL must be sqlite:<path>', { url })
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

function redactUrl(url: string): string {
  try {
    const parsed = new URL(url)
    if (parsed.password) parsed.password = '***'
    return parsed.toString()
  } catch {
    return url
  }
}
