import type { BugReportService } from '@chaos-overlords/bug-reports'
import type { Kernel, Logger } from '@chaos-overlords/kernel'
import { startPeriodic } from './periodic.js'

export interface CleanupTargets {
  bugReports?: BugReportService
  /**
   * Deletes one batch of the rate limit windows that have rolled, from the table instances share on
   * Postgres, and returns how many went. Every instance runs it; a window two of them delete at
   * once is simply gone.
   */
  sweepRateLimits?: () => Promise<number>
}

/**
 * The most rate limit batches one pass deletes. A minute's windows from a flood of addresses can
 * outnumber one batch every minute, so a pass keeps going while batches come back non-empty, up to
 * this many, and the next pass takes whatever is left.
 */
const MAX_RATE_LIMIT_BATCHES = 20

/**
 * The cleanup job: deletes every stored row past its retention window. Returns the stop handle.
 *
 * Separate from the turn sweeper because the two answer to different clocks. The sweeper is a
 * safety net for deadlines and wants seconds; retention windows are days long, so running it every
 * few seconds only repeats an empty delete. Each pass deletes at most one batch per retention
 * window and up to {@link MAX_RATE_LIMIT_BATCHES} of rolled rate limit windows, and the interval is
 * what turns that into throughput when a backlog builds up.
 */
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
  // A rolled window left in the table is never read as live, so what one pass leaves behind costs
  // only space until the next.
  try {
    if (sweepRateLimits) {
      for (let batch = 0; batch < MAX_RATE_LIMIT_BATCHES; batch++) {
        if ((await sweepRateLimits()) === 0) break
      }
    }
  } catch (error) {
    logger.warn('rate limit sweep failed', { error: String(error) })
  }
}
