import { type OrderDocument, orderDocumentSchema } from '@chaos-overlords/contracts'
import { describe, expect, it } from 'vitest'
import type { Player, TurnReport } from '../src'
import {
  assignSlots,
  authoritativeCandidates,
  canonicalJson,
  evaluateConsensus,
  generateJoinCode,
  generateSeed,
  hashOrderDocument,
  hashOrderSet,
  hashPassword,
  verifyPassword,
} from '../src'

function player(id: string, joinOrder: number, status: Player['status'] = 'active'): Player {
  return {
    id,
    matchId: 'm',
    slot: -1,
    joinOrder,
    displayName: id,
    tokenHash: null,
    status,
    joinedAt: new Date('2026-01-01T00:00:00.000Z'),
  }
}

function report(playerId: string, stateHash: string, finished = false): TurnReport {
  return { matchId: 'm', turn: 1, playerId, stateHash, finished, reportedAt: new Date() }
}

const HASH_A = 'a'.repeat(64)
const HASH_B = 'b'.repeat(64)

describe('canonicalJson', () => {
  it('is independent of key order and nested', () => {
    expect(canonicalJson({ b: [{ z: 1, a: 2 }], a: null })).toBe('{"a":null,"b":[{"a":2,"z":1}]}')
  })

  it('sorts keys by code unit, the order .NET calls ordinal', () => {
    expect(canonicalJson({ Z: 1, a: 2, A: 3, é: 4, Ä: 5 })).toBe('{"A":3,"Z":1,"a":2,"Ä":5,"é":4}')
  })

  it('refuses values whose text another language would write differently', () => {
    expect(() => canonicalJson({ x: 1.5 })).toThrow(/not a safe/)
    expect(() => canonicalJson({ x: 1e21 })).toThrow(/not a safe/)
    expect(() => canonicalJson({ x: -0 })).toThrow(/not a safe/)
    expect(() => canonicalJson({ x: Number.NaN })).toThrow(/not a safe/)
    expect(() => canonicalJson({ x: undefined })).toThrow(/unsupported type/)
  })

  /**
   * A golden digest for the C# side to reproduce, over a document in the real op vocabulary. Its
   * canonical text is
   * `{"ops":[{"action":10,"gang":7,"op":"submitCommand","player":0,"repeat":false,"secondaryTarget":null,"target":{"id":27,"kind":"sector"}},{"gangDefinitionId":44,"op":"queueHire","player":0,"sectorId":27}],"schemaVersion":1}`
   * and the digest is SHA-256 of those UTF-8 bytes. If this value ever has to change, every client
   * changes with it.
   */
  it('pins the order digest of a known document', async () => {
    const document: OrderDocument = {
      schemaVersion: 1,
      ops: [
        {
          op: 'submitCommand',
          player: 0,
          gang: 7,
          action: 10,
          target: { kind: 'sector', id: 27 },
          repeat: false,
          secondaryTarget: null,
        },
        { op: 'queueHire', player: 0, gangDefinitionId: 44, sectorId: 27 },
      ],
    }
    // The pin is only worth anything over a document the wire would actually carry.
    expect(orderDocumentSchema.safeParse(document).success).toBe(true)
    expect(await hashOrderDocument(document)).toBe(
      '1e8d923be158821a974c60903be48d010f7499bdadc3510f6ea3714abadd6631',
    )
  })

  it('hashes equal documents equally regardless of serializer ordering', async () => {
    const left = await hashOrderDocument({
      schemaVersion: 1,
      ops: [{ op: 'x', args: { a: 1, b: 2 } }],
    })
    const right = await hashOrderDocument({
      ops: [{ args: { b: 2, a: 1 }, op: 'x' }],
      schemaVersion: 1,
    })
    expect(left).toBe(right)
  })

  it('orders the set digest by slot, not by input order', async () => {
    const a = await hashOrderSet([
      { slot: 1, ordersHash: 'h1' },
      { slot: 0, ordersHash: 'h0' },
    ])
    const b = await hashOrderSet([
      { slot: 0, ordersHash: 'h0' },
      { slot: 1, ordersHash: 'h1' },
    ])
    expect(a).toBe(b)
  })
})

describe('evaluateConsensus', () => {
  const players = [player('a', 1), player('b', 2), player('c', 3, 'left')]

  it('waits while an active player has not reported and ignores departed ones', () => {
    expect(evaluateConsensus(players, [report('a', 'h')], null)).toEqual({ kind: 'pending' })
    expect(evaluateConsensus(players, [report('a', 'h'), report('b', 'h')], null)).toEqual({
      kind: 'confirmed',
      stateHash: 'h',
      finished: false,
    })
  })

  it('flags disagreement as a desync with every report attached', () => {
    const verdict = evaluateConsensus(players, [report('a', 'h1'), report('b', 'h2')], null)
    expect(verdict.kind).toBe('desynced')
  })

  it('judges against the authoritative hash once a snapshot exists and never desyncs again', () => {
    const reports = [report('a', 'h1'), report('b', 'h2')]
    expect(evaluateConsensus(players, reports, 'h1')).toEqual({ kind: 'pending' })
    expect(evaluateConsensus(players, [report('a', 'h1'), report('b', 'h1', true)], 'h1')).toEqual({
      kind: 'confirmed',
      stateHash: 'h1',
      finished: false,
    })
  })

  it('reports finished only when every active player saw the end', () => {
    const verdict = evaluateConsensus(
      players,
      [report('a', 'h', true), report('b', 'h', true)],
      null,
    )
    expect(verdict).toEqual({ kind: 'confirmed', stateHash: 'h', finished: true })
  })
})

