import type {
  MatchEventBody,
  MatchSettings,
  MatchStatus,
  TurnStatus,
} from '@chaos-overlords/contracts'
import type {
  Match,
  MatchSeat,
  OrderSummary,
  PersistedEvent,
  Player,
  PublicLobbyRow,
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
 * is such a compare-and-swap; `claimSeat`/`releaseSeat` and `claimLateJoinOrder` are atomic
 * counters; `submitOrders` is
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
  /**
   * The public lobby list: open lobbies and running matches anyone may look at, newest first.
   *
   * `playerCount` and `hasSnapshot` come out of the same statement rather than from a read per
   * listed match, and a running match with no human left in it is left out — it is unjoinable, so
   * advertising it only costs the reader a row and the server the two queries behind it.
   */
  listPublicLobbies(limit: number): Promise<PublicLobbyRow[]>
  /**
   * Atomically take a seat while the match is in the lobby and below capacity. Returns the seat's
   * position in the match's monotonic join sequence, or null when no seat was available.
   */
  claimSeat(matchId: string): Promise<number | null>
  /**
   * Atomically take the next position in the match's join sequence for a late joiner, while the
   * match is running; null when it is not running or no longer exists. It advances the same
   * `joinCounter` `claimSeat` does, in one conditional UPDATE — a separate sequence, or a read
   * followed by a write, would hand two joiners the same `joinOrder` — and it never returns a
   * position a stored player of the match already holds. It does not test capacity: `createLate`
   * does, and a position taken for a join that `createLate` then refuses stays unused.
   */
  claimLateJoinOrder(matchId: string): Promise<number | null>
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
   * Deletes running or desynced matches last touched before `before`, with everything they own.
   * Returns how many went.
   *
   * The living-dead case: everybody walked away from a running match, which is deliberately kept so
   * anyone can rejoin, and then nobody ever did. Nothing else collects one — a desync pause is not
   * abandonment and neither is a weekend — so on a public server this is the ordinary end of most
   * matches and it accumulated orders, events and up to five megabytes of snapshot each. The
   * caller's window for these is far longer than the one for a terminated match.
   *
   * `requireEmptyRoster` is the difference between the two windows the caller uses. A player stops
   * being `active` only through `leave`, `kick` or a missed deadline, and the game's default is an
   * untimed match, so two friends whose clients both died mid-match leave two `active` rows that
   * nothing ever clears: with the roster test alone their match is immortal. The longer window drops
   * the test, because `updated_at` is refreshed by every turn open, every status change and every
   * join or rejoin, and months of silence on all of them is not something a live match does.
   */
  deleteAbandonedLive(before: Date, limit: number, requireEmptyRoster: boolean): Promise<number>
  /**
   * Ids of matches sitting in `desynced`, oldest first.
   *
   * The sweep re-runs the verdict for these. A `settle` interrupted after its compare-and-swap
   * leaves the match desynced with nothing unsettled, which no request path revisits.
   *
   * `touchedSince` bounds that to the matches something has actually happened to. `updatedAt` moves
   * on every report and every snapshot upload, so a match that nothing touched since the previous
   * pass has no new evidence and its verdict cannot have changed; without the bound a public
   * server's parked desyncs — the documented way a match ends when a host never uploads — were
   * re-judged every fifteen seconds for the weeks retention keeps them. It also ends the
   * starvation of a page ordered by an `updatedAt` that never moves: pass null to sweep everything,
   * which is what a freshly started process does once to pick up whatever it missed.
   */
  listDesynced(limit: number, touchedSince: Date | null): Promise<string[]>
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
  /** Advance the current turn in one conditional write; a late sweep may never move it backwards. */
  advanceCurrentTurn(matchId: string, number: number, updatedAt: Date): Promise<boolean>
}

export interface PlayerRepository {
  /**
   * Insert the player only while their match is still in the lobby, in ONE statement. False when
   * the match has started (or is gone), which is what keeps a seat claimed a moment before the
   * host pressed start from becoming an unseated player in a running match.
   */
  create(player: Player): Promise<boolean>
  /**
   * Inserts a deterministic-id late member after start; false if that seat was ever human.
   *
   * `maxPlayers` is part of the same statement, because two late joiners taking two different free
   * slots each passed a capacity check the other invalidated and the match ended up over capacity,
   * with a roster the match view's seat schema then refused.
   */
  createLate(player: Player): Promise<boolean>
  get(id: string): Promise<Player | null>
  /** Never matches a revoked membership, whose token hash is null. */
  getByTokenHash(tokenHash: string): Promise<Player | null>
  /** Ordered by slot, then join order, then id. */
  listByMatch(matchId: string): Promise<Player[]>
  /**
   * Every seat held in any of `matchIds`, in ONE statement. The public listing needs the taken
   * slots of each match it returns and nothing else about the players; a `listByMatch` per listing
   * was a query per running match on an unauthenticated route.
   */
  listSeats(matchIds: readonly string[]): Promise<MatchSeat[]>
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
  /**
   * Deletes a lobby member. True when a row went.
   *
   * The result is what the caller releases the seat on. Two requests that both authenticated before
   * either deleted — a `leave` sent twice, or a host `kick` racing the target's own `leave` — each
   * used to decrement the seat counter for one row, so the lobby admitted more players than
   * `maxPlayers` and a seventh seat failed `seatSchema` in every match view.
   */
  delete(playerId: string): Promise<boolean>
}

