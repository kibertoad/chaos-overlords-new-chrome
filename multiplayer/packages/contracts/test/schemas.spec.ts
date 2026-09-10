import { describe, expect, it } from 'vitest'
import { LIMITS, matchSettingsSchema, orderDocumentSchema } from '../src'

describe('orderDocumentSchema', () => {
  it('accepts a bounded op list', () => {
    const parsed = orderDocumentSchema.safeParse({
      schemaVersion: 1,
      ops: [{ op: 'moveGang', args: { gangId: 4, sector: 'B3' } }],
    })
    expect(parsed.success).toBe(true)
  })

  it('refuses nested arguments and oversize op lists', () => {
    expect(
      orderDocumentSchema.safeParse({
        schemaVersion: 1,
        ops: [{ op: 'moveGang', args: { nested: { a: 1 } } }],
      }).success,
    ).toBe(false)
    const ops = Array.from({ length: LIMITS.ordersMaxOps + 1 }, () => ({ op: 'noop', args: {} }))
    expect(orderDocumentSchema.safeParse({ schemaVersion: 1, ops }).success).toBe(false)
  })
})

describe('matchSettingsSchema', () => {
  it('allows a disabled timer but not a tiny one', () => {
    const base = { name: 'x', maxPlayers: 2, visibility: 'private', gameSettings: {} }
    expect(matchSettingsSchema.safeParse({ ...base, turnTimerSeconds: 0 }).success).toBe(true)
    expect(matchSettingsSchema.safeParse({ ...base, turnTimerSeconds: 5 }).success).toBe(false)
  })
})
