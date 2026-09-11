import {
  array,
  boolean,
  type InferOutput,
  integer,
  literal,
  maxLength,
  maxValue,
  minValue,
  nullable,
  number,
  pipe,
  strictObject,
  variant,
} from 'valibot'
import { LIMITS } from './limits'
import { notNegativeZero, slotSchema } from './primitives'

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
 *
 * @example
 * ```ts
 * import { parse } from 'valibot'
 * import { orderDocumentSchema } from '@chaos-overlords/contracts'
 *
 * parse(orderDocumentSchema, {
 *   schemaVersion: 1,
 *   ops: [{ op: 'cancelCommand', player: 0, gang: 12 }],
 * })
 * ```
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

/**
 * The bounded ids, spelled out rather than derived from {@link GAME_BOUNDS}.
 *
 * The C# generator reads this source without running it, so `maxValue(GAME_BOUNDS.sectorCount - 1)`
 * is a bound it cannot see, and a bound it cannot see is a `long` where the client wants an `int`.
 * `orders.spec.ts` holds the two in step.
 */
export const gangIdSchema = pipe(
  number(),
  integer(),
  minValue(0),
  maxValue(2_147_483_647),
  notNegativeZero,
)
export const sectorIdSchema = pipe(number(), integer(), minValue(0), maxValue(63), notNegativeZero)
export const siteIdSchema = pipe(number(), integer(), minValue(0), maxValue(191), notNegativeZero)
export const itemIdSchema = pipe(number(), integer(), minValue(0), maxValue(63), notNegativeZero)
export const gangActionSchema = pipe(
  number(),
  integer(),
  minValue(0),
  maxValue(14),
  notNegativeZero,
)
export const gangDefinitionIdSchema = pipe(
  number(),
  integer(),
  minValue(-32_768),
  maxValue(32_767),
  notNegativeZero,
)

/**
 * `CommandTargetKind`. Each kind bounds its own id; `none` carries no id.
 *
 * Every member is named rather than inlined so the generated C# derives `NoneTarget`,
 * `GangTarget`, `SectorTarget` and friends, instead of `None`, `Gang` and `Sector` — bare nouns
 * that would collide with the game's own vocabulary in a shared namespace.
 */
export const noneTargetSchema = strictObject({ kind: literal('none') })
export const gangTargetSchema = strictObject({ kind: literal('gang'), id: gangIdSchema })
export const sectorTargetSchema = strictObject({ kind: literal('sector'), id: sectorIdSchema })
export const siteTargetSchema = strictObject({ kind: literal('site'), id: siteIdSchema })
export const itemTargetSchema = strictObject({ kind: literal('item'), id: itemIdSchema })

export const commandTargetSchema = variant('kind', [
  noneTargetSchema,
  gangTargetSchema,
  sectorTargetSchema,
  siteTargetSchema,
  itemTargetSchema,
])

/**
 * The five player intents. `player` is on every one of them because the game core takes it on
 * every one of them; the server checks it against the submitter's own slot rather than trusting
 * it (see `assertOwnOps` in the kernel's turn service).
 */

/** `MatchState.Submit(GameCommand)`. */
export const submitCommandOpSchema = strictObject({
  op: literal('submitCommand'),
  player: slotSchema,
  gang: gangIdSchema,
  action: gangActionSchema,
  target: commandTargetSchema,
  repeat: boolean(),
  secondaryTarget: nullable(commandTargetSchema),
})

/** `MatchState.Cancel(player, gang)`. */
export const cancelCommandOpSchema = strictObject({
  op: literal('cancelCommand'),
  player: slotSchema,
  gang: gangIdSchema,
})

/** `MatchState.QueueHire(player, gangDefinitionId, sectorId)`. */
export const queueHireOpSchema = strictObject({
  op: literal('queueHire'),
  player: slotSchema,
  gangDefinitionId: gangDefinitionIdSchema,
  sectorId: sectorIdSchema,
})

/** `MatchState.SnubHireOffer(player, gangDefinitionId)`. */
export const snubHireOfferOpSchema = strictObject({
  op: literal('snubHireOffer'),
  player: slotSchema,
  gangDefinitionId: gangDefinitionIdSchema,
})

/** `MatchState.TryDismissNotification(player)`. */
export const dismissNotificationOpSchema = strictObject({
  op: literal('dismissNotification'),
  player: slotSchema,
})

export const orderOpSchema = variant('op', [
  submitCommandOpSchema,
  cancelCommandOpSchema,
  queueHireOpSchema,
  snubHireOfferOpSchema,
  dismissNotificationOpSchema,
])

export const orderDocumentSchema = strictObject({
  schemaVersion: literal(1),
  ops: pipe(array(orderOpSchema), maxLength(LIMITS.ordersMaxOps)),
})

export type CommandTarget = InferOutput<typeof commandTargetSchema>
export type OrderOp = InferOutput<typeof orderOpSchema>
export type OrderOpKind = OrderOp['op']
export type OrderDocument = InferOutput<typeof orderDocumentSchema>

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
