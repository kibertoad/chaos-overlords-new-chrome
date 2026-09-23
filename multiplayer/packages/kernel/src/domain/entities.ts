import type {
  LobbyListing,
  MatchEvent,
  MatchSettings,
  MatchStatus,
  OrderDocument,
  PlayerStatus,
  TurnStatus,
} from '@chaos-overlords/contracts'

export interface Match {
  id: string
  /** Wire protocol spoken by the client that created this session. */
  protocolVersion: number
  /**
   * Shape of the session as it is stored here.
   *
   * Separate from `protocolVersion` because the two answer different questions: the protocol says
   * whether a client and this server can talk, and this says whether a client can pick the match
   * up and keep playing it. A client that speaks a newer protocol resumes a match created under an
   * older one as long as this is the version it plays.
   */
  sessionVersion: number
  status: MatchStatus
  settings: MatchSettings
  hostPlayerId: string
  joinCode: string
  passwordHash: string | null
  seed: number | null
  /** 0 in the lobby; the open turn number once running. */
  currentTurn: number
  /** Seats taken in the lobby; capacity is enforced on this counter atomically. */
  seatCount: number
  /**
   * The next position in the match's join sequence: advanced by every lobby seat claimed and every
   * late-join position taken, including claims whose join then failed, so it is an upper bound on
   * the players ever seated rather than a count of them. It never decreases, so the value handed to
   * each player as `joinOrder` is a stable total order even after someone leaves and a newcomer
   * takes the seat; gaps in that order carry no meaning.
   */
  joinCounter: number
  createdAt: Date
  updatedAt: Date
}

export interface Player {
  id: string
  matchId: string
  /** Assigned at match start; -1 while in the lobby. */
  slot: number
  /** Position in the match's monotonic join sequence; the host is always 0. */
  joinOrder: number
  displayName: string
  /**
   * The overlord face this player chose when they created or joined the match.
   *
   * Set once and never changed: every client builds its city from the roster, and the setup a city
   * is generated from is part of the state hash the turn verdict is taken over, so a face that
   * moved after the match started would read as a desync on any client that bootstrapped before it.
   */
  portraitId: number
  /**
   * SHA-256 of the player's bearer token, or null once the membership is revoked (kicked
   * from a running match). A null hash matches no token, so revocation needs no extra check.
   */
  tokenHash: string | null
  status: PlayerStatus
  joinedAt: Date
}

/** One participant of a sealed turn: the slot its orders were hashed under. */
export interface SealedSlot {
  playerId: string
  slot: number
}

export interface Turn {
  matchId: string
  number: number
  status: TurnStatus
  openedAt: Date
  deadlineAt: Date | null
  sealedAt: Date | null
  orderSetHash: string | null
  /**
   * The participants whose orders the seal folded into `orderSetHash`, frozen at seal time and
   * never recomputed. Readers of the sealed set serve exactly these rows, so the set a client
   * fetches always re-hashes to the digest that was announced, no matter who leaves afterwards.
   */
  sealedSlots: readonly SealedSlot[] | null
  /**
   * The post-turn state hash the verdict confirmed on, written once by `TurnService.settle` and
   * null until then.
   *
   * It is the settled consensus of that turn in one field, retained independently of the reports
   * and their changing active-player context. Match reads and diagnostics can therefore name the
   * verdict without trying to reconstruct an old vote against today's roster.
   */
  stateHash: string | null
  /**
   * When the verdict announced this turn's divergence, or null while it has not.
   *
   * It is the fact beside the status that makes `turn.desynced` publish-once, exactly as
   * `orderSetHash` does for `turn.sealed`: the caller whose compare-and-swap stamps it is the one
   * that announces. Without it the verdict had to page the whole event log looking for its own
   * announcement, on every sweep, for as long as the match stayed paused.
   */
  desyncedAt: Date | null
}

/** One player's row for a turn. Rows are pre-created when the turn opens (see TurnRepository). */
export interface TurnOrders {
  matchId: string
  turn: number
  playerId: string
  orders: OrderDocument | null
  ordersHash: string | null
  ready: boolean
  submittedAt: Date | null
}

/** `TurnOrders` without the document: what a seal decision and a match view actually read. */
export type OrderSummary = Pick<
  TurnOrders,
  'matchId' | 'turn' | 'playerId' | 'ordersHash' | 'ready'
>

export interface TurnReport {
  matchId: string
  turn: number
  playerId: string
  stateHash: string
  finished: boolean
  reportedAt: Date
}

export interface Snapshot {
  matchId: string
  turn: number
  formatVersion: number
  /** Wire protocol spoken by the client that serialized this snapshot. */
  protocolVersion: number
  /** Session shape these bytes belong to; see `Match.sessionVersion`. */
  sessionVersion: number
  stateHash: string
  uploadedByPlayerId: string
  uploadedAt: Date
  /** Base64 of the client's native snapshot bytes; the server never decodes it. */
  body: string
}

/** A snapshot row without its body. */
export type SnapshotSummary = Omit<Snapshot, 'body'>

/**
 * A public listing row as the database produces it: the contract's listing plus the two facts the
 * lobby list used to go back per match for.
 *
 * `playerCount` is already the count the reader wants — seats taken for a lobby, humans still in
 * the match for a running one — and `hasSnapshot` answers "could a late joiner bootstrap here"
 * without a second read. The listing is unauthenticated and rate limited per address, and it used
 * to cost up to two extra queries for every running match it returned.
 */
export interface PublicLobbyRow extends LobbyListing {
  hasSnapshot: boolean
}

/** One seat a listed match holds, for the reserved-slot arithmetic of the public listing. */
export interface MatchSeat {
  matchId: string
  slot: number
}

export type TakeoverDecision = 'computer' | 'wait'

export interface TakeoverVote {
  matchId: string
  targetPlayerId: string
  voterPlayerId: string
  decision: TakeoverDecision
  castAt: Date
}

export type PersistedEvent = MatchEvent

export const ACTIVE_PLAYER: PlayerStatus = 'active'

export function activePlayers(players: readonly Player[]): Player[] {
  return players.filter((player) => player.status === ACTIVE_PLAYER)
}

/** A seat a human still holds: present, or absent and waited for while a takeover is voted on. */
export function isHumanParticipant(player: Pick<Player, 'status'>): boolean {
  return player.status === 'active' || player.status === 'takeoverPending'
}

/** Human-controlled seats, including an absent player the lobby elected to keep waiting for. */
export function humanParticipants(players: readonly Player[]): Player[] {
  return players.filter(isHumanParticipant)
}

/** A started match that has not ended. A desync pause counts: the match resumes from it. */
export function isInProgress(match: Pick<Match, 'status'>): boolean {
  return match.status === 'running' || match.status === 'desynced'
}
