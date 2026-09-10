import { createKernel, type Kernel, RateLimiter } from '@chaos-overlords/kernel'
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

/**
 * Builds the whole facade from config: storage (migrated), the kernel with Node ports, the app.
 * The deadline scheduler needs the turn service and the turn service needs the scheduler, so the
 * scheduler is bound through a late-set reference rather than a second container.
 */
export async function buildNodeRuntime(config: NodeConfig): Promise<NodeRuntime> {
  const logger = createLogger(config.logLevel)
  const opened: OpenedStorage = await openStorage(parseStorageTarget(config.databaseUrl))
  const clock = { now: () => new Date() }
  const hub = new LocalEventHub(opened.storage.events, DEFAULT_SERVER_CONFIG.sseHeartbeatMs)

  let scheduler: TimerDeadlineScheduler | undefined
  const kernel = createKernel({
    storage: opened.storage,
    notifier: hub,
    clock,
    logger,
    scheduler: { schedule: (input) => (scheduler as TimerDeadlineScheduler).schedule(input) },
  })
  scheduler = new TimerDeadlineScheduler(kernel.turns, clock, logger)
  const stopSweeper = startSweeper(kernel.turns, config.sweepIntervalMs, logger)

  const container: ServerContainer = {
    kernel,
    eventStream: hub,
    rateLimiter: new RateLimiter(clock, { limit: config.rateLimitPerMinute, windowMs: 60_000 }),
    config: { ...DEFAULT_SERVER_CONFIG, publicListing: config.publicListing },
    clientAddress: (c) =>
      config.trustProxy ? defaultClientAddress(c) : (getConnInfo(c).remote.address ?? 'unknown'),
  }
  const app = createApp(container)
  logger.info('runtime ready', {
    databaseUrl: redactUrl(config.databaseUrl),
    publicListing: config.publicListing,
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
