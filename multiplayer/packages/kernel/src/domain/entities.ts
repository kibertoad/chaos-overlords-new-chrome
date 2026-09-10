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
  /** Last allocated event sequence number. */
  eventSeq: number
  createdAt: Date
  updatedAt: Date
}

export interface Player {
  id: string
  matchId: string
  /** Assigned at match start; -1 while in the lobby. */
  slot: number
  displayName: string
  tokenHash: string
  status: PlayerStatus
  joinedAt: Date
}

export interface Turn {
  matchId: string
  number: number
  status: TurnStatus
  openedAt: Date
  deadlineAt: Date | null
  sealedAt: Date | null
  orderSetHash: string | null
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
  stateHash: string
  uploadedByPlayerId: string
  uploadedAt: Date
  /** Base64 of the client's native snapshot bytes; the server never decodes it. */
  body: string
}

export type PersistedEvent = MatchEvent

export const ACTIVE_PLAYER: PlayerStatus = 'active'

export function activePlayers(players: readonly Player[]): Player[] {
  return players.filter((player) => player.status === ACTIVE_PLAYER)
}
