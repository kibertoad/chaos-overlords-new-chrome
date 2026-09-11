import { array, boolean, type InferOutput, nullable, picklist, strictObject } from 'valibot'
import { orderDocumentSchema } from './orders'
import {
  base64BodySchema,
  displayNameSchema,
  eventSeqSchema,
  formatVersionSchema,
  isoTimestampSchema,
  joinCodeSchema,
  matchNameSchema,
  playerCountSchema,
  resourceIdSchema,
  seatSchema,
  seedSchema,
  sha256HexSchema,
  slotSchema,
  tokenSchema,
  turnNumberSchema,
} from './primitives'
import { matchSettingsSchema } from './settings'

/**
 * What the server answers with. Every view is a schema rather than an interface so the C# client's
 * records are generated from the same source the server validates against, instead of being a
 * hand-kept mirror that drifts on the first rename.
 *
 * @example
 * ```ts
 * import { parse } from 'valibot'
 * import { matchViewSchema } from '@chaos-overlords/contracts'
 *
 * const view = parse(matchViewSchema, await response.json())
 * ```
 */

export const matchStatusSchema = picklist(['lobby', 'running', 'desynced', 'finished', 'abandoned'])
export const playerStatusSchema = picklist(['active', 'left', 'kicked'])
export const turnStatusSchema = picklist(['open', 'sealed', 'confirmed', 'desynced'])

export const playerViewSchema = strictObject({
  id: resourceIdSchema,
  /** -1 in the lobby; the seat the match start assigned once running. */
  slot: seatSchema,
  displayName: displayNameSchema,
  status: playerStatusSchema,
  isHost: boolean(),
})

export const turnViewSchema = strictObject({
  number: turnNumberSchema,
  status: turnStatusSchema,
  openedAt: isoTimestampSchema,
  /** ISO timestamp the server seals the turn regardless of readiness; null without a timer. */
  deadlineAt: nullable(isoTimestampSchema),
  sealedAt: nullable(isoTimestampSchema),
  orderSetHash: nullable(sha256HexSchema),
  /** Players (by id) who marked themselves ready for this turn. */
  readyPlayerIds: array(resourceIdSchema),
  /** Players (by id) whose post-turn state report the server has. */
  reportedPlayerIds: array(resourceIdSchema),
})

export const matchViewSchema = strictObject({
  id: resourceIdSchema,
  status: matchStatusSchema,
  settings: matchSettingsSchema,
  hostPlayerId: resourceIdSchema,
  /** Set at start; every client seeds its deterministic core from it. */
  seed: nullable(seedSchema),
  currentTurn: turnNumberSchema,
  players: array(playerViewSchema),
  /** The open turn once running. */
  turn: nullable(turnViewSchema),
  /** The turn before it, where post-resolution reports and any desync verdict live. */
  previousTurn: nullable(turnViewSchema),
  /** The highest event sequence number persisted for this match. */
  lastEventSeq: eventSeqSchema,
  createdAt: isoTimestampSchema,
})

export const lobbyListingSchema = strictObject({
  id: resourceIdSchema,
  name: matchNameSchema,
  hostDisplayName: displayNameSchema,
  playerCount: playerCountSchema,
  maxPlayers: playerCountSchema,
  passwordProtected: boolean(),
  createdAt: isoTimestampSchema,
})

/** What the caller gets back on create/join: the match plus its own credentials. */
export const membershipViewSchema = strictObject({
  match: matchViewSchema,
  player: playerViewSchema,
  /** Bearer token for every later call. Shown once; the server stores only its hash. */
  token: tokenSchema,
  /** Only the host receives the join code on create; it is also shown to lobby members. */
  joinCode: joinCodeSchema,
})

/** A member's own read of the match, with the join code it may share and its own player id. */
export const matchDetailSchema = strictObject({
  match: matchViewSchema,
  joinCode: joinCodeSchema,
  /** The caller's own player id. */
  you: resourceIdSchema,
})

export const ownSubmissionViewSchema = strictObject({
  turn: turnNumberSchema,
  orders: nullable(orderDocumentSchema),
  ready: boolean(),
  ordersHash: nullable(sha256HexSchema),
})

export const sealedPlayerOrdersSchema = strictObject({
  playerId: resourceIdSchema,
  slot: slotSchema,
  orders: orderDocumentSchema,
  ordersHash: sha256HexSchema,
})

export const sealedOrdersViewSchema = strictObject({
  turn: turnNumberSchema,
  orderSetHash: sha256HexSchema,
  /** Ordered by slot: the deterministic order every client applies the ops in. */
  players: array(sealedPlayerOrdersSchema),
})

export const snapshotViewSchema = strictObject({
  turn: turnNumberSchema,
  formatVersion: formatVersionSchema,
  stateHash: sha256HexSchema,
  uploadedByPlayerId: resourceIdSchema,
  uploadedAt: isoTimestampSchema,
  /** Base64 of the client's native snapshot bytes; the server never decodes it. */
  body: base64BodySchema,
})

export const lobbyListSchema = strictObject({ matches: array(lobbyListingSchema) })

export type MatchStatus = InferOutput<typeof matchStatusSchema>
export type PlayerStatus = InferOutput<typeof playerStatusSchema>
export type TurnStatus = InferOutput<typeof turnStatusSchema>
export type PlayerView = InferOutput<typeof playerViewSchema>
export type TurnView = InferOutput<typeof turnViewSchema>
export type MatchView = InferOutput<typeof matchViewSchema>
export type LobbyListing = InferOutput<typeof lobbyListingSchema>
export type LobbyList = InferOutput<typeof lobbyListSchema>
export type MembershipView = InferOutput<typeof membershipViewSchema>
export type MatchDetail = InferOutput<typeof matchDetailSchema>
export type OwnSubmissionView = InferOutput<typeof ownSubmissionViewSchema>
export type SealedPlayerOrders = InferOutput<typeof sealedPlayerOrdersSchema>
export type SealedOrdersView = InferOutput<typeof sealedOrdersViewSchema>
export type SnapshotView = InferOutput<typeof snapshotViewSchema>
