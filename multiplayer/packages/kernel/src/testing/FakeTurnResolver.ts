import type { MatchEvent, SealedOrdersView } from '@chaos-overlords/contracts'
import type {
  ResolverFeedResult,
  ResolverFeedStep,
  ResolverMatchStatus,
  ResolverSnapshot,
  TurnResolver,
} from '../ports/resolver'

/** A 32-digit lowercase hex digest of `text`: FNV-1a, four lanes. Not cryptographic. */
export function fakeHash(text: string): string {
  let out = ''
  for (let lane = 0; lane < 4; lane++) {
    let hash = 0x811c9dc5 ^ lane
    for (let i = 0; i < text.length; i++) {
      hash ^= text.charCodeAt(i)
      hash = Math.imul(hash, 0x01000193) >>> 0
    }
    out += hash.toString(16).padStart(8, '0')
  }
  return out
}

/** The state a {@link FakeTurnResolver} starts a match in. */
export const fakeStartHash = (seed: number): string => fakeHash(`start:${seed}`)

/** The state a {@link FakeTurnResolver} reaches by sealing `orderSetHash` on `previous`. */
export const fakeSealedHash = (previous: string, orderSetHash: string): string =>
  fakeHash(`${previous}|seal:${orderSetHash}`)

/** The state a {@link FakeTurnResolver} reaches by handing a seat over on `previous`. */
export const fakeHandoverHash = (previous: string, event: string, playerId: string): string =>
  fakeHash(`${previous}|${event}:${playerId}`)

interface FakeMatch {
  turn: number
  stateHash: string
  finished: boolean
}

class Refused extends Error {
  readonly code = 'resolver_refused'
}

class NotHeld extends Error {
  readonly code = 'match_not_held'
}

/**
 * A {@link TurnResolver} with the game's rules replaced by a digest chain, for tests of the server's
 * side of refereeing. A seal moves the state on by hashing the sealed set's digest into it, and a
 * handover by hashing the event in, so feeding an event twice or out of order shows in the hash.
 * Seals below the match's turn are passed over and seals above it refused, as the real fold does.
 */
export class FakeTurnResolver implements TurnResolver {
  readonly matches = new Map<string, FakeMatch>()
  /** Every call made, by method name. */
  readonly calls: string[] = []
  /** When set, every call rejects with it until cleared. */
  failure: Error | null = null

  constructor(
    readonly options: {
      sessionVersion?: number
      /** The turn whose seal ends the match. */
      finishAfterTurn?: number
    } = {},
  ) {}

  async describe() {
    this.enter('describe')
    return { sessionVersion: this.options.sessionVersion ?? 1, snapshotFormatVersion: 1 }
  }

  async bootstrap(matchId: string, input: { seed: number }): Promise<ResolverMatchStatus> {
    this.enter('bootstrap')
    const match = { turn: 1, stateHash: fakeStartHash(input.seed), finished: false }
    this.matches.set(matchId, match)
    return { ...match }
  }

  async restore(matchId: string, snapshot: ResolverSnapshot): Promise<ResolverMatchStatus> {
    this.enter('restore')
    const match = JSON.parse(atob(snapshot.body)) as FakeMatch
    if (match.stateHash !== snapshot.stateHash) throw new Refused('the snapshot hashes elsewhere')
    this.matches.set(matchId, match)
    return { ...match }
  }

  async applyEvents(
    matchId: string,
    fromTurn: number,
    steps: readonly ResolverFeedStep[],
  ): Promise<ResolverFeedResult> {
    this.enter('applyEvents')
    const match = this.held(matchId)
    if (match.turn !== fromTurn) return { applied: false, status: { ...match }, seals: [] }
    const seals: ResolverFeedResult['seals'] = []
    try {
      for (const step of steps) {
        const sealed = this.apply(match, step.event, step.sealedOrders)
        if (sealed !== null) seals.push(sealed)
      }
    } catch (error) {
      this.matches.delete(matchId)
      throw error
    }
    return { applied: true, status: { ...match }, seals }
  }

  async status(matchId: string): Promise<ResolverMatchStatus | null> {
    this.enter('status')
    const match = this.matches.get(matchId)
    return match ? { ...match } : null
  }

  async snapshot(matchId: string) {
    this.enter('snapshot')
    const match = this.held(matchId)
    return { body: btoa(JSON.stringify(match)), stateHash: match.stateHash, status: { ...match } }
  }

  async release(matchId: string): Promise<void> {
    this.enter('release')
    this.matches.delete(matchId)
  }

  /** How many calls of `method` were made. */
  count(method: string): number {
    return this.calls.filter((call) => call === method).length
  }

  private enter(method: string): void {
    this.calls.push(method)
    if (this.failure) throw this.failure
  }

  private held(matchId: string): FakeMatch {
    const match = this.matches.get(matchId)
    if (!match) throw new NotHeld(`resolver: match ${matchId} is not held here`)
    return match
  }

  private apply(
    match: FakeMatch,
    event: MatchEvent,
    sealedOrders: SealedOrdersView | undefined,
  ): { turn: number; stateHash: string; finished: boolean } | null {
    if (match.finished) return null
    switch (event.type) {
      case 'match.playerTakenOver':
      case 'match.latePlayerJoined':
        match.stateHash = fakeHandoverHash(match.stateHash, event.type, event.payload.playerId)
        return null
      case 'match.playerReturned':
        if (event.payload.replacedComputer) {
          match.stateHash = fakeHandoverHash(match.stateHash, event.type, event.payload.playerId)
        }
        return null
      case 'turn.sealed': {
        const turn = event.payload.turn
        if (turn < match.turn) return null
        if (turn > match.turn) throw new Refused(`a seal for turn ${turn} on turn ${match.turn}`)
        if (sealedOrders?.orderSetHash !== event.payload.orderSetHash) {
          throw new Refused(`the sealed set of turn ${turn} is not the one the log announced`)
        }
        match.stateHash = fakeSealedHash(match.stateHash, sealedOrders.orderSetHash)
        match.turn += 1
        match.finished = turn === this.options.finishAfterTurn
        return { turn, stateHash: match.stateHash, finished: match.finished }
      }
      default:
        return null
    }
  }
}
