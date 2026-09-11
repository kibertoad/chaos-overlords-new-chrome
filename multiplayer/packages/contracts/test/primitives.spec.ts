import { safeParse } from 'valibot'
import { describe, expect, it } from 'vitest'
import {
  displayNameInputSchema,
  displayNameSchema,
  eventSeqSchema,
  formatVersionSchema,
  INT32_MAX,
  INT32_MIN,
  isoTimestampSchema,
  RESERVED_DISPLAY_NAMES,
  resourceIdSchema,
  seedSchema,
  turnNumberSchema,
  turnPathParamSchema,
} from '../src'

/**
 * The bounds are literals in the pipes, so the C# generator can read them without running the
 * source, which leaves the same fact stated twice. This is what keeps the two equal.
 */
describe('int32-bounded schemas', () => {
  it('accept the edge of the range the C# client can hold, and nothing past it', () => {
    for (const schema of [turnNumberSchema, eventSeqSchema, formatVersionSchema]) {
      expect(safeParse(schema, INT32_MAX).success).toBe(true)
      expect(safeParse(schema, INT32_MAX + 1).success).toBe(false)
    }
    expect(safeParse(turnPathParamSchema, String(INT32_MAX)).success).toBe(true)
    expect(safeParse(turnPathParamSchema, String(INT32_MAX + 1)).success).toBe(false)
  })

  /**
   * `MatchSetup.InitialSeed` is a C# `int`, so half of all seeds are negative. A schema that
   * refused them would make half of all matches unstartable.
   */
  it('take a seed across the whole signed 32-bit range', () => {
    expect(safeParse(seedSchema, INT32_MIN).success).toBe(true)
    expect(safeParse(seedSchema, INT32_MAX).success).toBe(true)
    expect(safeParse(seedSchema, INT32_MIN - 1).success).toBe(false)
    expect(safeParse(seedSchema, INT32_MAX + 1).success).toBe(false)
  })

  /** `-0` survives every range check and has no canonical JSON text for a peer to reproduce. */
  it('refuse negative zero', () => {
    for (const schema of [turnNumberSchema, eventSeqSchema, seedSchema]) {
      expect(safeParse(schema, -0).success).toBe(false)
      expect(safeParse(schema, 0).success).toBe(true)
    }
  })
})

describe('resourceIdSchema', () => {
  /** Contract resolvers interpolate ids raw, so an id that could leave its path segment is refused. */
  it('takes a UUID and refuses anything that could escape a path segment', () => {
    expect(safeParse(resourceIdSchema, '0b6a1d7e-8f4c-4f2a-9a1e-2c7d3b5e6f70').success).toBe(true)
    for (const id of ['..', 'a/b', 'a?b', 'a#b', 'a b', '', 'x'.repeat(65)]) {
      expect(safeParse(resourceIdSchema, id).success).toBe(false)
    }
  })
})

describe('isoTimestampSchema', () => {
  it('takes what toISOString writes and refuses a spelling a client would have to guess at', () => {
    expect(safeParse(isoTimestampSchema, new Date(0).toISOString()).success).toBe(true)
    expect(safeParse(isoTimestampSchema, '2026-09-10T12:00:00Z').success).toBe(true)
    for (const value of ['2026-09-10T12:00:00+02:00', '2026-09-10 12:00:00Z', '2026-09-10']) {
      expect(safeParse(isoTimestampSchema, value).success).toBe(false)
    }
  })
})

describe('displayNameInputSchema', () => {
  it.each(RESERVED_DISPLAY_NAMES)('refuses %s, whatever case it is typed in', (reserved) => {
    for (const spelling of [reserved, reserved.toLowerCase(), ` ${reserved} `]) {
      expect(safeParse(displayNameInputSchema, spelling).success).toBe(false)
    }
  })

  it('still relays a reserved name already on a roster', () => {
    // Reading is not choosing. A name stored before the rule existed has to stay readable, or the
    // match it belongs to stops being describable at all.
    for (const reserved of RESERVED_DISPLAY_NAMES) {
      expect(safeParse(displayNameSchema, reserved).success).toBe(true)
    }
  })

  it('allows a name that merely contains one', () => {
    // The rules match the whole name, so only the whole name is a cheat; refusing a substring would
    // rule out names that do nothing.
    expect(safeParse(displayNameInputSchema, 'SMGFUNDAGE THE THIRD').success).toBe(true)
  })
})
