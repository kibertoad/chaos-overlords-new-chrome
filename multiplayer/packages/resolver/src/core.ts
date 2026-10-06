import type { BootedResolver, ResolverExports } from './boot.js'
import { MatchNotHeldError, ResolverRefusedError } from './errors.js'

/** Where a held match stands after a call. */
export interface MatchStatus {
  /** The state hash every client should report for the same facts. */
  stateHash: string
  /** The turn the match is planning: the next sealed set must be for this turn. */
  turn: number
  /** Whether the match has reached its outcome. */
  finished: boolean
}

/** The facts a match is built from on `match.started`, as the server stores them. */
export interface BootstrapInput {
  seed: number
  /** The stored `gameSettings` blob, verbatim. */
  gameSettings: unknown
  /** The seated roster as `PlayerView` rows. */
  players: unknown
}

/** How a match picked up from a snapshot is fed the events after it. */
export interface RestoreInput {
  /**
   * The roster as the server holds it now, as `PlayerView` rows, so that the events fed after the
   * snapshot find every seat that has ever been human, late joins included.
   */
  players: unknown
  /**
   * The turn the log is on at the first event that will be fed: 1 when the feed starts at the log's
   * beginning, as a reconnecting client's does. Left out, the turn the snapshot's state is on, for a
   * feed that starts after the last event the snapshot holds.
   */
  logTurn?: number
}

/** One step of a feed: an event of the match's log, with its sealed set when it is a seal. */
export interface FeedStep {
  event: unknown
  sealedOrders?: unknown
}

/** What a seal the feed applied resolved to. */
export interface ResolvedSeal {
  turn: number
  stateHash: string
  finished: boolean
}

/** The outcome of {@link ResolverCore.applyEvents}. */
export interface FeedResult {
  /**
   * False when the match was not planning the turn the feed was written for, in which case
   * nothing was applied: another caller fed the same events first.
   */
  applied: boolean
  /** Where the match stands after the feed, or as it was found when nothing was applied. */
  status: MatchStatus
  /** Every seal the feed applied, in log order, with the state it left. */
  seals: ResolvedSeal[]
}

export interface ResolverLimits {
  /** The most matches held at once; the least recently used goes first. */
  maxMatches: number
  /**
   * The live managed heap, in bytes, above which the least recently used matches are released
   * until it fits again or one match is left.
   */
  managedHeapBudgetBytes: number
}

export interface ResolverInfo {
  sessionVersion: number
  snapshotFormatVersion: number
  held: number
  /** The WebAssembly memory, which only grows. */
  memoryBytes: number
  /** The managed heap now, without forcing a collection. */
  managedHeapBytes: number
}

/**
 * The matches one runtime holds, by match id.
 *
 * It runs where the runtime is, synchronously: in a Node worker thread, or in a workerd isolate
 * behind a Durable Object. A match is a few megabytes of managed objects, so it stays in the runtime
 * between calls under a handle, and this keeps those handles, least recently used first, within
 * {@link ResolverLimits}. A match it no longer holds answers {@link MatchNotHeldError}, and the
 * caller rebuilds it from a snapshot and the facts after it.
 */
export class ResolverCore {
  private readonly exports: ResolverExports
  private readonly handles = new Map<string, number>()

  constructor(
    private readonly booted: BootedResolver,
    private readonly limits: ResolverLimits,
  ) {
    this.exports = booted.exports
  }

  info(): ResolverInfo {
    return {
      sessionVersion: this.booted.manifest.sessionVersion,
      snapshotFormatVersion: this.booted.manifest.snapshotFormatVersion,
      held: this.handles.size,
      memoryBytes: this.booted.memoryBytes(),
      managedHeapBytes: this.exports.ManagedHeapBytes(false),
    }
  }

  /** Builds a match the way every client does on `match.started`, replacing any held under the id. */
  bootstrap(matchId: string, input: BootstrapInput): MatchStatus {
    const handle = this.refused(() =>
      this.exports.Bootstrap(
        input.seed,
        JSON.stringify(input.gameSettings),
        JSON.stringify(input.players),
      ),
    )
    return this.hold(matchId, handle)
  }

  /**
   * Picks a match up from a native save payload (the bytes a snapshot archive compresses), refused
   * unless it hashes to `stateHash`. Replaces any match held under the id.
   */
  restore(
    matchId: string,
    savePayload: Uint8Array,
    stateHash: string,
    input: RestoreInput,
  ): MatchStatus {
    const handle = this.refused(() =>
      this.exports.Restore(
        savePayload,
        stateHash,
        JSON.stringify(input.players),
        input.logTurn ?? 0,
      ),
    )
    return this.hold(matchId, handle)
  }

  /**
   * Folds one event of the match's log, as the server stores it, in log order. A `turn.sealed`
   * event comes with its sealed set as `GET /turns/:n/orders` answers it; the set is not read for a
   * seal the match already holds. Events that do not change the state are accepted and ignored.
   */
  applyEvent(matchId: string, event: unknown, sealedOrders?: unknown): MatchStatus {
    const handle = this.use(matchId)
    const sealed =
      sealedOrders === undefined || sealedOrders === null ? null : JSON.stringify(sealedOrders)
    this.refused(() => this.exports.ApplyEvent(handle, JSON.stringify(event), sealed))
    const status = this.statusOf(handle)
    this.fit(matchId)
    return status
  }

