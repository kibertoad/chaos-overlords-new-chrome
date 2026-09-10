import type { MatchStatus } from '@chaos-overlords/contracts'
import type { KernelDeps } from './deps'

/** Matches that will never be played again, and are therefore the only ones retention may delete. */
const COLLECTABLE: readonly MatchStatus[] = ['finished', 'abandoned', 'lobby']

export interface RetentionPolicy {
  /** Age after which a finished, abandoned or never-started match is deleted. 0 keeps everything. */
  maxAgeMs: number
  /** Matches deleted per sweep, so one pass cannot monopolise the database. */
  batchSize: number
}

export const DEFAULT_RETENTION: RetentionPolicy = {
  maxAgeMs: 30 * 24 * 60 * 60 * 1000,
  batchSize: 50,
}

/**
 * Deletes the matches nobody will come back to. A coordination server accumulates rows that have no
 * second use — a finished match's order documents, a megabyte of snapshot per desync, the lobby
 * someone opened and never started — and a self-hosted SQLite file has no operator watching it
 * grow. Deleting the match row takes everything it owns with it, because every child table cascades.
 *
 * Running and desynced matches are never in scope at any age: a desync pause is not abandonment.
 */
export class RetentionService {
  constructor(
    private readonly deps: Pick<KernelDeps, 'storage' | 'clock' | 'logger'>,
    private readonly policy: RetentionPolicy = DEFAULT_RETENTION,
  ) {}

  async collect(): Promise<number> {
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
}
