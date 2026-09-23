import type { MatchStatus } from '@chaos-overlords/contracts'
import type { KernelDeps } from './deps'

/** Matches that are over, and will never be played again. */
const TERMINATED: readonly MatchStatus[] = ['finished', 'abandoned']

/** A lobby nobody ever started. It has no game in it, only a seat list and a listing slot. */
const NEVER_STARTED: readonly MatchStatus[] = ['lobby']

export interface RetentionPolicy {
  /** Age after which a finished or abandoned match is deleted. 0 keeps them forever. */
  finishedMaxAgeMs: number
  /**
   * Age after which a lobby that was never started is deleted. 0 keeps them forever.
   *
   * Its own window, and a short one, because a lobby holds nothing worth keeping: no turn has been
   * played in it. On a public server the forgotten ones are also what clutters the Browse list.
   * The age runs from creation or the last settings change; a join does not refresh it.
   *
   * Left unset, it follows the finished window's off switch: a server that keeps its finished
   * matches forever (`finished: 0`) kept its lobbies forever too before this window existed, and an
   * upgrade must not start deleting them underneath the guests seated in them.
   */
  lobbyMaxAgeMs: number
  /**
   * Age after which a RUNNING or desynced match with nobody active in it is deleted. 0 keeps them
   * forever.
   *
   * Longer than {@link finishedMaxAgeMs} on purpose. A match in this state is not over — it is kept
   * precisely so a player who closed the game can come back and rejoin — so the window has to be
   * long enough that collecting one is the same statement as "nobody is coming back". It is the
   * ordinary end of a match on a public server, and nothing else ever collects it.
   *
   * A join or a rejoin restarts the age, as every turn open and status change does.
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
  /** Matches deleted per window per sweep, so one pass cannot monopolise the database. */
  batchSize: number
}

/**
 * The retention windows in whole days, which is how both runtimes let an operator state them.
 *
 * Two are optional, and each follows another window when left out:
 * - `lobby` is {@link DEFAULT_RETENTION_DAYS}'s lobby window, or 0 when `finished` is 0, so a
 *   server that switched retention off before lobbies had their own window keeps them too.
 * - `silentLive` is {@link SILENT_TO_ABANDONED_RATIO} times `abandonedLive`, so the window without
 *   the roster test stays the longer of the two and switching the abandoned window off (0)
 *   switches both off.
 */
export interface RetentionDays {
  finished: number
  lobby?: number
  abandonedLive: number
  silentLive?: number
}

/**
 * Defaults sized for a shared public server, where nobody is watching one self-hosted file grow and
 * the database is every stranger's matches. A server for a group of friends that wants to keep its
 * history longer raises them through configuration; nothing else depends on their values.
 */
export const DEFAULT_RETENTION_DAYS: Required<RetentionDays> = {
  finished: 14,
  lobby: 3,
  abandonedLive: 30,
  silentLive: 90,
}

export const SILENT_TO_ABANDONED_RATIO = 3

export const DEFAULT_RETENTION_BATCH_SIZE = 50

const DAY_MS = 24 * 60 * 60 * 1000

/** Maps windows stated in days onto the policy, deriving the windows that are left out. */
export function retentionPolicyFromDays(
  days: RetentionDays,
  batchSize = DEFAULT_RETENTION_BATCH_SIZE,
): RetentionPolicy {
  const lobby = days.lobby ?? (days.finished === 0 ? 0 : DEFAULT_RETENTION_DAYS.lobby)
  const silentLive = days.silentLive ?? days.abandonedLive * SILENT_TO_ABANDONED_RATIO
  return {
    finishedMaxAgeMs: days.finished * DAY_MS,
    lobbyMaxAgeMs: lobby * DAY_MS,
    abandonedLiveMaxAgeMs: days.abandonedLive * DAY_MS,
    silentLiveMaxAgeMs: silentLive * DAY_MS,
    batchSize,
  }
}

export const DEFAULT_RETENTION: RetentionPolicy = retentionPolicyFromDays(DEFAULT_RETENTION_DAYS)

/**
 * Deletes the matches nobody will come back to. A coordination server accumulates rows that have no
 * second use — a finished match's order documents, a megabyte of snapshot per desync, the lobby
 * someone opened and never started — and every one of them has a window here after which it goes. Deleting the match row takes everything it owns with it, because every child table cascades.
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
      (await this.collectNeverStarted()) +
      (await this.collectAbandonedLive()) +
      (await this.collectSilentLive())
    )
  }

  private collectTerminated(): Promise<number> {
    return this.collectWindow(
      this.policy.finishedMaxAgeMs,
      'retention deleted finished matches',
      (before) =>
        this.deps.storage.matches.deleteInactive(TERMINATED, before, this.policy.batchSize),
    )
  }

  private collectNeverStarted(): Promise<number> {
    return this.collectWindow(
      this.policy.lobbyMaxAgeMs,
      'retention deleted lobbies that never started',
      (before) =>
        this.deps.storage.matches.deleteInactive(NEVER_STARTED, before, this.policy.batchSize),
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
