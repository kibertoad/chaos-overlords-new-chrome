import { safeParse } from 'valibot'
import { describe, expect, it } from 'vitest'
import {
  MULTIPLAYER_PROTOCOL_VERSION,
  MULTIPLAYER_SESSION_VERSION,
  protocolVersionSchema,
  sessionVersionSchema,
} from '../src'

describe('protocolVersionSchema', () => {
  it('uses the signed 32-bit integer range shared with the generated C# contract', () => {
    expect(safeParse(protocolVersionSchema, 0).success).toBe(true)
    expect(safeParse(protocolVersionSchema, 2_147_483_647).success).toBe(true)
    expect(safeParse(protocolVersionSchema, 2_147_483_648).success).toBe(false)
    expect(safeParse(protocolVersionSchema, '3').success).toBe(false)
  })
})

describe('sessionVersionSchema', () => {
  it('uses the signed 32-bit integer range shared with the generated C# contract', () => {
    expect(safeParse(sessionVersionSchema, 0).success).toBe(true)
    expect(safeParse(sessionVersionSchema, 2_147_483_647).success).toBe(true)
    expect(safeParse(sessionVersionSchema, 2_147_483_648).success).toBe(false)
    expect(safeParse(sessionVersionSchema, '3').success).toBe(false)
  })
})

/**
 * The two versions move independently on purpose: the protocol has already been bumped for wire
 * changes that left every stored session resumable, and a session version that tracked it would
 * have retired those matches for nothing.
 */
describe('the two versions', () => {
  it('are separate numbers, not one renamed', () => {
    expect(MULTIPLAYER_SESSION_VERSION).toBe(6)
    expect(MULTIPLAYER_PROTOCOL_VERSION).toBe(14)
    expect(MULTIPLAYER_PROTOCOL_VERSION).toBeGreaterThan(MULTIPLAYER_SESSION_VERSION)
  })
})