export interface TurnRepository {
  /**
   * Insert the turn and one empty orders row per player that has none yet. Rows are only added while
   * the stored turn is still `open`: a top-up that races a seal must not land in the sealed turn.
   * False when the turn already exists, which is how the seal repair can re-run the open step
   * without publishing a second `turn.opened`.
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
  /**
   * One player's row for a turn WITHOUT the document.
   *
   * `submitOrders` reads the previous row on every submission to decide whether readiness changed
   * and whether a stale write is an identical retry, and both questions are answered by the hash
   * and the flag. The client sends a whole-document replacement per command the player queues, so
   * loading up to `LIMITS.ordersBytes` of JSON to compare a boolean was the hottest read the server
   * had.
   */
  getOrderSummary(matchId: string, number: number, playerId: string): Promise<OrderSummary | null>
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
  /**
   * Freeze a sealed turn's participant set and digest, in ONE statement conditional on them not
   * being frozen already. True only for the caller that wrote them.
   *
   * `transition` is conditional on the STATUS, and the status is `sealed` for the whole window
   * between the seal's compare-and-swap and the successor opening — a window the sweep deliberately
   * runs `completeSeal` inside. Two callers could therefore both compute a set, both write it, and
   * both announce it, and with a departure landing between their two computations the second
   * overwrote the first with a different digest. Every client then saw two `turn.sealed` events for
   * one turn, and the one that fetched the set afterwards failed its digest check and ended the
   * session. Whoever wins here is the only one that announces, so the set is immutable from the
   * compare-and-swap onwards, as the design promises.
   */
  freezeSeal(
    matchId: string,
    number: number,
    frozen: { orderSetHash: string; sealedSlots: readonly SealedSlot[] },
  ): Promise<boolean>
  /**
   * Claim the announcement of a turn's divergence, in ONE statement conditional on `desyncedAt`
   * being null. True only for the caller that stamped it, which is then the one that publishes
   * `turn.desynced`.
   *
   * The alternative was asking the event log whether the announcement was already there, which
   * pages every event the match ever logged — on every sweep, for every paused match.
   */
  claimDesyncAnnouncement(matchId: string, number: number, at: Date): Promise<boolean>
  /** Move an open turn's deadline, e.g. when a match resumes after a desync pause. */
  rescheduleDeadline(matchId: string, number: number, deadlineAt: Date | null): Promise<boolean>
  /**
   * Record or replace one player's report, in ONE statement conditional on the turn still awaiting
   * a verdict (`sealed` or `desynced`). False when it has been confirmed since, which is what keeps
   * the evidence a verdict was taken on immutable: the caller reads the status a statement earlier,
   * so a report can otherwise land behind the `settle` that raced it.
   */
  upsertReport(report: TurnReport): Promise<boolean>
  listReports(matchId: string, number: number): Promise<TurnReport[]>
  /** Turns of a match still awaiting a verdict: status `sealed` or `desynced`, ascending. */
  listUnsettled(matchId: string): Promise<Turn[]>
  /**
   * Open turns of RUNNING matches whose deadline has passed, oldest first.
   *
   * The match status is part of the query on purpose. A desynced match keeps its open turn's expired
   * deadline for as long as the pause lasts, `trySeal` refuses it on every pass, and the row stays at
   * the head of a list ordered by deadline. A hundred of them and the page holds nothing the sweep
   * could act on, which on Node is the only thing that seals a deadline whose timer a restart lost.
   */
  listExpiredOpen(now: Date, limit: number): Promise<Array<Pick<Turn, 'matchId' | 'number'>>>
  /**
   * Live matches whose current turn is not open, so there is nothing for anyone to play: a seal
   * that died between marking the turn sealed and opening its successor, or a start that died
   * between running the match and opening turn 1 (no row at all). The repair sweep finishes both,
   * which is why a missing turn counts as stalled rather than being skipped.
   *
   * `touchedSince` bounds the join to matches something happened to recently. A seal in flight is
   * seconds old, but abandoned matches are deliberately kept `running` for weeks, so the
   * unbounded join walked thousands of rows every fifteen seconds to find nothing. The caller runs
   * the bounded scan every tick and the unbounded one (null) on a much longer period, which is what
   * still finds a seal interrupted while the process was down.
   */
  listStalledSeals(
    limit: number,
    touchedSince: Date | null,
  ): Promise<Array<Pick<Turn, 'matchId' | 'number'>>>
}

export interface SnapshotRepository {
  put(snapshot: Snapshot): Promise<void>
  get(matchId: string, turn: number): Promise<Snapshot | null>
  /**
   * One turn's snapshot metadata without its body. The verdict needs only `stateHash`, and it is
   * re-run for every unsettled turn of every paused match on every sweep; `get` there loaded a
   * megabyte of base64 to read one field.
   */
  getSummary(matchId: string, turn: number): Promise<SnapshotSummary | null>
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

/** One voter's choice on an open takeover prompt. */
export interface CastVoteInput {
  matchId: string
  targetPlayerId: string
  voterPlayerId: string
  decision: TakeoverDecision
  castAt: Date
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
  /**
   * Opens a prompt only while the seat is a human absence (`ABSENT_HUMAN_STATUSES`) of that match;
   * false when one is already open or the seat is active, computer controlled or not in the match.
   */
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
  castVote(input: CastVoteInput): Promise<boolean>
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
