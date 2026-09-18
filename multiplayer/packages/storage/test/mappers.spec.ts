import { describe, expect, it } from 'vitest'
import { type MatchRow, type SnapshotRow, toMatch, toSnapshot } from '../src/shared/mappers'

describe('database row mappers', () => {
  it('normalizes driver string integers before they reach the wire model', () => {
    const now = new Date('2026-09-18T12:00:00.000Z')
    const match: MatchRow = {
      id: 'match',
      protocolVersion: '3' as unknown as number,
      status: 'lobby',
      settings: {
        name: 'Match',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostPlayerId: 'host',
      joinCode: 'ABCDEFGH',
      passwordHash: null,
      seed: null,
      currentTurn: 0,
      seatCount: 1,
      joinCounter: 1,
      createdAt: now,
      updatedAt: now,
    }
    const snapshot: SnapshotRow = {
      matchId: 'match',
      turn: 0,
      formatVersion: 1,
      protocolVersion: '3' as unknown as number,
      stateHash: 'a'.repeat(64),
      uploadedByPlayerId: 'host',
      uploadedAt: now,
      body: 'AAAA',
    }

    expect(toMatch(match).protocolVersion).toBe(3)
    expect(toSnapshot(snapshot).protocolVersion).toBe(3)
  })

  it('refuses an invalid database protocol version instead of emitting it', () => {
    for (const protocolVersion of [null, '', ' 3 ', 'not-a-number', -1, 2_147_483_648]) {
      const row = { protocolVersion } as unknown as MatchRow
      expect(() => toMatch(row)).toThrow(/matches\.protocol_version/)
    }
  })
})