describe('assignSlots', () => {
  it('seats the host first, then join order, skipping departed players', () => {
    const roster = [
      player('late', 3),
      player('host', 0),
      player('early', 1),
      player('gone', 2, 'left'),
    ]
    expect(assignSlots(roster, 'host')).toEqual([
      { playerId: 'host', slot: 0 },
      { playerId: 'early', slot: 1 },
      { playerId: 'late', slot: 2 },
    ])
  })

  it('does not depend on join timestamps, which can collide to the millisecond', () => {
    const roster = [player('zulu', 1), player('alpha', 2), player('host', 0)]
    expect(assignSlots(roster, 'host').map((seat) => seat.playerId)).toEqual([
      'host',
      'zulu',
      'alpha',
    ])
  })
})

describe('credentials', () => {
  it('verifies a hashed password and refuses another', async () => {
    const stored = await hashPassword('correct horse')
    expect(await verifyPassword('correct horse', stored)).toBe(true)
    expect(await verifyPassword('battery staple', stored)).toBe(false)
    expect(await verifyPassword('correct horse', 'garbage')).toBe(false)
  })

  it('join codes avoid ambiguous glyphs', () => {
    for (let i = 0; i < 50; i += 1) expect(generateJoinCode(8)).toMatch(/^[A-HJ-NP-Z2-9]{8}$/)
  })

  /**
   * Folding a random byte with `%` over a 31-glyph alphabet would favour the first four glyphs by
   * about 3%. Over this many draws that bias is far larger than the sampling noise allowed here.
   */
  it('draws join code glyphs without modulo bias', () => {
    const counts = new Map<string, number>()
    const draws = 400
    for (let i = 0; i < draws; i += 1) {
      for (const glyph of generateJoinCode(8)) {
        counts.set(glyph, (counts.get(glyph) ?? 0) + 1)
      }
    }
    const expected = (draws * 8) / 31
    const head = ['A', 'B', 'C', 'D'].reduce((sum, glyph) => sum + (counts.get(glyph) ?? 0), 0) / 4
    expect(head).toBeGreaterThan(expected * 0.75)
    expect(head).toBeLessThan(expected * 1.25)
  })
})

describe('authoritativeCandidates', () => {
  /**
   * This is what stops the host arbitrating a disagreement it is a party to: the hash a recovery
   * snapshot may claim has to be one the players themselves computed in the greatest number.
   */
  it('names the hashes the most active players reported', () => {
    const roster = [player('a', 0), player('b', 1), player('c', 2)]
    const reports = [report('a', HASH_A), report('b', HASH_A), report('c', HASH_B)]
    expect(authoritativeCandidates(roster, reports)).toEqual([HASH_A])
  })

  it('reports every hash of a tie, which the host is then free to break', () => {
    const roster = [player('a', 0), player('b', 1)]
    const tied = authoritativeCandidates(roster, [report('a', HASH_A), report('b', HASH_B)])
    expect(tied).toEqual([HASH_A, HASH_B].sort())
  })

  it('ignores departed players, whose reports no longer count', () => {
    const roster = [player('a', 0), player('b', 1), player('c', 2, 'kicked')]
    const reports = [report('a', HASH_A), report('b', HASH_B), report('c', HASH_B)]
    // Without the filter, the kicked player's report would carry the verdict.
    expect(authoritativeCandidates(roster, reports)).toEqual([HASH_A, HASH_B].sort())
  })

  it('has nothing to say before anyone has reported', () => {
    expect(authoritativeCandidates([player('a', 0)], [])).toEqual([])
  })
})

describe('generateSeed', () => {
  /**
   * `MatchSetup.InitialSeed` is a C# `int`. An unsigned draw would put half of all seeds above
   * `int.MaxValue`, where the client's deserializer refuses them and the match never starts.
   */
  it('draws a seed a C# int can hold', () => {
    for (let i = 0; i < 500; i += 1) {
      const seed = generateSeed()
      expect(Number.isInteger(seed)).toBe(true)
      expect(seed).toBeGreaterThanOrEqual(-2_147_483_648)
      expect(seed).toBeLessThanOrEqual(2_147_483_647)
    }
  })

  it('still spends the full 32 bits, so seeds do not collide', () => {
    const drawn = new Set(Array.from({ length: 200 }, () => generateSeed()))
    expect(drawn.size).toBe(200)
    // Both signs must occur, or the range was silently halved.
    expect([...drawn].some((seed) => seed < 0)).toBe(true)
    expect([...drawn].some((seed) => seed > 0)).toBe(true)
  })
})
