import type { MatchStatus } from '@chaos-overlords/contracts'
import type { KernelDeps } from './deps'

/** Matches that will never be played again, and are therefore the only ones retention may delete. */
const COLLECTABLE: readonly MatchStatus[] = ['finished', 'abandoned', 'lobby']

export interface RetentionPolicy {
  /** Age after which a finished, abandoned or never-started match is deleted. 0 keeps everything. */
  maxAgeMs: number
  /**
   * Age after which a RUNNING or desynced match with nobody active in it is deleted. 0 keeps them
   * forever, which is what the server used to do.
   *
   * Far longer than {@link maxAgeMs} on purpose. A match in this state is not over — it is kept
   * precisely so a player who closed the game in March can come back and rejoin — so the window has
   * to be long enough that collecting one is the same statement as "nobody is coming back". It is
   * the ordinary end of a match on a public server, and nothing else ever collected it.
   */
  abandonedLiveMaxAgeMs: number
  /** Matches deleted per sweep, so one pass cannot monopolise the database. */
  batchSize: number
}

const DAY_MS = 24 * 60 * 60 * 1000

export const DEFAULT_RETENTION: RetentionPolicy = {
  maxAgeMs: 30 * DAY_MS,
  abandonedLiveMaxAgeMs: 90 * DAY_MS,
  batchSize: 50,
}

/**
 * Deletes the matches nobody will come back to. A coordination server accumulates rows that have no
 * second use — a finished match's order documents, a megabyte of snapshot per desync, the lobby
 * someone opened and never started — and a self-hosted SQLite file has no operator watching it
 * grow. Deleting the match row takes everything it owns with it, because every child table cascades.
 *
 * A running or desynced match is collected only when it has been silent for the much longer second
 * window AND holds no active player. A desync pause is not abandonment and neither is a weekend;
 * everybody having left months ago is.
 */
export class RetentionService {
  constructor(
    private readonly deps: Pick<KernelDeps, 'storage' | 'clock' | 'logger'>,
    private readonly policy: RetentionPolicy = DEFAULT_RETENTION,
  ) {}

  async collect(): Promise<number> {
    return (await this.collectTerminated()) + (await this.collectAbandonedLive())
  }

  private async collectTerminated(): Promise<number> {
    if (this.policy.maxAgeMs <= 0) return 0
    const before = new Date(this.deps.clock.now().getTime() - this.policy.maxAgeMs)
    const deleted = await this.deps.storage.matches.deleteInactive(
      COLLECTABLE,
      before,
      this.policy.batchSize,
    )
    if (deleted > 0) {
      this.deps.logger.info('retention deleted inactive matches', {
        deleted,
        before: before.toISOString(),
      })
    }
    return deleted
  }

  private async collectAbandonedLive(): Promise<number> {
    if (this.policy.abandonedLiveMaxAgeMs <= 0) return 0
    const before = new Date(this.deps.clock.now().getTime() - this.policy.abandonedLiveMaxAgeMs)
    const deleted = await this.deps.storage.matches.deleteAbandonedLive(
      before,
      this.policy.batchSize,
    )
    if (deleted > 0) {
      this.deps.logger.info('retention deleted long-abandoned live matches', {
        deleted,
        before: before.toISOString(),
      })
    }
    return deleted
  }
}
