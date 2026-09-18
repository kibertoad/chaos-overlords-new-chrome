import { safeParse } from 'valibot'
import { describe, expect, it } from 'vitest'
import { protocolVersionSchema } from '../src'

describe('protocolVersionSchema', () => {
  it('uses the signed 32-bit integer range shared with the generated C# contract', () => {
    expect(safeParse(protocolVersionSchema, 0).success).toBe(true)
    expect(safeParse(protocolVersionSchema, 2_147_483_647).success).toBe(true)
    expect(safeParse(protocolVersionSchema, 2_147_483_648).success).toBe(false)
    expect(safeParse(protocolVersionSchema, '3').success).toBe(false)
  })
})
