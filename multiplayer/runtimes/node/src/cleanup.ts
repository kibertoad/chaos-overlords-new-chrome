import type { BugReportService } from '@chaos-overlords/bug-reports'
import type { Kernel, Logger } from '@chaos-overlords/kernel'
import { startPeriodic } from './periodic.js'

/**
 * The cleanup job: deletes every stored row past its retention window. Returns the stop handle.
 *
 * Separate from the turn sweeper because the two answer to different clocks. The sweeper is a
 * safety net for deadlines and wants seconds; retention windows are days long, so running it every
 * few seconds only repeats an empty delete. Each pass deletes at most one batch per window, and the
 * interval is what turns that into throughput when a backlog builds up.
 */
export interface CleanupTargets {
  bugReports?: BugReportService
  /**
   * Deletes the rate limit windows that have rolled, from the table instances share on Postgres.
   * Every instance runs it; a window two of them delete at once is simply gone.
   */
  sweepRateLimits?: () => Promise<number>
}

export function startCleanup(
  kernel: Kernel,
  intervalMs: number,
  logger: Logger,
  targets: CleanupTargets = {},
): () => void {
  return startPeriodic(intervalMs, () => cleanup(kernel, logger, targets))
}

async function cleanup(
  kernel: Kernel,
  logger: Logger,
  { bugReports, sweepRateLimits }: CleanupTargets,
): Promise<void> {
  try {
    await kernel.retention.collect()
  } catch (error) {
    logger.warn('retention sweep failed', { error: String(error) })
  }
  // The intake keeps nothing forever either. It is a different database with a different window,
  // so it gets its own call and its own failure: neither sweep may take the other down.
  try {
    await bugReports?.collect()
  } catch (error) {
    logger.warn('bug report retention sweep failed', { error: String(error) })
  }
  // One batch per pass. A minute's windows from a flood of addresses can outnumber it, and the
  // next pass takes the rest; a rolled window left in the table is never read as live.
  try {
    await sweepRateLimits?.()
  } catch (error) {
    logger.warn('rate limit sweep failed', { error: String(error) })
  }
}
