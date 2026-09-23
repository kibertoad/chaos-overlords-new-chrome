import {
  type BugReportService,
  bugReportSchema,
  createBugReportRepository,
  createBugReportService,
  createR2BlobStore,
  DEFAULT_BUG_REPORT_RETENTION,
} from '@chaos-overlords/bug-reports'
import {
  createKernel,
  DEFAULT_RETENTION_BATCH_SIZE,
  DEFAULT_RETENTION_DAYS,
  type DeadlineScheduler,
  type EventNotifier,
  type Kernel,
  type Logger,
  type RetentionPolicy,
  retentionPolicyFromDays,
  type StreamCloser,
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

/** A non-negative whole number from an environment variable; anything else reads as `fallback`. */
function wholeNumber(raw: string | undefined, fallback: number): number {
  const value = Number(raw ?? fallback)
  return Number.isInteger(value) && value >= 0 ? value : fallback
}

/** A whole number when the variable is set, `undefined` when it is unset or not a whole number. */
function optionalWholeNumber(raw: string | undefined): number | undefined {
  if (raw === undefined || raw === '') return undefined
  const value = Number(raw)
  return Number.isInteger(value) && value >= 0 ? value : undefined
}

/**
 * The retention windows from the vars, with the kernel's defaults for any that are unset.
 *
 * The lobby and silent windows are only passed when they are set to a whole number, so the kernel
 * derives them the same way it does on Node: the silent window from the abandoned one, the lobby
 * window from `RETENTION_DAYS`'s off switch. A mistyped value reads as unset rather than as a
 * fixed default, because a fixed silent window could end up shorter than the abandoned window and
 * collect matches whose players are still seated. Node refuses to start on the same input; a
 * Worker has no start to refuse, so the derived window is the safe reading.
 */
export function retentionPolicyFor(env: Env): RetentionPolicy {
  const lobby = optionalWholeNumber(env.LOBBY_RETENTION_DAYS)
  const silentLive = optionalWholeNumber(env.SILENT_RETENTION_DAYS)
  const batchSize = wholeNumber(env.RETENTION_BATCH_SIZE, DEFAULT_RETENTION_BATCH_SIZE)
  return retentionPolicyFromDays(
    {
      finished: wholeNumber(env.RETENTION_DAYS, DEFAULT_RETENTION_DAYS.finished),
      abandonedLive: wholeNumber(
        env.ABANDONED_RETENTION_DAYS,
        DEFAULT_RETENTION_DAYS.abandonedLive,
      ),
      ...(lobby === undefined ? {} : { lobby }),
      ...(silentLive === undefined ? {} : { silentLive }),
    },
    // A batch of 0 would delete nothing while looking switched on; the windows are the off switch.
    batchSize > 0 ? batchSize : DEFAULT_RETENTION_BATCH_SIZE,
  )
}

export const HUB_PATHS = {
  notify: '/notify',
  schedule: '/schedule',
  subscribe: '/subscribe',
  /** Hangs up the streams of a membership that has just been revoked. */
  disconnect: '/disconnect',
} as const

/** The per-match Durable Object, addressed by match id. */
export function hubFor(env: Env, matchId: string) {
  return env.MATCH_HUB.get(env.MATCH_HUB.idFromName(matchId))
}

/**
 * How long a call into a match's Durable Object may take before the caller gives up on it.
 *
 * Every one of them is best effort by design — fan-out, arming a deadline, hanging up a revoked
 * membership — and each has a safety net behind it: the stream's own catch-up drain, the sweep's
 * `listExpiredOpen`, and the revoked token itself. What they did not have was an end. An object
 * that is slow to wake, being relocated, or holding a lock kept the REQUEST waiting, so a seal
 * completed in the database and the submitter saw a timeout.
 */
const HUB_CALL_TIMEOUT_MS = 5_000

/**
 * A best-effort call into a hub: bounded in time, and its failure logged rather than raised.
 *
 * The step it belongs to has already committed whatever was durable about it, so the only thing a
 * failure here can still cost is latency somebody else's retry or the sweep already covers.
 */
async function tellHub(
  env: Env,
  call: { matchId: string; path: string; body: unknown },
): Promise<void> {
  const { matchId, path, body } = call
  try {
    await hubFor(env, matchId).fetch(`https://hub${path}`, {
      method: 'POST',
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(HUB_CALL_TIMEOUT_MS),
    })
  } catch (error) {
    workerLogger.warn('could not reach the match hub', { matchId, path, error: String(error) })
  }
}

/**
 * Builds the kernel over D1 for one invocation. Fan-out and alarms cross into the match's Durable
 * Object; everything else is plain D1 through the shared SQLite repositories.
 */
export function buildKernel(
  env: Env,
  overrides: Partial<{
    notifier: EventNotifier
    scheduler: DeadlineScheduler
    streams: StreamCloser
  }> = {},
): Kernel {
  const storage = createSqliteStorage(drizzle(env.DB, { schema: sqliteSchema }))
  const notifier: EventNotifier = overrides.notifier ?? {
    // The whole durable row, not just the match id. The object formats it once and hands the frame
    // to every stream of the match, so a seal's burst costs it no reads at all; being told only
    // which match had changed meant each subscriber queried D1 for rows the notification was
    // already carrying. The event has been persisted before this runs, so the body crossing the
    // isolate boundary is a copy of a fact, never a substitute for one.
    notify: async (event) =>
      tellHub(env, { matchId: event.matchId, path: HUB_PATHS.notify, body: event }),
  }
  const streams: StreamCloser = overrides.streams ?? {
    close: async (input) =>
      tellHub(env, { matchId: input.matchId, path: HUB_PATHS.disconnect, body: input }),
  }
  const scheduler: DeadlineScheduler = overrides.scheduler ?? {
    schedule: async (input) =>
      tellHub(env, {
        matchId: input.matchId,
        path: HUB_PATHS.schedule,
        body: { ...input, dueAt: input.dueAt.toISOString() },
      }),
  }
  return createKernel(
    {
      storage,
      notifier,
      scheduler,
      streams,
      clock: { now: () => new Date() },
      logger: workerLogger,
    },
    { retention: retentionPolicyFor(env) },
  )
}

/**
 * Bug report intake over its own D1 instance, or nothing when the binding is absent.
 *
 * Built beside the kernel rather than inside it: the kernel is the multiplayer domain, and a bug
 * report is not part of a match. Keeping them apart here is what makes the separate database
 * structural rather than a convention — nothing in `Kernel` can reach `BUG_DB`, and nothing here
 * can reach `DB`.
 *
 * The D1 migration lineage is `packages/bug-reports/migrations/sqlite`; apply it with
 * `wrangler d1 migrations apply chaos_overlords_bug_reports`.
 */
export function buildBugReports(env: Env): BugReportService | undefined {
  if (!env.BUG_DB) return undefined
  const repository = createBugReportRepository(drizzle(env.BUG_DB, { schema: bugReportSchema }))
  const blobs = env.BUG_BLOBS ? createR2BlobStore(env.BUG_BLOBS) : undefined
  return createBugReportService({
    repository,
    clock: { now: () => new Date() },
    logger: workerLogger,
    retention: {
      ...DEFAULT_BUG_REPORT_RETENTION,
      dailyStateBytes:
        wholeNumber(
          env.BUG_REPORT_DAILY_STATE_MB,
          DEFAULT_BUG_REPORT_RETENTION.dailyStateBytes / (1024 * 1024),
        ) *
        1024 *
        1024,
      maxAgeMs:
        wholeNumber(env.BUG_REPORT_RETENTION_DAYS, DEFAULT_BUG_REPORT_RETENTION.maxAgeMs / DAY_MS) *
        DAY_MS,
    },
    ...(blobs ? { blobs } : {}),
  })
}
