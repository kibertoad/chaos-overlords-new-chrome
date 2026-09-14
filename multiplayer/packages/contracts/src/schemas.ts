import {
  array,
  boolean,
  type InferOutput,
  integer,
  maxValue,
  minValue,
  number,
  optional,
  picklist,
  pipe,
  strictObject,
} from 'valibot'
import { orderDocumentSchema } from './orders'
import {
  base64BodySchema,
  displayNameInputSchema,
  formatVersionSchema,
  joinCodeInputSchema,
  passwordSchema,
  resourceIdSchema,
  sha256HexSchema,
  slotSchema,
  turnNumberSchema,
} from './primitives'
import { matchSettingsSchema } from './settings'

/**
 * Request bodies, path params and query strings. Every one is strict: an unknown field is refused
 * rather than dropped, so a doctored client cannot smuggle a payload past a peer that reads more
 * of the document than it should.
 *
 * @example
 * ```ts
 * import { parse } from 'valibot'
 * import { joinMatchRequestSchema } from '@chaos-overlords/contracts'
 *
 * parse(joinMatchRequestSchema, { joinCode: ' abcd2345 ', displayName: 'Ada' })
 * // => { joinCode: 'ABCD2345', displayName: 'Ada' }
 * ```
 */

export const createMatchRequestSchema = strictObject({
  settings: matchSettingsSchema,
  hostDisplayName: displayNameInputSchema,
  password: optional(passwordSchema),
})

export const joinMatchRequestSchema = strictObject({
  joinCode: joinCodeInputSchema,
  displayName: displayNameInputSchema,
  password: optional(passwordSchema),
})

export const joinRunningMatchRequestSchema = strictObject({
  match: resourceIdSchema,
  displayName: displayNameInputSchema,
  password: optional(passwordSchema),
  slot: slotSchema,
})

export const submitOrdersRequestSchema = strictObject({
  orders: orderDocumentSchema,
  /** `true` = the player has finished planning; the turn seals once every human is ready. */
  ready: boolean(),
})

export const takeoverVoteRequestSchema = strictObject({
  /** `computer` approves AI control; `wait` keeps the returning player's human seat intact. */
  decision: picklist(['computer', 'wait']),
})

export const turnReportRequestSchema = strictObject({
  /** Canonical state hash after the client applied the sealed turn. */
  stateHash: sha256HexSchema,
  /** The client observed a completed match after this turn. */
  finished: boolean(),
})

export const aiSeatSummarySchema = strictObject({
  slot: slotSchema,
  gangs: pipe(number(), integer(), minValue(0), maxValue(256)),
  sites: pipe(number(), integer(), minValue(0), maxValue(512)),
  sectors: pipe(number(), integer(), minValue(0), maxValue(64)),
})

export const uploadSnapshotRequestSchema = strictObject({
  turn: turnNumberSchema,
  formatVersion: formatVersionSchema,
  stateHash: sha256HexSchema,
  /** The client's native snapshot, base64-encoded. The server stores it without decoding it. */
  body: base64BodySchema,
  /** Public, bounded facts that let a late joiner choose an AI seat without exposing the save. */
  seatSummaries: array(aiSeatSummarySchema),
})

export type CreateMatchRequest = InferOutput<typeof createMatchRequestSchema>
export type JoinMatchRequest = InferOutput<typeof joinMatchRequestSchema>
export type JoinRunningMatchRequest = InferOutput<typeof joinRunningMatchRequestSchema>
export type SubmitOrdersRequest = InferOutput<typeof submitOrdersRequestSchema>
export type TakeoverVoteRequest = InferOutput<typeof takeoverVoteRequestSchema>
export type TurnReportRequest = InferOutput<typeof turnReportRequestSchema>
export type UploadSnapshotRequest = InferOutput<typeof uploadSnapshotRequestSchema>
export type AiSeatSummary = InferOutput<typeof aiSeatSummarySchema>
