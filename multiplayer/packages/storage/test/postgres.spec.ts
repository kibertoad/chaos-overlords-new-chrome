import { defineStorageConformance } from '@chaos-overlords/conformance'
import type { Match, Player } from '@chaos-overlords/kernel'
import pg from 'pg'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { type OpenedStorage, openPostgresStorage } from '../src/node'

const url = process.env.TEST_DATABASE_URL

/**
 * Skipping is right on a laptop and wrong in CI.
 *
 * `REQUIRE_POSTGRES` is set by the workflows that stand a Postgres service up. Without it, a broken
 * env variable — a rename, a turbo `passThroughEnv` change, a service container that failed its
 * health check after the job started — turned the whole Postgres suite into a green skip, and a
 * release published the Postgres storage package untested.
 */
if (process.env.REQUIRE_POSTGRES === '1' && !url) {
  throw new Error('REQUIRE_POSTGRES=1 but TEST_DATABASE_URL is empty.')
}

/** Needs a reachable Postgres: `docker compose up -d` at the workspace root sets one up. */
describe.skipIf(!url)('postgres', () => {
  let opened: OpenedStorage | undefined

  /**
   * The suite's contract is a migrated, EMPTY database. Unlike the in-memory SQLite file, a Postgres
   * service outlives the run, and leftovers from a previous one would drift into the global queries
   * (the lobby listing, expired turns) that this suite asserts on.
   */
  beforeAll(async () => {
    opened = await openPostgresStorage(url as string)
    const pool = new pg.Pool({ connectionString: url })
    try {
      await pool.query('TRUNCATE TABLE matches CASCADE')
    } finally {
      await pool.end()
    }
  })

  afterAll(async () => {
    await opened?.close()
  })

  defineStorageConformance({
    createStorage: async () => (opened as OpenedStorage).storage,
  })

  async function matchWithPlayers(maxPlayers = 2, playerCount = 1, start = true) {
    const storage = (opened as OpenedStorage).storage
    const now = new Date()
    const id = crypto.randomUUID()
    const match: Match = {
      id,
      protocolVersion: 1,
      sessionVersion: 1,
      status: 'lobby',
      settings: {
        name: 'Concurrent guards',
        maxPlayers,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: { scenario: 'deep' },
      },
      hostPlayerId: `${id}-p0`,
      joinCode: id.replaceAll('-', '').slice(0, 8).toUpperCase(),
      passwordHash: null,
      seed: null,
      currentTurn: 0,
      seatCount: playerCount,
      joinCounter: playerCount,
      createdAt: now,
      updatedAt: now,
    }
    const player = (slot: number): Player => ({
      id: `${id}-p${slot}`,
      matchId: id,
      slot,
      joinOrder: slot,
      displayName: `P${slot}`,
      portraitId: 0,
      tokenHash: `${id}-token-${slot}`,
      status: 'active',
      joinedAt: now,
    })
    expect(await storage.matches.create(match)).toBe(true)
    for (let slot = 0; slot < playerCount; slot += 1) {
      expect(await storage.players.create(player(slot))).toBe(true)
    }
    if (start) {
      expect(
        await storage.matches.transition(id, ['lobby'], {
          status: 'running',
          currentTurn: 1,
          updatedAt: now,
        }),
      ).toBe(true)
    }
    return { storage, match, player }
  }

  async function holdingUpdate(
    query: string,
    params: unknown[],
    operation: () => Promise<boolean>,
  ) {
    const client = new pg.Client({ connectionString: url as string })
    await client.connect()
    try {
      await client.query('BEGIN')
      await client.query(query, params)
      const result = operation()
      // A guarded write must wait for the status update, rather than use its old snapshot.
      let settled = false
      void result.then(
        () => {
          settled = true
        },
        () => {
          settled = true
        },
      )
      await new Promise((resolve) => setTimeout(resolve, 75))
      expect(settled).toBe(false)
      await client.query('COMMIT')
      expect(await result).toBe(false)
    } finally {
      await client.query('ROLLBACK').catch(() => {})
      await client.end()
    }
  }

  it('does not insert a lobby player after start commits', async () => {
    const { storage, match, player } = await matchWithPlayers(2, 1, false)
    await holdingUpdate("UPDATE matches SET status = 'running' WHERE id = $1", [match.id], () =>
      storage.players.create(player(1)),
    )
  })

  it('serializes late joins before counting remaining capacity', async () => {
    const { storage, match, player } = await matchWithPlayers(3, 2)
    const client = new pg.Client({ connectionString: url as string })
    await client.connect()
    try {
      await client.query('BEGIN')
      await client.query('SELECT id FROM matches WHERE id = $1 FOR UPDATE', [match.id])
      const joins = [storage.players.createLate(player(2)), storage.players.createLate(player(3))]
      await new Promise((resolve) => setTimeout(resolve, 75))
      await client.query('COMMIT')
      expect((await Promise.all(joins)).sort()).toEqual([false, true])
      expect((await storage.players.listByMatch(match.id)).length).toBe(3)
    } finally {
      await client.query('ROLLBACK').catch(() => {})
      await client.end()
    }
  })

  async function openTurn(matchId: string, playerId: string) {
    const storage = (opened as OpenedStorage).storage
    await storage.turns.open(
      {
        matchId,
        number: 1,
        status: 'open',
        openedAt: new Date(),
        deadlineAt: null,
        sealedAt: null,
        orderSetHash: null,
        sealedSlots: null,
        stateHash: null,
        desyncedAt: null,
      },
      [playerId],
    )
  }

  it('does not accept orders after the seal commits', async () => {
    const { storage, match, player } = await matchWithPlayers()
    await openTurn(match.id, player(0).id)
    await holdingUpdate(
      "UPDATE turns SET status = 'sealed' WHERE match_id = $1 AND number = 1",
      [match.id],
      () =>
        storage.turns.submitOrders(match.id, 1, player(0).id, {
          orders: { schemaVersion: 1, ops: [] },
          ordersHash: 'a'.repeat(64),
          ready: true,
          submittedAt: new Date(),
        }),
    )
  })

  it('does not accept a report after the verdict commits', async () => {
    const { storage, match, player } = await matchWithPlayers()
    await openTurn(match.id, player(0).id)
    const client = new pg.Client({ connectionString: url as string })
    await client.connect()
    try {
      await client.query("UPDATE turns SET status = 'sealed' WHERE match_id = $1 AND number = 1", [
        match.id,
      ])
    } finally {
      await client.end()
    }
    await holdingUpdate(
      "UPDATE turns SET status = 'confirmed' WHERE match_id = $1 AND number = 1",
      [match.id],
      () =>
        storage.turns.upsertReport({
          matchId: match.id,
          turn: 1,
          playerId: player(0).id,
          stateHash: 'a'.repeat(32),
          finished: false,
          reportedAt: new Date(),
        }),
    )
  })
})
