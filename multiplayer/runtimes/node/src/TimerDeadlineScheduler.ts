import type { Clock, DeadlineScheduler, Kernel, Logger, TurnService } from '@chaos-overlords/kernel'
import { startPeriodic } from './periodic.js'

/**
 * One `setTimeout` per open turn, replaced when the same match schedules again. Timers do not
 * survive a restart, which is why `startSweeper` re-scans the table on an interval.
 */
export class TimerDeadlineScheduler implements DeadlineScheduler {
  private readonly timers = new Map<string, NodeJS.Timeout>()

  constructor(
    private readonly turns: TurnService,
    private readonly clock: Clock,
    private readonly logger: Logger,
  ) {}

  async schedule({
    matchId,
    turn,
    dueAt,
  }: {
    matchId: string
    turn: number
    dueAt: Date
  }): Promise<void> {
    const existing = this.timers.get(matchId)
    if (existing) clearTimeout(existing)
    const delay = Math.max(0, dueAt.getTime() - this.clock.now().getTime())
    const timer = setTimeout(() => {
      this.timers.delete(matchId)
      this.turns.trySeal(matchId, turn, 'deadline').catch((error: unknown) => {
        this.logger.warn('deadline seal failed; the sweeper will retry', {
          matchId,
          turn,
          error: String(error),
        })
      })
    }, delay)
    timer.unref()
    this.timers.set(matchId, timer)
  }

  stop(): void {
    for (const timer of this.timers.values()) clearTimeout(timer)
    this.timers.clear()
  }
}

/**
 * The periodic safety net: seals turns whose timer was lost to a restart and finishes seals that
 * were interrupted halfway. Returns the stop handle. Retention is the cleanup job's, not this one's.
 */
export function startSweeper(kernel: Kernel, intervalMs: number, logger: Logger): () => void {
  return startPeriodic(intervalMs, () => sweep(kernel, logger))
}

async function sweep(kernel: Kernel, logger: Logger): Promise<void> {
  try {
    const { sealed, repaired } = await kernel.turns.sweep()
    if (sealed > 0 || repaired > 0) logger.info('sweeper advanced turns', { sealed, repaired })
  } catch (error) {
    logger.warn('turn sweep failed', { error: String(error) })
  }
}
