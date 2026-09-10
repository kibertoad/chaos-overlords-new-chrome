import { z } from 'zod'
import { LIMITS } from './limits'

/**
 * The order document: what one player intends to do in a turn, in the vocabulary the game core
 * actually records.
 *
 * ## What this schema is for, and what it cannot be
 *
 * The server does not simulate. It cannot answer "does this player own gang 12", "can they afford
 * this hire", "is that sector adjacent" — those are the rules, and the rules live in
 * `Rechaos.Core`. What the server CAN do, and does here, is refuse anything that is not a
 * *representable* operation of this game:
 *
 * - the op must be one of the five player intents the replay recorder accepts over a turn
 *   (`MatchReplayRecorder.Submit`/`Cancel`/`QueueHire`/`SnubHireOffer`/`TryDismissNotification`);
 *   the phase transitions and the `Prepare*` steps are driven by the turn structure on every
 *   client and are refused if a client tries to send one,
 * - every field must exist, have the right type, and fit the C# type and capacity it indexes
 *   (`MatchLimits`): a sector is 0..63, a site 0..191, an item 0..63, a player 0..5, a gang action
 *   0..14, a gang definition a signed 16-bit id,
 * - no unknown fields survive, so a doctored client cannot smuggle a payload past a client that
 *   reads more of the document than it should.
 *
 * The remaining legality judgement is every client's, made while applying the sealed set through the
 * same validator the replay reader uses, and disagreement surfaces as a desync. This schema is
 * what stops a malformed or out-of-range document from ever reaching that point: an op that could
 * only crash or diverge a peer is refused at the door, by the one party every client trusts.
 *
 * See `docs/MULTIPLAYER.md`, "What the server does and does not defend against".
 */

/** `MatchLimits` and the enum ranges of `Rechaos.Core`, mirrored for validation. */
export const GAME_BOUNDS = {
  /** `MatchLimits.PlayerCount`. */
  playerCount: 6,
  /** `MatchLimits.SectorCount` (an 8x8 board). */
  sectorCount: 64,
  /** `MatchLimits.SiteCount`. */
  siteCount: 192,
  /** `MatchLimits.ItemSlots`. */
  itemSlots: 64,
  /** `GangAction`, `None` through `Terminate`. */
  maxGangAction: 14,
  /**
   * `GangId` is allocated as `max + 1` over the whole match, so it grows with every hire and has
   * no capacity to check against. Only its C# type bounds it.
   */
  maxGangId: 2_147_483_647,
  /** `GangDefinitionId` is a C# `short`. */
  minGangDefinitionId: -32_768,
  maxGangDefinitionId: 32_767,
} as const

/** `CommandTargetKind`. Each kind bounds its own id; `none` carries no id. */
export const commandTargetSchema = z.discriminatedUnion('kind', [
  z.object({ kind: z.literal('none') }).strict(),
  z.object({ kind: z.literal('gang'), id: bounded(GAME_BOUNDS.maxGangId) }).strict(),
  z.object({ kind: z.literal('sector'), id: bounded(GAME_BOUNDS.sectorCount - 1) }).strict(),
  z.object({ kind: z.literal('site'), id: bounded(GAME_BOUNDS.siteCount - 1) }).strict(),
  z.object({ kind: z.literal('item'), id: bounded(GAME_BOUNDS.itemSlots - 1) }).strict(),
])

const playerId = bounded(GAME_BOUNDS.playerCount - 1)
const gangId = bounded(GAME_BOUNDS.maxGangId)
const sectorId = bounded(GAME_BOUNDS.sectorCount - 1)
const gangAction = bounded(GAME_BOUNDS.maxGangAction)
const gangDefinitionId = integerIn(GAME_BOUNDS.minGangDefinitionId, GAME_BOUNDS.maxGangDefinitionId)

/**
 * The five player intents. `player` is on every one of them because the game core takes it on
 * every one of them; the server checks it against the submitter's own slot rather than trusting
 * it (see `assertOwnOps` in the kernel's turn service).
 */
export const orderOpSchema = z.discriminatedUnion('op', [
  /** `MatchState.Submit(GameCommand)`. */
  z
    .object({
      op: z.literal('submitCommand'),
      player: playerId,
      gang: gangId,
      action: gangAction,
      target: commandTargetSchema,
      repeat: z.boolean(),
      secondaryTarget: commandTargetSchema.nullable(),
    })
    .strict(),
  /** `MatchState.Cancel(player, gang)`. */
  z.object({ op: z.literal('cancelCommand'), player: playerId, gang: gangId }).strict(),
  /** `MatchState.QueueHire(player, gangDefinitionId, sectorId)`. */
  z
    .object({
      op: z.literal('queueHire'),
      player: playerId,
      gangDefinitionId,
      sectorId,
    })
    .strict(),
  /** `MatchState.SnubHireOffer(player, gangDefinitionId)`. */
  z.object({ op: z.literal('snubHireOffer'), player: playerId, gangDefinitionId }).strict(),
  /** `MatchState.TryDismissNotification(player)`. */
  z.object({ op: z.literal('dismissNotification'), player: playerId }).strict(),
])

export const orderDocumentSchema = z
  .object({
    schemaVersion: z.literal(1),
    ops: z.array(orderOpSchema).max(LIMITS.ordersMaxOps),
  })
  .strict()

export type CommandTarget = z.infer<typeof commandTargetSchema>
export type OrderOp = z.infer<typeof orderOpSchema>
export type OrderOpKind = OrderOp['op']
export type OrderDocument = z.infer<typeof orderDocumentSchema>

/** Every op kind the wire accepts, for documentation and for exhaustiveness in clients. */
export const ORDER_OP_KINDS = [
  'submitCommand',
  'cancelCommand',
  'queueHire',
  'snubHireOffer',
  'dismissNotification',
] as const satisfies readonly OrderOpKind[]

/**
 * The ops of a document that do not belong to `slot`, if any.
 *
 * Orders are sealed under the slot they were submitted from and every client applies them
 * attributed to that slot, so an op naming a different player is either a client bug or an attempt
 * to act as somebody else. Refusing it at submission keeps the two attributions from ever
 * disagreeing, and means a client that (wrongly) trusts the `player` field cannot be steered by a
 * peer.
 */
export function foreignOps(document: OrderDocument, slot: number): OrderOp[] {
  return document.ops.filter((op) => op.player !== slot)
}

function bounded(max: number) {
  return integerIn(0, max)
}

/**
 * An integer in a closed range, with `-0` refused.
 *
 * `-0` passes every numeric comparison a range check makes, but it has no portable canonical JSON
 * form — and the order digest is taken over exactly that text. Letting it through here would turn a
 * doctored request into a canonicalisation failure deeper in, which is a 500 where a 422 is the
 * truth.
 */
function integerIn(min: number, max: number) {
  return z
    .number()
    .int()
    .min(min)
    .max(max)
    .refine((value) => !Object.is(value, -0), 'negative zero has no portable JSON form')
}
