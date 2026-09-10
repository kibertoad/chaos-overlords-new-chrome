import { describe, expect, it } from 'vitest'
import type { Player, TurnReport } from '../src'
import {
  assignSlots,
  canonicalJson,
  evaluateConsensus,
  generateJoinCode,
  hashOrderDocument,
  hashOrderSet,
  hashPassword,
  verifyPassword,
} from '../src'

function player(id: string, joinedAt: string, status: Player['status'] = 'active'): Player {
  return {
    id,
    matchId: 'm',
    slot: -1,
    displayName: id,
    tokenHash: '',
    status,
    joinedAt: new Date(joinedAt),
  }
}

function report(playerId: string, stateHash: string, finished = false): TurnReport {
  return { matchId: 'm', turn: 1, playerId, stateHash, finished, reportedAt: new Date() }
}

describe('canonicalJson', () => {
  it('is independent of key order and nested', () => {
    expect(canonicalJson({ b: [{ z: 1, a: 2 }], a: null })).toBe('{"a":null,"b":[{"a":2,"z":1}]}')
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
  const players = [
    player('a', '2026-01-01'),
    player('b', '2026-01-02'),
    player('c', '2026-01-03', 'left'),
  ]

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
      player('late', '2026-01-03'),
      player('host', '2026-01-02'),
      player('early', '2026-01-01'),
      player('gone', '2026-01-01', 'left'),
    ]
    expect(assignSlots(roster, 'host')).toEqual([
      { playerId: 'host', slot: 0 },
      { playerId: 'early', slot: 1 },
      { playerId: 'late', slot: 2 },
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
})
