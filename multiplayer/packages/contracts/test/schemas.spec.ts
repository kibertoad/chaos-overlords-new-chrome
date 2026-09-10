import { safeParse } from 'valibot'
import { describe, expect, it } from 'vitest'
import {
  gameSettingsSchema,
  joinMatchRequestSchema,
  LIMITS,
  matchSettingsSchema,
  uploadSnapshotRequestSchema,
} from '../src'

describe('matchSettingsSchema', () => {
  it('allows a disabled timer but not a tiny one', () => {
    const base = { name: 'x', maxPlayers: 2, visibility: 'private', gameSettings: {} }
    expect(safeParse(matchSettingsSchema, { ...base, turnTimerSeconds: 0 }).success).toBe(true)
    expect(safeParse(matchSettingsSchema, { ...base, turnTimerSeconds: 5 }).success).toBe(false)
  })
})

describe('gameSettingsSchema', () => {
  it('bounds the blob in bytes, not in code units', () => {
    // Three bytes each in UTF-8: a limit measured in string length would let three times the cap
    // through, and the D1 row budget is in bytes.
    const wide = { blob: '漢'.repeat(LIMITS.gameSettingsBytes / 3) }
    expect(safeParse(gameSettingsSchema, wide).success).toBe(false)
    expect(safeParse(gameSettingsSchema, { blob: 'a'.repeat(100) }).success).toBe(true)
  })
})

describe('joinMatchRequestSchema', () => {
  /** Players type codes off a chat line or hear them over voice; the alphabet is uppercase only. */
  it('normalises the case and surrounding space of a join code', () => {
    const parsed = safeParse(joinMatchRequestSchema, {
      joinCode: '  abcd2345 ',
      displayName: 'Ada',
    })
    expect(parsed.success && parsed.output.joinCode).toBe('ABCD2345')
    expect(safeParse(joinMatchRequestSchema, { joinCode: 'ABC', displayName: 'Ada' }).success).toBe(
      false,
    )
  })
})

describe('uploadSnapshotRequestSchema', () => {
  const base = { turn: 1, formatVersion: 1, stateHash: 'a'.repeat(64) }

  /** The server never decodes the body, so this is the only chance to notice it cannot be decoded. */
  it('refuses base64 that could never decode', () => {
    expect(safeParse(uploadSnapshotRequestSchema, { ...base, body: 'QUJD' }).success).toBe(true)
    expect(safeParse(uploadSnapshotRequestSchema, { ...base, body: 'QQ==' }).success).toBe(true)
    expect(safeParse(uploadSnapshotRequestSchema, { ...base, body: '' }).success).toBe(true)
    expect(safeParse(uploadSnapshotRequestSchema, { ...base, body: 'QUJDQ' }).success).toBe(false)
    expect(safeParse(uploadSnapshotRequestSchema, { ...base, body: 'QU_J' }).success).toBe(false)
  })
})

describe('gameSettingsSchema depth', () => {
  /**
   * An unbounded recursion would overflow the stack on a body well inside the size limit, and a
   * `RangeError` is not a validation issue, so the caller would see a 500 where a 422 is the truth.
   */
  it('refuses nesting deeper than the limit instead of overflowing the stack', () => {
    const nest = (depth: number): unknown => (depth === 0 ? 1 : { next: nest(depth - 1) })
    expect(
      safeParse(gameSettingsSchema, { a: nest(LIMITS.gameSettingsMaxDepth - 1) }).success,
    ).toBe(true)
    expect(
      safeParse(gameSettingsSchema, { a: nest(LIMITS.gameSettingsMaxDepth + 5) }).success,
    ).toBe(false)
    let deep: unknown = 1
    for (let i = 0; i < 20_000; i += 1) deep = [deep]
    expect(() => safeParse(gameSettingsSchema, { a: deep })).not.toThrow()
  })
})
