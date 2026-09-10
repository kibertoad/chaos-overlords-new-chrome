import type { OrderDocument } from './orders'
import type { MatchSettings } from './schemas'

export type MatchStatus = 'lobby' | 'running' | 'desynced' | 'finished' | 'abandoned'
export type PlayerStatus = 'active' | 'left' | 'kicked'
export type TurnStatus = 'open' | 'sealed' | 'confirmed' | 'desynced'

export interface PlayerView {
  id: string
  slot: number
  displayName: string
  status: PlayerStatus
  isHost: boolean
}

export interface TurnView {
  number: number
  status: TurnStatus
  openedAt: string
  /** ISO timestamp the server seals the turn regardless of readiness; null without a timer. */
  deadlineAt: string | null
  sealedAt: string | null
  orderSetHash: string | null
  /** Players (by id) who marked themselves ready for this turn. */
  readyPlayerIds: string[]
  /** Players (by id) whose post-turn state report the server has. */
  reportedPlayerIds: string[]
}

export interface MatchView {
  id: string
  status: MatchStatus
  settings: MatchSettings
  hostPlayerId: string
  /** Set at start; every client seeds its deterministic core from it. */
  seed: number | null
  currentTurn: number
  players: PlayerView[]
  /** The open turn once running. */
  turn: TurnView | null
  /** The turn before it, where post-resolution reports and any desync verdict live. */
  previousTurn: TurnView | null
  /** The highest event sequence number persisted for this match. */
  lastEventSeq: number
  createdAt: string
}

export interface LobbyListing {
  id: string
  name: string
  hostDisplayName: string
  playerCount: number
  maxPlayers: number
  passwordProtected: boolean
  createdAt: string
}

/** What the caller gets back on create/join: the match plus its own credentials. */
export interface MembershipView {
  match: MatchView
  player: PlayerView
  /** Bearer token for every later call. Shown once; the server stores only its hash. */
  token: string
  /** Only the host receives the join code on create; it is also shown to lobby members. */
  joinCode: string
}

export interface OwnSubmissionView {
  turn: number
  orders: OrderDocument | null
  ready: boolean
  ordersHash: string | null
}

export interface SealedPlayerOrders {
  playerId: string
  slot: number
  orders: OrderDocument
  ordersHash: string
}

export interface SealedOrdersView {
  turn: number
  orderSetHash: string
  /** Ordered by slot: the deterministic order every client applies the ops in. */
  players: SealedPlayerOrders[]
}

export interface SnapshotView {
  turn: number
  formatVersion: number
  stateHash: string
  uploadedByPlayerId: string
  uploadedAt: string
  body: string
}
