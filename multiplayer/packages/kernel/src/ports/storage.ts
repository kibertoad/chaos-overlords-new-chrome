import type {
  LobbyListing,
  MatchEventBody,
  MatchStatus,
  TurnStatus,
} from '@chaos-overlords/contracts'
import type {
  Match,
  PersistedEvent,
  Player,
  SealedSlot,
  Snapshot,
  Turn,
  TurnOrders,
  TurnReport,
} from '../domain/entities'

/**
 * Persistence ports. There are NO transactions: D1 has none, so every invariant that two writers
 * could race on is a single conditional statement whose row count says who won. Each `transition`
 * is such a compare-and-swap; `claimSeat`/`releaseSeat` are atomic counters; `submitOrders` is
 * conditional on the turn still being open; `append` allocates its own sequence number.
 *
 * Writes that a unique constraint can refuse answer `false` instead of throwing, so the services
 * can retry (a join code collision) without knowing anything about a driver's error shapes.
 */
export interface MatchRepository {
  /** False when the id or the join code is already taken; the caller retries with a fresh code. */
  create(match: Match): Promise<boolean>
  get(id: string): Promise<Match | null>
  getByJoinCode(joinCode: string): Promise<Match | null>
  listPublicLobbies(limit: number): Promise<LobbyListing[]>
  /**
   * Atomically take a seat while the match is in the lobby and below capacity. Returns the seat's
   * position in the match's monotonic join sequence, or null when no seat was available.
   */
  claimSeat(matchId: string): Promise<number | null>
  releaseSeat(matchId: string): Promise<void>
  /**
   * Deletes matches in one of `statuses` last touched before `before`, with everything they own
   * (players, turns, orders, reports, snapshots, events cascade). Returns how many went. Live
   * matches are never in scope: the caller passes only terminal or never-started statuses.
   */
  deleteInactive(statuses: readonly MatchStatus[], before: Date, limit: number): Promise<number>
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
}

export interface PlayerRepository {
  create(player: Player): Promise<void>
  get(id: string): Promise<Player | null>
  /** Never matches a revoked membership, whose token hash is null. */
  getByTokenHash(tokenHash: string): Promise<Player | null>
  /** Ordered by slot, then join order, then id. */
  listByMatch(matchId: string): Promise<Player[]>
  setStatus(playerId: string, status: Player['status']): Promise<void>
  /** Clears the token hash, so the player's bearer token stops authenticating immediately. */
  revokeToken(playerId: string): Promise<void>
  assignSlots(assignments: ReadonlyArray<{ playerId: string; slot: number }>): Promise<void>
  delete(playerId: string): Promise<void>
}

export interface TurnRepository {
  /**
   * Insert the turn and one empty orders row per player. False when the turn already exists, which
   * is how the seal repair can re-run the open step without publishing a second `turn.opened`.
   */
  open(turn: Turn, playerIds: readonly string[]): Promise<boolean>
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
    patch: {
      status: TurnStatus
      sealedAt?: Date
      orderSetHash?: string
      sealedSlots?: readonly SealedSlot[]
    },
  ): Promise<boolean>
  /** Move an open turn's deadline, e.g. when a match resumes after a desync pause. */
  rescheduleDeadline(matchId: string, number: number, deadlineAt: Date | null): Promise<boolean>
  upsertReport(report: TurnReport): Promise<void>
  listReports(matchId: string, number: number): Promise<TurnReport[]>
  /** Turns of a match still awaiting a verdict: status `sealed` or `desynced`, ascending. */
  listUnsettled(matchId: string): Promise<Turn[]>
  /** Open turns whose deadline has passed, oldest first. */
  listExpiredOpen(now: Date, limit: number): Promise<Array<Pick<Turn, 'matchId' | 'number'>>>
  /**
   * Current turns of live matches that are no longer open: a seal that died between marking the
   * turn sealed and opening its successor, which leaves the match with nothing to play. The repair
   * sweep finishes them.
   */
  listStalledSeals(limit: number): Promise<Array<Pick<Turn, 'matchId' | 'number'>>>
}

export interface SnapshotRepository {
  put(snapshot: Snapshot): Promise<void>
  get(matchId: string, turn: number): Promise<Snapshot | null>
  getLatest(matchId: string): Promise<Snapshot | null>
}

export interface EventRepository {
  /**
   * Append one event, allocating its sequence number inside the insert as `max(seq) + 1` for the
   * match. The sequence is therefore gapless and a committed `seq` implies every lower one is
   * committed too, which is what lets a stream cursor move forward and never skip an event. Two
   * concurrent appends race on the primary key; the loser retries. Returns the persisted event.
   */
  append(event: MatchEventBody & { matchId: string; createdAt: string }): Promise<PersistedEvent>
  listAfter(matchId: string, afterSeq: number, limit: number): Promise<PersistedEvent[]>
  /** Highest sequence number persisted for the match, or 0 when the log is empty. */
  lastSeq(matchId: string): Promise<number>
}

export interface MultiplayerStorage {
  matches: MatchRepository
  players: PlayerRepository
  turns: TurnRepository
  snapshots: SnapshotRepository
  events: EventRepository
}
