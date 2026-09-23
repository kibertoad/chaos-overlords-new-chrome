import { defineStorageConformance } from '@chaos-overlords/conformance'
import type { Match, Player } from '@chaos-overlords/kernel'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve, sep } from 'node:path'
import BetterSqlite3 from 'better-sqlite3'
import { describe, expect, it } from 'vitest'
import { openSqliteStorage } from '../src/node'

describe('better-sqlite3 (in-memory)', () => {
  const opened = openSqliteStorage(':memory:')
  defineStorageConformance({ createStorage: async () => opened.storage })
})

it('discards an orphan vote before a later prompt reuses the seat', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'rechaos-votes-'))
  if (!directory.startsWith(`${resolve(tmpdir())}${sep}`)) throw new Error('unexpected temp path')
  const opened = openSqliteStorage(join(directory, 'match.db'))
  const raw = new BetterSqlite3(join(directory, 'match.db'))
  try {
    const now = new Date('2026-03-01T12:00:00.000Z')
    const match: Match = {
      id: 'orphan-vote-match',
      protocolVersion: 1,
      sessionVersion: 1,
      status: 'lobby',
      settings: {
        name: 'Orphan vote',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostPlayerId: 'orphan-vote-host',
      joinCode: 'VOTE0001',
      passwordHash: null,
      seed: null,
      currentTurn: 0,
      seatCount: 1,
      joinCounter: 1,
      createdAt: now,
      updatedAt: now,
    }
    const player: Player = {
      id: 'orphan-vote-target',
      matchId: match.id,
      slot: 0,
      joinOrder: 0,
      displayName: 'Target',
      portraitId: 0,
      tokenHash: 'orphan-vote-token',
      status: 'left',
      joinedAt: now,
    }
    expect(await opened.storage.matches.create(match)).toBe(true)
    expect(await opened.storage.players.create(player)).toBe(true)
    expect(await opened.storage.takeovers.openPrompt(match.id, player.id, 1, now)).toBe(true)
    expect(
      await opened.storage.takeovers.castVote({
        matchId: match.id,
        targetPlayerId: player.id,
        voterPlayerId: 'voter',
        decision: 'computer',
        castAt: now,
      }),
    ).toBe(true)
    // Simulate a process dying after deleting the prompt but before deleting its votes.
    raw
      .prepare('DELETE FROM takeover_prompts WHERE match_id = ? AND player_id = ?')
      .run(match.id, player.id)
    expect(await opened.storage.takeovers.openPrompt(match.id, player.id, 2, now)).toBe(true)
    expect(await opened.storage.takeovers.listVotes(match.id, player.id)).toEqual([])
    const orphanCount = raw
      .prepare('SELECT count(*) AS count FROM takeover_votes WHERE match_id = ?')
      .get(match.id) as { count: number }
    expect(orphanCount.count).toBe(0)
  } finally {
    raw.close()
    await opened.close()
    rmSync(directory, { recursive: true, force: true })
  }
})
