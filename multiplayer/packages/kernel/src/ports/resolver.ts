import type { MatchEvent, SealedOrdersView } from '@chaos-overlords/contracts'

/** Where a match the resolver holds stands. */
export interface ResolverMatchStatus {
  stateHash: string
  /** The turn the match is planning: the next seal it applies must be for this turn. */
  turn: number
  finished: boolean
}

/** One event of a match's log as the resolver is fed it, with its sealed set when it is a seal. */
export interface ResolverFeedStep {
  event: MatchEvent
  sealedOrders?: SealedOrdersView
}

/** What a feed did; see {@link TurnResolver.applyEvents}. */
export interface ResolverFeedResult {
  /** False when the match was not planning the turn the feed was written for: nothing applied. */
  applied: boolean
  status: ResolverMatchStatus
  /** Every seal the feed applied, in log order, with the state it left. */
  seals: Array<{ turn: number; stateHash: string; finished: boolean }>
}

/** A snapshot as the server stores it: the archive clients upload and read, base64. */
export interface ResolverSnapshot {
  body: string
  stateHash: string
}

/**
 * The game's own turn resolver, which the server runs to referee online turns (docs/MULTIPLAYER.md,
 * "Resolving turns on the server"). `@chaos-overlords/resolver` implements it for Node and for
 * Cloudflare; a runtime that supplies none keeps deciding turns by the players' reports.
 *
 * A match lives in the resolver between calls under the match id, and the resolver may let go of
 * it at any time (eviction, a lost runtime). A call that names a match it does not hold rejects
 * with an error whose `code` is `match_not_held`, and the server rebuilds the match from its newest
 * checkpoint and the log after it.
 */
export interface TurnResolver {
  describe(): Promise<{ sessionVersion: number; snapshotFormatVersion: number }>
  /** Builds a match from the facts of `match.started`, replacing any held under the id. */
  bootstrap(
    matchId: string,
    input: { seed: number; gameSettings: unknown; players: unknown },
  ): Promise<ResolverMatchStatus>
  /** Picks a match up from a stored snapshot, refused unless it hashes to `stateHash`. */
  restore(
    matchId: string,
    snapshot: ResolverSnapshot,
    input: { players: unknown; logTurn?: number },
  ): Promise<ResolverMatchStatus>
  /**
   * Folds a run of the log in one call if the match is planning `fromTurn`, and applies nothing
   * otherwise. A feed that fails part way releases the match.
   */
  applyEvents(
    matchId: string,
    fromTurn: number,
    steps: readonly ResolverFeedStep[],
  ): Promise<ResolverFeedResult>
  status(matchId: string): Promise<ResolverMatchStatus | null>
  snapshot(matchId: string): Promise<ResolverSnapshot & { status: ResolverMatchStatus }>
  release(matchId: string): Promise<void>
}

/** Whether a resolver call failed because the resolver no longer holds the match. */
export function isMatchNotHeld(error: unknown): boolean {
  return (
    typeof error === 'object' &&
    error !== null &&
    (error as { code?: unknown }).code === 'match_not_held'
  )
}