  /**
   * Folds a run of the log in one call, if the match is planning `fromTurn`.
   *
   * A feed is not idempotent: a handover fed twice is applied twice. Two callers that each read
   * where a match stands and then feed it the events after that point would otherwise both apply
   * them. The check and the feed run here, in one call the host never interleaves with another, so
   * the second caller finds the match on a later turn and is told nothing was applied.
   *
   * A feed that fails part way leaves a state that matches no point of the log, so the match is
   * released and the error rethrown; the caller rebuilds it.
   */
  applyEvents(matchId: string, fromTurn: number, steps: readonly FeedStep[]): FeedResult {
    const handle = this.use(matchId)
    const found = this.statusOf(handle)
    if (found.turn !== fromTurn) return { applied: false, status: found, seals: [] }
    const seals: ResolvedSeal[] = []
    try {
      for (const step of steps) {
        const turn = this.exports.Turn(handle)
        const finished = this.exports.IsFinished(handle)
        const sealed =
          step.sealedOrders === undefined || step.sealedOrders === null
            ? null
            : JSON.stringify(step.sealedOrders)
        this.refused(() => this.exports.ApplyEvent(handle, JSON.stringify(step.event), sealed))
        if (!finished && sealedTurnOf(step.event) === turn) {
          seals.push({
            turn,
            stateHash: this.exports.StateHash(handle),
            finished: this.exports.IsFinished(handle),
          })
        }
      }
    } catch (error: unknown) {
      this.release(matchId)
      throw error
    }
    const status = this.statusOf(handle)
    this.fit(matchId)
    return { applied: true, status, seals }
  }

  /** Where a held match stands, or `null` when it is not held. */
  status(matchId: string): MatchStatus | null {
    const handle = this.handles.get(matchId)
    return handle === undefined ? null : this.statusOf(handle)
  }

  /** The held match as a native save payload, with where it stands. */
  savePayload(matchId: string): { payload: Uint8Array; status: MatchStatus } {
    const handle = this.use(matchId)
    // Copied out: the interop view points into the WebAssembly memory, which the next call may move.
    const payload = this.refused(() => this.exports.SavePayload(handle)).slice()
    return { payload, status: this.statusOf(handle) }
  }

  /**
   * The seat in `slot`'s view of the held match as a native save payload, or `null` when the seat
   * has none (it has been eliminated, or the match has ended), with where the match stands: the
   * view is of the planning entry of `status.turn`.
   */
  seatViewPayload(
    matchId: string,
    slot: number,
  ): { payload: Uint8Array | null; status: MatchStatus } {
    const handle = this.use(matchId)
    // Copied out, as a save payload is.
    const payload = this.refused(() => this.exports.SeatView(handle, slot)).slice()
    return { payload: payload.length === 0 ? null : payload, status: this.statusOf(handle) }
  }

  /** Forgets a match. Releasing one that is not held does nothing. */
  release(matchId: string): void {
    const handle = this.handles.get(matchId)
    if (handle === undefined) return
    this.handles.delete(matchId)
    this.exports.Release(handle)
  }

  /** The ids held, least recently used first. */
  heldMatches(): string[] {
    return [...this.handles.keys()]
  }

  private hold(matchId: string, handle: number): MatchStatus {
    this.release(matchId)
    this.handles.set(matchId, handle)
    const status = this.statusOf(handle)
    this.fit(matchId)
    return status
  }

  /** The handle of a held match, which becomes the most recently used. */
  private use(matchId: string): number {
    const handle = this.handles.get(matchId)
    if (handle === undefined) throw new MatchNotHeldError(matchId)
    this.handles.delete(matchId)
    this.handles.set(matchId, handle)
    return handle
  }

  private statusOf(handle: number): MatchStatus {
    return {
      stateHash: this.exports.StateHash(handle),
      turn: this.exports.Turn(handle),
      finished: this.exports.IsFinished(handle),
    }
  }

  /** Releases the least recently used matches other than `keep` until the limits hold. */
  private fit(keep: string): void {
    const evictable = () => [...this.handles.keys()].find((id) => id !== keep)
    while (this.handles.size > this.limits.maxMatches) {
      const victim = evictable()
      if (victim === undefined) break
      this.release(victim)
    }
    // A collection is forced only once the cheap reading is over budget: garbage from the last few
    // turns would otherwise evict matches that fit.
    const budget = this.limits.managedHeapBudgetBytes
    if (this.exports.ManagedHeapBytes(false) <= budget) return
    while (this.exports.ManagedHeapBytes(true) > budget) {
      const victim = evictable()
      if (victim === undefined) break
      this.release(victim)
    }
  }

  /**
   * Runs a call into the runtime, turning a managed exception (a payload the build refuses) into
   * {@link ResolverRefusedError}. Anything else, such as the runtime itself failing, propagates as it
   * is.
   */
  private refused<T>(call: () => T): T {
    try {
      return call()
    } catch (error: unknown) {
      // The interop layer's class for a managed exception; it leaves `name` as "Error".
      if (error instanceof Error && error.constructor.name === 'ManagedError') {
        throw new ResolverRefusedError(error.message)
      }
      throw error
    }
  }
}

/** The turn a `turn.sealed` event seals, or undefined for any other event. */
function sealedTurnOf(event: unknown): number | undefined {
  if (typeof event !== 'object' || event === null) return undefined
  const { type, payload } = event as { type?: unknown; payload?: { turn?: unknown } }
  if (type !== 'turn.sealed' || typeof payload?.turn !== 'number') return undefined
  return payload.turn
}
