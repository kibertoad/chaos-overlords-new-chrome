import type {
  LobbyListing,
  MatchEventBody,
  MatchSettings,
  MatchStatus,
  TurnStatus,
} from '@chaos-overlords/contracts'
import type {
  Match,
  OrderSummary,
  PersistedEvent,
  Player,
  SealedSlot,
  Snapshot,
  SnapshotSummary,
  TakeoverDecision,
  TakeoverVote,
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
  /** Host-only lobby configuration; false after the match starts or below the occupied seat count. */
  updateSettings(matchId: string, settings: MatchSettings, updatedAt: Date): Promise<boolean>
  /** Host-published public seat facts, written while running without changing lobby policy. */
  updateRuntimeGameSettings(
    matchId: string,
    gameSettings: MatchSettings['gameSettings'],
    updatedAt: Date,
  ): Promise<boolean>
  /**
   * Deletes matches in one of `statuses` last touched before `before`, with everything they own
   * (players, turns, orders, reports, snapshots, events cascade). Returns how many went. Live
   * matches are never in scope: the caller passes only terminal or never-started statuses.
   */
  deleteInactive(statuses: readonly MatchStatus[], before: Date, limit: number): Promise<number>
  /**
   * Deletes running or desynced matches last touched before `before` that hold no active player,
   * with everything they own. Returns how many went.
   *
   * The living-dead case: everybody walked away from a running match, which is deliberately kept so
   * anyone can rejoin, and then nobody ever did. Nothing else collects one — a desync pause is not
   * abandonment and neither is a weekend — so on a public server this is the ordinary end of most
   * matches and it accumulated orders, events and up to five megabytes of snapshot each. The
   * caller's window for these is far longer than the one for a terminated match.
   */
  deleteAbandonedLive(before: Date, limit: number): Promise<number>
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
  /**
   * Insert the player only while their match is still in the lobby, in ONE statement. False when
   * the match has started (or is gone), which is what keeps a seat claimed a moment before the
   * host pressed start from becoming an unseated player in a running match.
   */
  create(player: Player): Promise<boolean>
  /** Inserts a deterministic-id late member after start; false if that seat was ever human. */
  createLate(player: Player): Promise<boolean>
  get(id: string): Promise<Player | null>
  /** Never matches a revoked membership, whose token hash is null. */
  getByTokenHash(tokenHash: string): Promise<Player | null>
  /** Ordered by slot, then join order, then id. */
  listByMatch(matchId: string): Promise<Player[]>
  setStatus(playerId: string, status: Player['status']): Promise<void>
  /** Compare-and-swap a player status; exactly one return/takeover race may win. */
  transitionStatus(
    playerId: string,
    from: readonly Player['status'][],
    status: Player['status'],
  ): Promise<boolean>
  /** Clears the token hash, so the player's bearer token stops authenticating immediately. */
  revokeToken(playerId: string): Promise<void>
  /**
   * Seat every player in ONE statement. A loop of updates could fail partway and leave a running
   * match with some players still unseated, after the status change that made the roster final has
   * already committed — and nothing downstream repairs seating.
   */
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
  /**
   * The readiness and digest of every row of a turn, WITHOUT the documents. The seal decision,
   * the digest of the sealed set and the match view need only these; loading a quarter-megabyte
   * document per player to read a flag is what this projection avoids.
   */
  listOrderSummaries(matchId: string, number: number): Promise<OrderSummary[]>
  transition(
    matchId: string,
    number: number,
    from: readonly TurnStatus[],
    patch: {
      status: TurnStatus
      sealedAt?: Date
      orderSetHash?: string
      sealedSlots?: readonly SealedSlot[]
      /** Written by the verdict that confirms a turn; see `Turn.stateHash`. */
      stateHash?: string
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
   * Live matches whose current turn is not open, so there is nothing for anyone to play: a seal
   * that died between marking the turn sealed and opening its successor, or a start that died
   * between running the match and opening turn 1 (no row at all). The repair sweep finishes both,
   * which is why a missing turn counts as stalled rather than being skipped.
   */
  listStalledSeals(limit: number): Promise<Array<Pick<Turn, 'matchId' | 'number'>>>
}

export interface SnapshotRepository {
  put(snapshot: Snapshot): Promise<void>
  get(matchId: string, turn: number): Promise<Snapshot | null>
  getLatest(matchId: string): Promise<Snapshot | null>
  /** The newest snapshot's metadata without its body, which is a megabyte a caller checking for existence never reads. */
  getLatestSummary(matchId: string): Promise<SnapshotSummary | null>
  /**
   * Keep only the `keep` newest turns' snapshots of a match, dropping the rest. Returns how many
   * went. Retention collects whole terminated matches; this bounds what a single LIVE match holds,
   * which is otherwise a megabyte per desynced turn with nothing to stop it.
   */
  prune(matchId: string, keep: number): Promise<number>
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

/**
 * Absence prompts and their votes, as durable state rather than a replay of the event log.
 *
 * A prompt is open from the moment a human seat goes quiet (a departure, a kick, a wholly missed
 * timed turn) until the seat returns or is voted to the computer. The open-turn clock is paused
 * while a match has any, and every vote is judged against the prompts on file, so both questions
 * are one indexed read instead of a scan of every event the match ever logged.
 */
export interface TakeoverRepository {
  /** Opens a prompt for the seat; false when one is already open, so a caller can announce only the first. */
  openPrompt(matchId: string, playerId: string, turn: number, openedAt: Date): Promise<boolean>
  /** Closes the prompt and discards its votes. A no-op when none is open. */
  closePrompt(matchId: string, playerId: string): Promise<void>
  hasOpenPrompts(matchId: string): Promise<boolean>
  /** Player ids with an open prompt, in a stable order. */
  listOpenPrompts(matchId: string): Promise<string[]>
  /**
   * Records or replaces one voter's choice on an open prompt, in ONE statement conditional on the
   * prompt being open. False when it is not, so a vote can never outlive the prompt it answers.
   */
  castVote(
    matchId: string,
    targetPlayerId: string,
    voterPlayerId: string,
    decision: TakeoverDecision,
    castAt: Date,
  ): Promise<boolean>
  listVotes(matchId: string, targetPlayerId: string): Promise<TakeoverVote[]>
}

export interface MultiplayerStorage {
  matches: MatchRepository
  players: PlayerRepository
  turns: TurnRepository
  takeovers: TakeoverRepository
  snapshots: SnapshotRepository
  events: EventRepository
}
