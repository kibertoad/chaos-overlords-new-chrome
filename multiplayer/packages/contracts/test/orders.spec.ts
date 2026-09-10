import { parse, safeParse } from 'valibot'
import { describe, expect, it } from 'vitest'
import {
  foreignOps,
  GAME_BOUNDS,
  LIMITS,
  ORDER_OP_KINDS,
  type OrderDocument,
  orderDocumentSchema,
} from '../src'

const document = (ops: unknown[]) => ({ schemaVersion: 1, ops })
const accepts = (ops: unknown[]) => safeParse(orderDocumentSchema, document(ops)).success

const submitCommand = {
  op: 'submitCommand',
  player: 0,
  gang: 3,
  action: 10,
  target: { kind: 'sector', id: 12 },
  repeat: false,
  secondaryTarget: null,
}

describe('orderDocumentSchema', () => {
  it('accepts every op the game core records as a player intent', () => {
    expect(
      accepts([
        submitCommand,
        { op: 'cancelCommand', player: 1, gang: 7 },
        { op: 'queueHire', player: 2, gangDefinitionId: 44, sectorId: 63 },
        { op: 'snubHireOffer', player: 3, gangDefinitionId: -1 },
        { op: 'dismissNotification', player: 4 },
      ]),
    ).toBe(true)
  })

  it('covers exactly the documented op vocabulary', () => {
    const accepted = ORDER_OP_KINDS.filter((op) =>
      // Each kind needs its own required fields, so probe with a document known to be valid for it.
      accepts([
        op === 'submitCommand'
          ? submitCommand
          : op === 'cancelCommand'
            ? { op, player: 0, gang: 1 }
            : op === 'queueHire'
              ? { op, player: 0, gangDefinitionId: 1, sectorId: 1 }
              : op === 'snubHireOffer'
                ? { op, player: 0, gangDefinitionId: 1 }
                : { op, player: 0 },
      ]),
    )
    expect(accepted).toEqual([...ORDER_OP_KINDS])
  })

  /**
   * The phase transitions and the `Prepare*` steps are driven by the turn structure on every client
   * and are never anybody's intent to send. An op name the game does not have is refused outright,
   * which is the difference between this and a schema that took any identifier.
   */
  it('refuses engine-driven steps and invented op names', () => {
    for (const op of [
      'finishCommand',
      'finishUpkeep',
      'finishExecutionPhase',
      'prepareHireOffers',
      'prepareAiPlanning',
      'moveGang',
      'giveMeMoney',
    ]) {
      expect(accepts([{ op, player: 0 }])).toBe(false)
    }
  })

  it('refuses ids outside the capacity they index', () => {
    expect(accepts([{ ...submitCommand, player: GAME_BOUNDS.playerCount }])).toBe(false)
    expect(accepts([{ ...submitCommand, player: -1 }])).toBe(false)
    expect(accepts([{ ...submitCommand, action: GAME_BOUNDS.maxGangAction + 1 }])).toBe(false)
    expect(
      accepts([{ ...submitCommand, target: { kind: 'sector', id: GAME_BOUNDS.sectorCount } }]),
    ).toBe(false)
    expect(
      accepts([{ ...submitCommand, target: { kind: 'site', id: GAME_BOUNDS.siteCount - 1 } }]),
    ).toBe(true)
    expect(
      accepts([{ ...submitCommand, target: { kind: 'item', id: GAME_BOUNDS.itemSlots } }]),
    ).toBe(false)
    expect(accepts([{ ...submitCommand, target: { kind: 'elsewhere', id: 0 } }])).toBe(false)
    // `GangDefinitionId` is a C# short, so the wire has to refuse what it could not hold.
    expect(
      accepts([
        {
          op: 'queueHire',
          player: 0,
          gangDefinitionId: GAME_BOUNDS.maxGangDefinitionId,
          sectorId: 0,
        },
      ]),
    ).toBe(true)
    expect(
      accepts([
        {
          op: 'queueHire',
          player: 0,
          gangDefinitionId: GAME_BOUNDS.maxGangDefinitionId + 1,
          sectorId: 0,
        },
      ]),
    ).toBe(false)
  })

  /**
   * The digest clients verify is taken over canonical JSON, and only integers are written identically
   * by every language's JSON writer. A float accepted here would be a digest no C# client could
   * reproduce.
   */
  it('refuses numbers that would not survive another JSON writer', () => {
    for (const gang of [1.5, -0, 1e21, Number.NaN, Number.POSITIVE_INFINITY]) {
      expect(accepts([{ op: 'cancelCommand', player: 0, gang }])).toBe(false)
    }
    expect(accepts([{ op: 'cancelCommand', player: 0, gang: 7 }])).toBe(true)
  })

  it('refuses missing fields, unknown fields and a wrong document shape', () => {
    expect(accepts([{ op: 'cancelCommand', player: 0 }])).toBe(false)
    expect(accepts([{ op: 'cancelCommand', player: 0, gang: 1, extra: 1 }])).toBe(false)
    expect(accepts([{ ...submitCommand, target: { kind: 'sector', id: 1, extra: 1 } }])).toBe(false)
    expect(safeParse(orderDocumentSchema, { schemaVersion: 2, ops: [] }).success).toBe(false)
    expect(safeParse(orderDocumentSchema, { schemaVersion: 1, ops: [], extra: true }).success).toBe(
      false,
    )
  })

  it('bounds the op list', () => {
    const many = Array.from({ length: LIMITS.ordersMaxOps + 1 }, () => ({
      op: 'dismissNotification',
      player: 0,
    }))
    expect(accepts(many)).toBe(false)
    expect(accepts(many.slice(1))).toBe(true)
  })
})

describe('foreignOps', () => {
  /**
   * Orders are sealed under the slot they were submitted from, so an op naming another player is
   * either a client bug or an attempt to act as somebody else. The server refuses the document
   * rather than silently re-attributing it.
   */
  it('finds the ops that act for a slot other than the submitter', () => {
    const mine: OrderDocument = parse(
      orderDocumentSchema,
      document([
        { op: 'cancelCommand', player: 2, gang: 1 },
        { op: 'dismissNotification', player: 2 },
      ]),
    )
    expect(foreignOps(mine, 2)).toEqual([])
    expect(foreignOps(mine, 0)).toHaveLength(2)

    const mixed: OrderDocument = parse(
      orderDocumentSchema,
      document([
        { op: 'cancelCommand', player: 1, gang: 1 },
        { op: 'dismissNotification', player: 4 },
      ]),
    )
    expect(foreignOps(mixed, 1).map((op) => op.player)).toEqual([4])
  })
})
