import { mapApiContractToPath } from '@toad-contracts/core'
import { safeParse } from 'valibot'
import { describe, expect, it } from 'vitest'
import {
  API_CONTRACTS,
  eventsQuerySchema,
  GAME_BOUNDS,
  gangActionSchema,
  gangDefinitionIdSchema,
  itemIdSchema,
  LIMITS,
  matchEventSchema,
  sealedOrdersContract,
  sectorIdSchema,
  siteIdSchema,
  slotSchema,
} from '../src'

describe('API_CONTRACTS', () => {
  it('resolves a path and derives the route pattern from the same definition', () => {
    expect(sealedOrdersContract.pathResolver({ matchId: 'm1', turn: 7 })).toBe(
      '/matches/m1/turns/7/orders',
    )
    expect(mapApiContractToPath(sealedOrdersContract)).toBe('/matches/:matchId/turns/:turn/orders')
  })

  it('covers every route the server serves, each with the error envelope', () => {
    const routes = Object.values(API_CONTRACTS).map(
      (contract) => `${contract.method.toUpperCase()} ${mapApiContractToPath(contract)}`,
    )
    expect([...routes].sort()).toEqual(
      [
        'GET /matches',
        'POST /matches',
        'POST /matches/join',
        'GET /matches/:matchId',
        'POST /matches/:matchId/start',
        'POST /matches/:matchId/leave',
        'POST /matches/:matchId/players/:playerId/kick',
        'PUT /matches/:matchId/turns/:turn/orders',
        'GET /matches/:matchId/turns/:turn/orders/mine',
        'GET /matches/:matchId/turns/:turn/orders',
        'POST /matches/:matchId/turns/:turn/report',
        'POST /matches/:matchId/snapshots',
        'GET /matches/:matchId/snapshots/latest',
        'GET /matches/:matchId/snapshots/:turn',
        'GET /matches/:matchId/events',
        'GET /matches/:matchId/stream',
      ].sort(),
    )
    for (const contract of Object.values(API_CONTRACTS)) {
      expect(contract.responsesByStatusCode[422]).toBeDefined()
    }
  })
})

/**
 * The bounds are written as literals in the pipes so the C# generator can read them without running
 * the source, which leaves two statements of the same fact. This is what keeps them equal.
 */
describe('order id bounds', () => {
  const rejects = (schema: Parameters<typeof safeParse>[0], value: number) =>
    !safeParse(schema, value).success

  it('matches GAME_BOUNDS exactly', () => {
    expect(safeParse(slotSchema, GAME_BOUNDS.playerCount - 1).success).toBe(true)
    expect(rejects(slotSchema, GAME_BOUNDS.playerCount)).toBe(true)
    expect(safeParse(sectorIdSchema, GAME_BOUNDS.sectorCount - 1).success).toBe(true)
    expect(rejects(sectorIdSchema, GAME_BOUNDS.sectorCount)).toBe(true)
    expect(safeParse(siteIdSchema, GAME_BOUNDS.siteCount - 1).success).toBe(true)
    expect(rejects(siteIdSchema, GAME_BOUNDS.siteCount)).toBe(true)
    expect(safeParse(itemIdSchema, GAME_BOUNDS.itemSlots - 1).success).toBe(true)
    expect(rejects(itemIdSchema, GAME_BOUNDS.itemSlots)).toBe(true)
    expect(safeParse(gangActionSchema, GAME_BOUNDS.maxGangAction).success).toBe(true)
    expect(rejects(gangActionSchema, GAME_BOUNDS.maxGangAction + 1)).toBe(true)
    expect(safeParse(gangDefinitionIdSchema, GAME_BOUNDS.minGangDefinitionId).success).toBe(true)
    expect(rejects(gangDefinitionIdSchema, GAME_BOUNDS.minGangDefinitionId - 1)).toBe(true)
    expect(safeParse(gangDefinitionIdSchema, GAME_BOUNDS.maxGangDefinitionId).success).toBe(true)
    expect(rejects(gangDefinitionIdSchema, GAME_BOUNDS.maxGangDefinitionId + 1)).toBe(true)
  })
})

describe('eventsQuerySchema', () => {
  it('defaults a bare query and coerces the text a query string carries', () => {
    expect(safeParse(eventsQuerySchema, {}).output).toEqual({
      after: 0,
      limit: LIMITS.eventsPageSize,
    })
    expect(safeParse(eventsQuerySchema, { after: '41', limit: '5' }).output).toEqual({
      after: 41,
      limit: 5,
    })
  })

  /** A typo has to fail rather than default, or the client silently rereads the log from the start. */
  it('refuses a value that is not a non-negative integer', () => {
    for (const after of ['', 'x', '-1', '1.5']) {
      expect(safeParse(eventsQuerySchema, { after }).success).toBe(false)
    }
    expect(safeParse(eventsQuerySchema, { limit: String(LIMITS.eventsPageSize + 1) }).success).toBe(
      false,
    )
  })
})

describe('matchEventSchema', () => {
  it('carries the log envelope on every member of the union', () => {
    const parsed = safeParse(matchEventSchema, {
      seq: 3,
      matchId: 'm1',
      createdAt: '2026-09-10T12:00:00.000Z',
      type: 'turn.sealed',
      payload: { turn: 7, orderSetHash: 'a'.repeat(64) },
    })
    expect(parsed.success && parsed.output.seq).toBe(3)
    expect(parsed.success && parsed.output.type).toBe('turn.sealed')
  })

  it('refuses an unknown event type and a payload that is not its own', () => {
    const envelope = { seq: 1, matchId: 'm1', createdAt: '2026-09-10T12:00:00.000Z' }
    expect(
      safeParse(matchEventSchema, { ...envelope, type: 'turn.invented', payload: {} }).success,
    ).toBe(false)
    expect(
      safeParse(matchEventSchema, {
        ...envelope,
        type: 'turn.sealed',
        payload: { turn: 7, stateHash: 'a'.repeat(64) },
      }).success,
    ).toBe(false)
  })
})
