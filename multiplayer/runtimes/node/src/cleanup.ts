import type { BugReportService } from '@chaos-overlords/bug-reports'
import type { Kernel, Logger } from '@chaos-overlords/kernel'
import type { ClusterBus } from './cluster.js'
import { startPeriodic } from './periodic.js'

/**
 * The cleanup job: deletes every stored row past its retention window. Returns the stop handle.
 *
 * Separate from the turn sweeper because the two answer to different clocks. The sweeper is a
 * safety net for deadlines and wants seconds; retention windows are days long, so running it every
 * few seconds only repeats an empty delete. Each pass deletes at most one batch per window, and the
 * interval is what turns that into throughput when a backlog builds up.
 */
export function startCleanup(
  kernel: Kernel,
  intervalMs: number,
  logger: Logger,
  shared: { bus: Pick<ClusterBus, 'exclusive'>; bugReports?: BugReportService | undefined },
): () => void {
  return startPeriodic(intervalMs, () => cleanup(kernel, logger, shared.bus, shared.bugReports))
}

async function cleanup(
  kernel: Kernel,
  logger: Logger,
  bus: Pick<ClusterBus, 'exclusive'>,
  bugReports?: BugReportService,
): Promise<void> {
  // One instance at a time for the shared match database: concurrent passes would race to delete
  // the same matches, and on Postgres two cascading deletes over overlapping rows can deadlock.
  try {
    await bus.exclusive('retention', () => kernel.retention.collect().then(() => undefined))
  } catch (error) {
    logger.warn('retention sweep failed', { error: String(error) })
  }
  // The intake keeps nothing forever either. It is a different database with a different window,
  // so it gets its own call and its own failure: neither sweep may take the other down. That
  // database is a SQLite file of this instance's own, so every instance collects its own.
  try {
    await bugReports?.collect()
  } catch (error) {
    logger.warn('bug report retention sweep failed', { error: String(error) })
  }
}
