import { safeParse } from 'valibot'
import { describe, expect, it } from 'vitest'
import {
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
