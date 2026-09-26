import {
  array,
  boolean,
  check,
  type InferOutput,
  integer,
  maxLength,
  maxValue,
  minValue,
  number,
  optional,
  picklist,
  pipe,
  strictObject,
} from 'valibot'
import { LIMITS } from './limits'
import { orderDocumentSchema } from './orders'
import {
  base64BodySchema,
  displayNameInputSchema,
  formatVersionSchema,
  joinCodeInputSchema,
  passwordSchema,
  portraitIdSchema,
  resourceIdSchema,
  stateFingerprintSchema,
  slotSchema,
  turnNumberSchema,
} from './primitives'
import { protocolVersionSchema, sessionVersionSchema } from './protocol'
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
  /** The face the host plays under. Omitted by clients that predate the choice; they get the first. */
  hostPortraitId: optional(portraitIdSchema),
  password: optional(passwordSchema),
  /** Wire protocol of the creating client. Omitted legacy requests predate version negotiation. */
  protocolVersion: optional(protocolVersionSchema),
  /** Session shape the creating client plays. Omitted legacy requests are session version 1. */
  sessionVersion: optional(sessionVersionSchema),
})

export const joinMatchRequestSchema = strictObject({
  joinCode: joinCodeInputSchema,
  displayName: displayNameInputSchema,
  /** The face this player plays under. Omitted by clients that predate the choice. */
  portraitId: optional(portraitIdSchema),
  password: optional(passwordSchema),
})

export const joinRunningMatchRequestSchema = strictObject({
  match: resourceIdSchema,
  displayName: displayNameInputSchema,
  /**
   * The face the seat already wears, which a latecomer inherits rather than chooses.
   *
   * The match was generated before this player existed, and every client generated that seat's
   * overlord from the host's `gameSettings` portraits. The seat's face is therefore already part of
   * a state every client has hashed, so a latecomer who brought their own would be handing a
   * different setup to any client that still bootstraps the match from the roster.
   */
  portraitId: optional(portraitIdSchema),
  password: optional(passwordSchema),
  slot: slotSchema,
})

/**
 * A lobby member's new name and face, sent as a whole so the two can never be half-applied.
 *
 * Only while the match is in the lobby: once it starts, the roster is what every client generates
 * its city from, and a name or face that moved afterwards would read as a desync.
 */
export const updatePlayerProfileRequestSchema = strictObject({
  displayName: displayNameInputSchema,
  portraitId: portraitIdSchema,
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

export const aiSeatSummarySchema = strictObject({
  slot: slotSchema,
  gangs: pipe(number(), integer(), minValue(0), maxValue(256)),
  sites: pipe(number(), integer(), minValue(0), maxValue(512)),
  sectors: pipe(number(), integer(), minValue(0), maxValue(64)),
})

export const turnReportRequestSchema = strictObject({
  /** Canonical state hash after the client applied the sealed turn. */
  stateHash: stateFingerprintSchema,
  /** The client observed a completed match after this turn. */
  finished: boolean(),
  /** Current public seat facts. Only the host's copy is published for late-join selection. */
  seatSummaries: optional(
    pipe(
      array(aiSeatSummarySchema),
      maxLength(LIMITS.maxPlayers),
      check(
        (summaries) => new Set(summaries.map((summary) => summary.slot)).size === summaries.length,
        'seatSummaries may not name a slot twice',
      ),
    ),
  ),
})

export const uploadSnapshotRequestSchema = strictObject({
  turn: turnNumberSchema,
  formatVersion: formatVersionSchema,
  /** Wire protocol that produced this snapshot. Omitted legacy requests are protocol version 1. */
  protocolVersion: optional(protocolVersionSchema),
  /** Session shape these bytes belong to. Omitted legacy requests are session version 1. */
  sessionVersion: optional(sessionVersionSchema),
  stateHash: stateFingerprintSchema,
  /** The client's native snapshot, base64-encoded. The server stores it without decoding it. */
  body: base64BodySchema,
  /**
   * Public, bounded facts that let a late joiner choose an AI seat without exposing the save.
   *
   * One entry per seat and no seat twice. Both bounds are load-bearing rather than tidy: the upload
   * merges this array into the match's `gameSettings` blob, which is served on every match read
   * and, for a public match, in every listing answer to every anonymous browser. Without a length
   * the host could write tens of thousands of entries inside the one-megabyte upload budget and
   * make that blob permanently large for everybody reading the lobby.
   *
   * Written inline rather than as a named schema so the C# generator keeps the field a plain list
   * instead of minting a type for it. `SnapshotService.upload` re-checks the merged blob against
   * the 8 KiB settings cap as well, so this is the cheap bound and that one is the real one.
   */
  seatSummaries: pipe(
    array(aiSeatSummarySchema),
    maxLength(LIMITS.maxPlayers),
    check(
      (summaries) => new Set(summaries.map((summary) => summary.slot)).size === summaries.length,
      'seatSummaries may not name a slot twice',
    ),
  ),
})

export type CreateMatchRequest = InferOutput<typeof createMatchRequestSchema>
export type JoinMatchRequest = InferOutput<typeof joinMatchRequestSchema>
export type JoinRunningMatchRequest = InferOutput<typeof joinRunningMatchRequestSchema>
export type UpdatePlayerProfileRequest = InferOutput<typeof updatePlayerProfileRequestSchema>
export type SubmitOrdersRequest = InferOutput<typeof submitOrdersRequestSchema>
export type TakeoverVoteRequest = InferOutput<typeof takeoverVoteRequestSchema>
export type TurnReportRequest = InferOutput<typeof turnReportRequestSchema>
export type UploadSnapshotRequest = InferOutput<typeof uploadSnapshotRequestSchema>
export type AiSeatSummary = InferOutput<typeof aiSeatSummarySchema>
