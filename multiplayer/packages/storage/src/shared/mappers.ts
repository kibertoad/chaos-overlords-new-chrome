import type {
  MatchSettings,
  MatchStatus,
  OrderDocument,
  PlayerStatus,
  TurnStatus,
} from '@chaos-overlords/contracts'
import type {
  Match,
  PersistedEvent,
  Player,
  Snapshot,
  Turn,
  TurnOrders,
  TurnReport,
} from '@chaos-overlords/kernel'

/**
 * Row shapes after Drizzle's own mapping, identical across the two dialects: Dates for timestamps,
 * parsed values for JSON, booleans for flags. Persisted enums are stored as text; each mapper
 * narrows them back with a cast, since the service layer is the only writer.
 */
export interface MatchRow {
  id: string
  status: string
  settings: unknown
  hostPlayerId: string
  joinCode: string
  passwordHash: string | null
  seed: number | null
  currentTurn: number
  seatCount: number
  eventSeq: number
  createdAt: Date
  updatedAt: Date
}

export interface PlayerRow {
  id: string
  matchId: string
  slot: number
  displayName: string
  tokenHash: string
  status: string
  joinedAt: Date
}

export interface TurnRow {
  matchId: string
  number: number
  status: string
  openedAt: Date
  deadlineAt: Date | null
  sealedAt: Date | null
  orderSetHash: string | null
}

export interface TurnOrdersRow {
  matchId: string
  turn: number
  playerId: string
  orders: unknown
  ordersHash: string | null
  ready: boolean
  submittedAt: Date | null
}

export interface TurnReportRow {
  matchId: string
  turn: number
  playerId: string
  stateHash: string
  finished: boolean
  reportedAt: Date
}

export interface SnapshotRow {
  matchId: string
  turn: number
  formatVersion: number
  stateHash: string
  uploadedByPlayerId: string
  uploadedAt: Date
  body: string
}

export interface EventRow {
  matchId: string
  seq: number
  type: string
  payload: unknown
  createdAt: Date
}

export const toMatch = (row: MatchRow): Match => ({
  id: row.id,
  status: row.status as MatchStatus,
  settings: row.settings as MatchSettings,
  hostPlayerId: row.hostPlayerId,
  joinCode: row.joinCode,
  passwordHash: row.passwordHash,
  seed: row.seed,
  currentTurn: row.currentTurn,
  seatCount: row.seatCount,
  eventSeq: row.eventSeq,
  createdAt: row.createdAt,
  updatedAt: row.updatedAt,
})

export const toMatchInsert = (match: Match) => ({
  id: match.id,
  status: match.status,
  name: match.settings.name,
  visibility: match.settings.visibility,
  maxPlayers: match.settings.maxPlayers,
  settings: match.settings,
  hostPlayerId: match.hostPlayerId,
  joinCode: match.joinCode,
  passwordHash: match.passwordHash,
  seed: match.seed,
  currentTurn: match.currentTurn,
  seatCount: match.seatCount,
  eventSeq: match.eventSeq,
  createdAt: match.createdAt,
  updatedAt: match.updatedAt,
})

export const toPlayer = (row: PlayerRow): Player => ({
  ...row,
  status: row.status as PlayerStatus,
})

export const toTurn = (row: TurnRow): Turn => ({ ...row, status: row.status as TurnStatus })

export const toTurnOrders = (row: TurnOrdersRow): TurnOrders => ({
  ...row,
  orders: (row.orders ?? null) as OrderDocument | null,
})

export const toTurnReport = (row: TurnReportRow): TurnReport => ({ ...row })

export const toSnapshot = (row: SnapshotRow): Snapshot => ({ ...row })

export const toEvent = (row: EventRow): PersistedEvent =>
  ({
    matchId: row.matchId,
    seq: row.seq,
    type: row.type,
    payload: row.payload,
    createdAt: row.createdAt.toISOString(),
  }) as PersistedEvent

export const toEventInsert = (event: PersistedEvent) => ({
  matchId: event.matchId,
  seq: event.seq,
  type: event.type,
  payload: event.payload,
  createdAt: new Date(event.createdAt),
})

export const firstOrNull = <T>(rows: T[]): T | null => rows[0] ?? null
