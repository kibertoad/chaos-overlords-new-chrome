import { defineStorageConformance } from '@chaos-overlords/conformance'
import type { Match, Player } from '@chaos-overlords/kernel'
import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs'
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
      comlinkKey: null,
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

/**
 * Events logged before `dedupe_key` existed are announcements a repeat must recognise, or the first
 * repeat after the upgrade logs them a second time. The earliest copy of each fact takes the key.
 */
it('keys the announcements logged before the dedupe column existed', () => {
  const folder = resolve(import.meta.dirname, '../migrations/sqlite')
  const migrations = readdirSync(folder)
    .filter((name) => name.endsWith('.sql'))
    .sort()
  const run = (db: BetterSqlite3.Database, name: string) => {
    for (const statement of readFileSync(join(folder, name), 'utf8').split(
      '--> statement-breakpoint',
    )) {
      if (statement.trim() !== '') db.exec(statement)
    }
  }
  const db = new BetterSqlite3(':memory:')
  try {
    const upgrade = migrations.findIndex((name) => name.startsWith('0006_'))
    for (const name of migrations.slice(0, upgrade)) run(db, name)
    db.prepare(
      `insert into matches (id, status, name, visibility, max_players, settings, host_player_id,
        join_code, created_at, updated_at)
        values ('m', 'running', 'm', 'private', 2, '{}', 'h', 'CODE0001', 0, 0)`,
    ).run()
    const insert = db.prepare(
      'insert into match_events (match_id, seq, type, payload, created_at) values (?, ?, ?, ?, 0)',
    )
    insert.run('m', 1, 'turn.sealed', JSON.stringify({ turn: 1, orderSetHash: 'x' }))
    insert.run('m', 2, 'turn.sealed', JSON.stringify({ turn: 1, orderSetHash: 'x' }))
    insert.run('m', 3, 'turn.sealed', JSON.stringify({ turn: 2, orderSetHash: 'y' }))
    insert.run('m', 4, 'turn.opened', JSON.stringify({ turn: 3, deadlineAt: null }))
    insert.run('m', 5, 'match.statusChanged', JSON.stringify({ status: 'desynced' }))
    insert.run('m', 6, 'match.statusChanged', JSON.stringify({ status: 'finished' }))
    for (const name of migrations.slice(upgrade)) run(db, name)

    const keys = db
      .prepare('select seq, dedupe_key as key from match_events order by seq')
      .all() as Array<{ seq: number; key: string | null }>
    expect(keys).toEqual([
      { seq: 1, key: 'turn.sealed:1' },
      { seq: 2, key: null },
      { seq: 3, key: 'turn.sealed:2' },
      { seq: 4, key: null },
      { seq: 5, key: null },
      { seq: 6, key: 'match.statusChanged:finished' },
    ])
  } finally {
    db.close()
  }
})
