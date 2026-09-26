import type {
  LobbyListing,
  MatchSettings,
  MatchStatus,
  OrderDocument,
  PlayerStatus,
  TurnStatus,
} from '@chaos-overlords/contracts'
import type {
  Match,
  PersistedEvent,
  PublicLobbyRow,
  Player,
  SealedSlot,
  Snapshot,
  SnapshotSummary,
  TakeoverDecision,
  TakeoverVote,
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
  protocolVersion: number
  sessionVersion: number
  status: string
  settings: unknown
  hostPlayerId: string
  joinCode: string
  passwordHash: string | null
  seed: number | null
  currentTurn: number
  seatCount: number
  joinCounter: number
  createdAt: Date
  updatedAt: Date
}

export interface PlayerRow {
  id: string
  matchId: string
  slot: number
  joinOrder: number
  displayName: string
  portraitId: number
  tokenHash: string | null
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
  sealedSlots: unknown
  stateHash: string | null
  desyncedAt: Date | null
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
  protocolVersion: number
  sessionVersion: number
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
  protocolVersion: databaseVersion(row.protocolVersion, 'matches.protocol_version'),
  sessionVersion: databaseVersion(row.sessionVersion, 'matches.session_version'),
  status: row.status as MatchStatus,
  settings: row.settings as MatchSettings,
  hostPlayerId: row.hostPlayerId,
  joinCode: row.joinCode,
  passwordHash: row.passwordHash,
  seed: row.seed,
  currentTurn: row.currentTurn,
  seatCount: row.seatCount,
  joinCounter: row.joinCounter,
  createdAt: row.createdAt,
  updatedAt: row.updatedAt,
})

export const toMatchInsert = (match: Match) => ({
  id: match.id,
  protocolVersion: match.protocolVersion,
  sessionVersion: match.sessionVersion,
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
  joinCounter: match.joinCounter,
  createdAt: match.createdAt,
  updatedAt: match.updatedAt,
})

export const toPlayer = (row: PlayerRow): Player => ({
  ...row,
  status: row.status as PlayerStatus,
})

export const toTurn = (row: TurnRow): Turn => ({
  ...row,
  status: row.status as TurnStatus,
  sealedSlots: (row.sealedSlots ?? null) as readonly SealedSlot[] | null,
})

export const toTurnOrders = (row: TurnOrdersRow): TurnOrders => ({
  ...row,
  orders: (row.orders ?? null) as OrderDocument | null,
})

export const toTurnReport = (row: TurnReportRow): TurnReport => ({ ...row })

export const toSnapshot = (row: SnapshotRow): Snapshot => ({
  ...row,
  ...snapshotVersions(row),
})

export const toSnapshotSummary = (row: Omit<SnapshotRow, 'body'>): SnapshotSummary => ({
  ...row,
  ...snapshotVersions(row),
})

const snapshotVersions = (row: Omit<SnapshotRow, 'body'>) => ({
  protocolVersion: databaseVersion(row.protocolVersion, 'snapshots.protocol_version'),
  sessionVersion: databaseVersion(row.sessionVersion, 'snapshots.session_version'),
})

export interface TakeoverVoteRow {
  matchId: string
  targetPlayerId: string
  voterPlayerId: string
  decision: string
  castAt: Date
}

export const toTakeoverVote = (row: TakeoverVoteRow): TakeoverVote => ({
  ...row,
  decision: row.decision as TakeoverDecision,
})

export const toEvent = (row: EventRow): PersistedEvent =>
  ({
    matchId: row.matchId,
    seq: row.seq,
    type: row.type,
    payload: row.payload,
    createdAt: row.createdAt.toISOString(),
  }) as PersistedEvent

export const firstOrNull = <T>(rows: T[]): T | null => rows[0] ?? null

/**
 * The public listing row as the two listing statements select it, before the contract shape.
 *
 * `seatCount` and `humanCount` are both carried because they answer the question for different
 * statuses: a lobby advertises the seats it has taken, a running match the humans still in it —
 * the ones playing and the ones a takeover vote has not yet decided on.
 */
export interface PublicLobbyRowShape {
  id: string
  joinCode: string
  name: string
  hostDisplayName: string
  seatCount: number
  humanCount: unknown
  maxPlayers: number
  passwordHash: string | null
  status: string
  sessionVersion: number
  settings: unknown
  createdAt: Date
  hasSnapshot: unknown
}

export const toPublicLobbyRow = (row: PublicLobbyRowShape): PublicLobbyRow => ({
  id: row.id,
  joinCode: row.joinCode,
  name: row.name,
  hostDisplayName: row.hostDisplayName,
  playerCount: row.status === 'running' ? countColumn(row.humanCount) : row.seatCount,
  maxPlayers: row.maxPlayers,
  status: row.status as LobbyListing['status'],
  sessionVersion: databaseVersion(row.sessionVersion, 'matches.session_version'),
  settings: row.settings as LobbyListing['settings'],
  availableSlots: [],
  availableSeatSummaries: [],
  passwordProtected: row.passwordHash !== null,
  hasSnapshot: booleanColumn(row.hasSnapshot),
  createdAt: row.createdAt.toISOString(),
})

/**
 * A driver-agnostic read of an aggregate or predicate column. `count(*)` comes back as a number on
 * better-sqlite3 and D1 and as a decimal string on node-postgres, and `exists` is a boolean on
 * Postgres and 0/1 on SQLite; both are normalized here rather than at each call site.
 */
function countColumn(value: unknown): number {
  const number = typeof value === 'string' ? Number(value) : value
  return typeof number === 'number' && Number.isFinite(number) ? number : 0
}

function booleanColumn(value: unknown): boolean {
  if (typeof value === 'boolean') return value
  if (typeof value === 'number') return value !== 0
  return value === 't' || value === 'true' || value === '1'
}

/**
 * Normalizes a protocol or session version at the database boundary, before it can leak into a
 * JSON response.
 *
 * Drizzle's static row type describes the schema, but database drivers are still runtime inputs:
 * some expose numeric columns as decimal strings. The public contract deliberately does not accept
 * that representation, so adapters turn it into the one JavaScript number the valibot schema and
 * generated C# record agree on. Invalid or unsafe values fail here instead of reaching a client.
 */
function databaseVersion(value: unknown, column: string): number {
  const number =
    typeof value === 'number'
      ? value
      : typeof value === 'string' && /^(0|[1-9]\d*)$/.test(value)
        ? Number(value)
        : Number.NaN
  if (!Number.isInteger(number) || number < 0 || number > 2_147_483_647)
    throw new TypeError(`${column} is not a signed 32-bit version`)
  return number
}
