import { describe, expect, it } from 'vitest'
import { gameSettingsSchema, LIMITS, matchSettingsSchema, orderDocumentSchema } from '../src'

describe('orderDocumentSchema', () => {
  it('accepts a bounded op list', () => {
    const parsed = orderDocumentSchema.safeParse({
      schemaVersion: 1,
      ops: [{ op: 'moveGang', args: { gangId: 4, sector: 'B3' } }],
    })
    expect(parsed.success).toBe(true)
  })

  /**
   * The digest clients verify is taken over canonical JSON, and only integers are written identically
   * by every language's JSON writer. A float accepted here would be a digest no C# client could
   * reproduce.
   */
  it('refuses numeric arguments that would not survive another JSON writer', () => {
    const withArg = (value: unknown) => ({
      schemaVersion: 1,
      ops: [{ op: 'moveGang', args: { n: value } }],
    })
    expect(orderDocumentSchema.safeParse(withArg(7)).success).toBe(true)
    expect(orderDocumentSchema.safeParse(withArg(-7)).success).toBe(true)
    expect(orderDocumentSchema.safeParse(withArg(1.5)).success).toBe(false)
    expect(orderDocumentSchema.safeParse(withArg(-0)).success).toBe(false)
    expect(orderDocumentSchema.safeParse(withArg(1e21)).success).toBe(false)
    expect(orderDocumentSchema.safeParse(withArg(Number.NaN)).success).toBe(false)
    expect(orderDocumentSchema.safeParse(withArg(Number.MAX_SAFE_INTEGER + 2)).success).toBe(false)
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

describe('gameSettingsSchema', () => {
  it('bounds the blob in bytes, not in code units', () => {
    // Three bytes each in UTF-8: a limit measured in string length would let three times the cap
    // through, and the D1 row budget is in bytes.
    const wide = { blob: '漢'.repeat(LIMITS.gameSettingsBytes / 3) }
    expect(gameSettingsSchema.safeParse(wide).success).toBe(false)
    expect(gameSettingsSchema.safeParse({ blob: 'a'.repeat(100) }).success).toBe(true)
  })
})
