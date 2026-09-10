import type { LobbyListing, MatchStatus, TurnStatus } from '@chaos-overlords/contracts'
import type {
  Match,
  PersistedEvent,
  Player,
  Snapshot,
  Turn,
  TurnOrders,
  TurnReport,
} from '../domain/entities'

/**
 * Persistence ports. There are NO transactions: D1 has none, so every invariant that two writers
 * could race on is a single conditional statement whose row count says who won. Each `transition`
 * is such a compare-and-swap; `claimSeat`/`releaseSeat` and `allocateEventSeq` are atomic
 * counters; `submitOrders` is conditional on the turn still being open.
 */
export interface MatchRepository {
  create(match: Match): Promise<void>
  get(id: string): Promise<Match | null>
  getByJoinCode(joinCode: string): Promise<Match | null>
  listPublicLobbies(limit: number): Promise<LobbyListing[]>
  /** Atomically take a seat while the match is in the lobby and below capacity. */
  claimSeat(matchId: string): Promise<boolean>
  releaseSeat(matchId: string): Promise<void>
  /** Compare-and-swap on status; returns false when the match was not in one of `from`. */
  transition(
    matchId: string,
    from: readonly MatchStatus[],
    patch: {
      status?: MatchStatus
      seed?: number
      currentTurn?: number
      hostPlayerId?: string
      updatedAt: Date
    },
  ): Promise<boolean>
  allocateEventSeq(matchId: string): Promise<number>
}

export interface PlayerRepository {
  create(player: Player): Promise<void>
  get(id: string): Promise<Player | null>
  getByTokenHash(tokenHash: string): Promise<Player | null>
  /** Ordered by slot, then joinedAt, then id. */
  listByMatch(matchId: string): Promise<Player[]>
  setStatus(playerId: string, status: Player['status']): Promise<void>
  assignSlots(assignments: ReadonlyArray<{ playerId: string; slot: number }>): Promise<void>
  delete(playerId: string): Promise<void>
}

export interface TurnRepository {
  /** Insert the turn and one empty orders row per player, in one batch. */
  open(turn: Turn, playerIds: readonly string[]): Promise<void>
  get(matchId: string, number: number): Promise<Turn | null>
  /**
   * Overwrite a player's orders only while the turn is `open`, in ONE statement.
   * Returns false when the turn is no longer open or the player has no row.
   */
  submitOrders(
    matchId: string,
    number: number,
    playerId: string,
    submission: {
      orders: TurnOrders['orders']
      ordersHash: string | null
      ready: boolean
      submittedAt: Date
    },
  ): Promise<boolean>
  getOrders(matchId: string, number: number, playerId: string): Promise<TurnOrders | null>
  listOrders(matchId: string, number: number): Promise<TurnOrders[]>
  transition(
    matchId: string,
    number: number,
    from: readonly TurnStatus[],
    patch: { status: TurnStatus; sealedAt?: Date; orderSetHash?: string },
  ): Promise<boolean>
  upsertReport(report: TurnReport): Promise<void>
  listReports(matchId: string, number: number): Promise<TurnReport[]>
  /** Turns of a match still awaiting a verdict: status `sealed` or `desynced`, ascending. */
  listUnsettled(matchId: string): Promise<Turn[]>
  /** Open turns whose deadline has passed, oldest first. */
  listExpiredOpen(now: Date, limit: number): Promise<Array<Pick<Turn, 'matchId' | 'number'>>>
}

export interface SnapshotRepository {
  put(snapshot: Snapshot): Promise<void>
  get(matchId: string, turn: number): Promise<Snapshot | null>
  getLatest(matchId: string): Promise<Snapshot | null>
}

export interface EventRepository {
  append(event: PersistedEvent): Promise<void>
  listAfter(matchId: string, afterSeq: number, limit: number): Promise<PersistedEvent[]>
}

export interface MultiplayerStorage {
  matches: MatchRepository
  players: PlayerRepository
  turns: TurnRepository
  snapshots: SnapshotRepository
  events: EventRepository
}
