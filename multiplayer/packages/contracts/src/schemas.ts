import { boolean, type InferOutput, optional, strictObject } from 'valibot'
import { INT32_MAX, LIMITS } from './limits'
import { orderDocumentSchema } from './orders'
import {
  base64BodySchema,
  displayNameSchema,
  formatVersionSchema,
  integerQueryParam,
  joinCodeInputSchema,
  passwordSchema,
  sha256HexSchema,
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
  hostDisplayName: displayNameSchema,
  password: optional(passwordSchema),
})

export const joinMatchRequestSchema = strictObject({
  joinCode: joinCodeInputSchema,
  displayName: displayNameSchema,
  password: optional(passwordSchema),
})

export const submitOrdersRequestSchema = strictObject({
  orders: orderDocumentSchema,
  /** `true` = the player has finished planning; the turn seals once every human is ready. */
  ready: boolean(),
})

export const turnReportRequestSchema = strictObject({
  /** Canonical state hash after the client applied the sealed turn. */
  stateHash: sha256HexSchema,
  /** The client observed a completed match after this turn. */
  finished: boolean(),
})

export const uploadSnapshotRequestSchema = strictObject({
  turn: turnNumberSchema,
  formatVersion: formatVersionSchema,
  stateHash: sha256HexSchema,
  /** The client's native snapshot, base64-encoded. The server stores it without decoding it. */
  body: base64BodySchema,
})

export type CreateMatchRequest = InferOutput<typeof createMatchRequestSchema>
export type JoinMatchRequest = InferOutput<typeof joinMatchRequestSchema>
export type SubmitOrdersRequest = InferOutput<typeof submitOrdersRequestSchema>
export type TurnReportRequest = InferOutput<typeof turnReportRequestSchema>
export type UploadSnapshotRequest = InferOutput<typeof uploadSnapshotRequestSchema>
