import { type Clock, createKernel, type Kernel, RateLimiter } from '@chaos-overlords/kernel'
import {
  type AppEnv,
  createApp,
  DEFAULT_SERVER_CONFIG,
  defaultClientAddress,
  LocalEventHub,
  type ServerContainer,
} from '@chaos-overlords/server'
import { type OpenedStorage, openStorage, parseStorageTarget } from '@chaos-overlords/storage/node'
import { getConnInfo } from '@hono/node-server/conninfo'
import type { Hono } from 'hono'
import type { NodeConfig } from './config'
import { createLogger } from './logger'
import { startSweeper, TimerDeadlineScheduler } from './TimerDeadlineScheduler'

export interface NodeRuntime {
  app: Hono<AppEnv>
  kernel: Kernel
  /** Releases timers and the database. */
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
  const hub = new LocalEventHub(opened.storage.events, DEFAULT_SERVER_CONFIG.sseHeartbeatMs)

  let scheduler: TimerDeadlineScheduler | undefined
  const kernel = createKernel(
    {
      storage: opened.storage,
      notifier: hub,
      clock,
      logger,
      scheduler: { schedule: (input) => (scheduler as TimerDeadlineScheduler).schedule(input) },
    },
    { retention: { maxAgeMs: config.retentionDays * DAY_MS, batchSize: 50 } },
  )
  scheduler = new TimerDeadlineScheduler(kernel.turns, clock, logger)
  const stopSweeper = startSweeper(kernel, config.sweepIntervalMs, logger)

  const perMinute = (limit: number) => new RateLimiter(clock, { limit, windowMs: 60_000 })
  const container: ServerContainer = {
    kernel,
    eventStream: hub,
    rateLimiters: {
      anonymous: perMinute(config.rateLimitPerMinute),
      member: perMinute(config.memberRateLimitPerMinute),
      upload: perMinute(config.uploadRateLimitPerMinute),
    },
    config: { ...DEFAULT_SERVER_CONFIG, publicListing: config.publicListing },
    clientAddress: (c) =>
      config.trustProxy ? defaultClientAddress(c) : (getConnInfo(c).remote.address ?? 'unknown'),
  }
  const app = createApp(container)
  logger.info('runtime ready', {
    databaseUrl: redactUrl(config.databaseUrl),
    publicListing: config.publicListing,
    retentionDays: config.retentionDays,
  })
  return {
    app,
    kernel,
    close: async () => {
      stopSweeper()
      scheduler?.stop()
      await opened.close()
    },
  }
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
