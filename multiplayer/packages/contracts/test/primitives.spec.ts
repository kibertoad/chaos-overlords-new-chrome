import { safeParse } from 'valibot'
import { describe, expect, it } from 'vitest'
import {
  displayNameInputSchema,
  displayNameSchema,
  eventSeqSchema,
  foldName,
  formatVersionSchema,
  INT32_MAX,
  INT32_MIN,
  isoTimestampSchema,
  matchNameSchema,
  originalPlayerNameProjection,
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

  it('refuses a modern name that becomes a cheat in the native ten-character record', () => {
    expect(originalPlayerNameProjection('SMGFUNDAGE THE THIRD')).toBe('SMGFUNDAGE')
    expect(safeParse(displayNameInputSchema, 'SMGFUNDAGE THE THIRD').success).toBe(false)
  })

  it('refuses a name whose long s folds into a native cheat', () => {
    const name = 'ſMGISLANDS'
    expect(originalPlayerNameProjection(name)).toBe('SMGISLANDS')
    expect(safeParse(displayNameInputSchema, name).success).toBe(false)
  })

  it('leaves dotless i outside the native alphabet as the C# projection does', () => {
    const name = 'SMGıSLANDS'
    expect(originalPlayerNameProjection(name)).toBe('SMG SLANDS')
    expect(safeParse(displayNameInputSchema, name).success).toBe(true)
  })

  /**
   * The length is measured after normalisation, not before.
   *
   * NFC does not preserve length: `שּׁ` is one unit going in and three coming out, and it is
   * category Lo so the unsafe-character check accepts it. Measuring first stored a 96-unit name
   * that then failed `displayNameSchema` on the way out, so every response carrying it — the match
   * view, the events page, and `GET /matches` when it was a public host's name — became a 500 that
   * one unauthenticated join could cause and nobody could undo.
   */
  it('refuses a name that fits only until NFC expands it', () => {
    const expanding = 'שּׁ'.repeat(32)
    expect(expanding).toHaveLength(32)
    expect(expanding.normalize('NFC')).toHaveLength(96)
    expect(safeParse(displayNameInputSchema, expanding).success).toBe(false)
    // The relaying schema still accepts whatever a roster already holds.
    const accepted = safeParse(displayNameInputSchema, 'שּׁ'.repeat(10))
    expect(accepted.success).toBe(true)
    if (accepted.success) {
      expect(safeParse(displayNameSchema, accepted.output).success).toBe(true)
    }
  })

  it('allows a name that only contains a cheat outside the native record', () => {
    expect(safeParse(displayNameInputSchema, 'I AM SMGFUNDAGE').success).toBe(true)
  })

  it.each([
    ['a C0 control', 'Ada\u0007Lovelace'],
    ['a C1 control', 'Ada\u0085Lovelace'],
    ['a bidi override', 'Ada\u202ELovelace'],
    ['a zero-width joiner', 'Ada\u200DLovelace'],
    ['a private-use glyph', 'Ada\uE000'],
  ])('refuses a name carrying %s', (_what, name) => {
    // A bidi override rewrites how every name drawn after it reads, on every other player's screen.
    // The rest have no agreed rendering at all.
    expect(safeParse(displayNameInputSchema, name).success).toBe(false)
  })

  it('normalises a name to NFC, so one name is one string', () => {
    const composed = 'Ren\u00E9'
    const decomposed = 'Ren\u0065\u0301'
    expect(decomposed).not.toBe(composed)
    const parsed = safeParse(displayNameInputSchema, decomposed)
    expect(parsed.success && parsed.output).toBe(composed)
  })

  it('relays a name a looser rule once let through', () => {
    // Same reason as the reserved names: reading a roster is not choosing a name.
    expect(safeParse(displayNameSchema, 'Ada\u200DLovelace').success).toBe(true)
  })
})

describe('foldName', () => {
  it('makes case, spacing and the combining form of one name the same key', () => {
    expect(foldName(' ada  LOVELACE ')).toBe(foldName('Ada Lovelace'))
    expect(foldName('Ren\u0065\u0301')).toBe(foldName('Ren\u00E9'))
    expect(foldName('Ada')).not.toBe(foldName('Adam'))
  })
})

describe('matchNameSchema', () => {
  it('holds a lobby name to the same rules, in and out', () => {
    // Unlike a display name this is only ever set through the settings, so there is no roster of
    // older names to stay readable for.
    expect(safeParse(matchNameSchema, 'Night\u202ECity').success).toBe(false)
    expect(safeParse(matchNameSchema, '  Night City  ').success).toBe(true)
  })
})
