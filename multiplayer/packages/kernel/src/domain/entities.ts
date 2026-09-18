import type {
  MatchEvent,
  MatchSettings,
  MatchStatus,
  OrderDocument,
  PlayerStatus,
  TurnStatus,
} from '@chaos-overlords/contracts'

export interface Match {
  id: string
  /** Wire protocol used by the client that created this session. */
  protocolVersion: number
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
   * Monotonic count of seats ever claimed. It never decreases, so the value handed to each player
   * as `joinOrder` is a stable total order even after someone leaves and a newcomer takes the seat.
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
  /** Wire protocol used by the client that serialized this snapshot. */
  protocolVersion: number
  stateHash: string
  uploadedByPlayerId: string
  uploadedAt: Date
  /** Base64 of the client's native snapshot bytes; the server never decodes it. */
  body: string
}

/** A snapshot row without its body. */
export type SnapshotSummary = Omit<Snapshot, 'body'>

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

/** Human-controlled seats, including an absent player the lobby elected to keep waiting for. */
export function humanParticipants(players: readonly Player[]): Player[] {
  return players.filter(
    (player) => player.status === 'active' || player.status === 'takeoverPending',
  )
}
