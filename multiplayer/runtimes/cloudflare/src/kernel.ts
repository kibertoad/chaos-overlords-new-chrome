import {
  createKernel,
  type DeadlineScheduler,
  type EventNotifier,
  type Kernel,
  type Logger,
} from '@chaos-overlords/kernel'
import { createSqliteStorage, sqliteSchema } from '@chaos-overlords/storage/sqlite'
import { drizzle } from 'drizzle-orm/d1'
import type { Env } from './env'

/** Console-backed structured logging; Workers observability ingests it as JSON. */
export const workerLogger: Logger = {
  debug: (msg, fields) => console.debug(JSON.stringify({ level: 'debug', msg, ...fields })),
  info: (msg, fields) => console.info(JSON.stringify({ level: 'info', msg, ...fields })),
  warn: (msg, fields) => console.warn(JSON.stringify({ level: 'warn', msg, ...fields })),
  error: (msg, fields) => console.error(JSON.stringify({ level: 'error', msg, ...fields })),
}

const DAY_MS = 24 * 60 * 60 * 1000
const DEFAULT_RETENTION_DAYS = 30

export const HUB_PATHS = {
  notify: '/notify',
  schedule: '/schedule',
  subscribe: '/subscribe',
} as const

/** The per-match Durable Object, addressed by match id. */
export function hubFor(env: Env, matchId: string) {
  return env.MATCH_HUB.get(env.MATCH_HUB.idFromName(matchId))
}

/**
 * Builds the kernel over D1 for one invocation. Fan-out and alarms cross into the match's Durable
 * Object; everything else is plain D1 through the shared SQLite repositories.
 */
export function buildKernel(
  env: Env,
  overrides: Partial<{ notifier: EventNotifier; scheduler: DeadlineScheduler }> = {},
): Kernel {
  const storage = createSqliteStorage(drizzle(env.DB, { schema: sqliteSchema }))
  const notifier: EventNotifier = overrides.notifier ?? {
    notify: async (event) => {
      await hubFor(env, event.matchId).fetch(`https://hub${HUB_PATHS.notify}`, {
        method: 'POST',
        body: JSON.stringify({ matchId: event.matchId }),
      })
    },
  }
  const scheduler: DeadlineScheduler = overrides.scheduler ?? {
    schedule: async (input) => {
      await hubFor(env, input.matchId).fetch(`https://hub${HUB_PATHS.schedule}`, {
        method: 'POST',
        body: JSON.stringify({ ...input, dueAt: input.dueAt.toISOString() }),
      })
    },
  }
  const retentionDays = Number(env.RETENTION_DAYS ?? DEFAULT_RETENTION_DAYS)
  return createKernel(
    {
      storage,
      notifier,
      scheduler,
      clock: { now: () => new Date() },
      logger: workerLogger,
    },
    {
      retention: {
        maxAgeMs:
          (Number.isInteger(retentionDays) && retentionDays >= 0
            ? retentionDays
            : DEFAULT_RETENTION_DAYS) * DAY_MS,
        batchSize: 50,
      },
    },
  )
}
