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
  /**
   * Age after which a running or desynced match is deleted whatever its roster says. 0 keeps them
   * forever.
   *
   * The roster test alone is not enough to collect anything. A player stops being `active` through
   * `leave`, `kick` or a missed deadline, and the game's default is an untimed match with no
   * deadline to miss, so two friends whose clients both died leave two `active` rows that nothing
   * ever clears. That is the ordinary end of an untimed match, and without this window those
   * matches, their orders, their events and up to five megabytes of snapshot are immortal.
   */
  silentLiveMaxAgeMs: number
  /** Matches deleted per sweep, so one pass cannot monopolise the database. */
  batchSize: number
}

const DAY_MS = 24 * 60 * 60 * 1000

export const DEFAULT_RETENTION: RetentionPolicy = {
  maxAgeMs: 30 * DAY_MS,
  abandonedLiveMaxAgeMs: 90 * DAY_MS,
  silentLiveMaxAgeMs: 180 * DAY_MS,
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
    return (
      (await this.collectTerminated()) +
      (await this.collectAbandonedLive()) +
      (await this.collectSilentLive())
    )
  }

  private collectTerminated(): Promise<number> {
    return this.collectWindow(
      this.policy.maxAgeMs,
      'retention deleted inactive matches',
      (before) =>
        this.deps.storage.matches.deleteInactive(COLLECTABLE, before, this.policy.batchSize),
    )
  }

  private collectAbandonedLive(): Promise<number> {
    return this.collectWindow(
      this.policy.abandonedLiveMaxAgeMs,
      'retention deleted long-abandoned live matches',
      (before) =>
        this.deps.storage.matches.deleteAbandonedLive(before, this.policy.batchSize, true),
    )
  }

  /** The same delete without the roster test, on a window long enough to stand in for it. */
  private collectSilentLive(): Promise<number> {
    return this.collectWindow(
      this.policy.silentLiveMaxAgeMs,
      'retention deleted silent live matches',
      (before) =>
        this.deps.storage.matches.deleteAbandonedLive(before, this.policy.batchSize, false),
    )
  }

  /** One retention window: a window of 0 is switched off and reads neither the clock nor storage. */
  private async collectWindow(
    maxAgeMs: number,
    message: string,
    run: (before: Date) => Promise<number>,
  ): Promise<number> {
    if (maxAgeMs <= 0) return 0
    const before = new Date(this.deps.clock.now().getTime() - maxAgeMs)
    const deleted = await run(before)
    if (deleted > 0) this.deps.logger.info(message, { deleted, before: before.toISOString() })
    return deleted
  }
}
