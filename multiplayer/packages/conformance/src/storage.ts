import type { Match, MultiplayerStorage, Player, Turn } from '@chaos-overlords/kernel'
import { beforeEach, describe, expect, it } from 'vitest'

export interface StorageConformanceHarness {
  /** A fresh, migrated, empty storage per call (or one whose ids never collide across calls). */
  createStorage(): Promise<MultiplayerStorage>
}

let counter = 0
const uid = (prefix: string): string => {
  counter += 1
  return `${prefix}-${Date.now().toString(36)}-${counter}`
}

function matchFixture(overrides: Partial<Match> = {}): Match {
  const id = uid('match')
  const now = new Date('2026-03-01T10:00:00.000Z')
  return {
    id,
    status: 'lobby',
    settings: {
      name: 'Conformance',
      maxPlayers: 2,
      turnTimerSeconds: 0,
      visibility: 'public',
      gameSettings: { scenario: 'deep' },
    },
    hostPlayerId: uid('host'),
    joinCode: crypto.randomUUID().replaceAll('-', '').slice(0, 8).toUpperCase(),
    passwordHash: null,
    seed: null,
    currentTurn: 0,
    seatCount: 1,
    joinCounter: 1,
    createdAt: now,
    updatedAt: now,
    ...overrides,
  }
}

function playerFixture(match: Match, overrides: Partial<Player> = {}): Player {
  return {
    id: uid('player'),
    matchId: match.id,
    slot: -1,
    joinOrder: 0,
    displayName: 'P',
    tokenHash: uid('hash'),
    status: 'active',
    joinedAt: new Date('2026-03-01T10:00:01.000Z'),
    ...overrides,
  }
}

function turnFixture(match: Match, number: number, overrides: Partial<Turn> = {}): Turn {
  return {
    matchId: match.id,
    number,
    status: 'open',
    openedAt: new Date('2026-03-01T11:00:00.000Z'),
    deadlineAt: null,
    sealedAt: null,
    orderSetHash: null,
    sealedSlots: null,
    ...overrides,
  }
}

/**
 * Pins the atomicity contracts the services rely on. A dialect that maps a column differently, or
 * turns a conditional statement into a read-then-write, fails here rather than in a live race. The
 * in-memory reference implementation runs through the same suite, so it cannot drift from SQL either.
 */
export function defineStorageConformance(harness: StorageConformanceHarness): void {
  describe('storage conformance', () => {
    let storage: MultiplayerStorage

    beforeEach(async () => {
      storage = await harness.createStorage()
    })

    it('round-trips a match with its settings, dates and nullable columns', async () => {
      const match = matchFixture({ passwordHash: 'pbkdf2$1$aa$bb' })
      expect(await storage.matches.create(match)).toBe(true)
      expect(await storage.matches.get(match.id)).toEqual(match)
      expect(await storage.matches.getByJoinCode(match.joinCode)).toEqual(match)
      expect(await storage.matches.get('nope')).toBeNull()
    })

    it('refuses a second match on a taken join code instead of throwing', async () => {
      const first = matchFixture()
      expect(await storage.matches.create(first)).toBe(true)
      expect(await storage.matches.create(matchFixture({ joinCode: first.joinCode }))).toBe(false)
      expect(await storage.matches.create(first)).toBe(false)
    })

    it('claims seats atomically up to capacity, numbering them monotonically', async () => {
      const match = matchFixture({ settings: { ...matchFixture().settings, maxPlayers: 3 } })
      await storage.matches.create(match)
      expect(await storage.matches.claimSeat(match.id)).toBe(1)
      await storage.matches.releaseSeat(match.id)
      expect((await storage.matches.get(match.id))?.seatCount).toBe(1)
      // The freed seat is reusable, but its join number is not: the sequence only ever moves on.
      expect(await storage.matches.claimSeat(match.id)).toBe(2)
      expect(await storage.matches.claimSeat(match.id)).toBe(3)
      expect(await storage.matches.claimSeat(match.id)).toBeNull()
      await storage.matches.transition(match.id, ['lobby'], {
        status: 'running',
        updatedAt: new Date(),
      })
      expect(await storage.matches.claimSeat(match.id)).toBeNull()
    })

    it('transitions only from the expected statuses and patches the given fields', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const at = new Date('2026-03-02T00:00:00.000Z')
      expect(
        await storage.matches.transition(match.id, ['running'], {
          status: 'finished',
          updatedAt: at,
        }),
      ).toBe(false)
      expect(
        await storage.matches.transition(match.id, ['lobby'], {
          status: 'running',
          seed: 42,
          currentTurn: 1,
          updatedAt: at,
        }),
      ).toBe(true)
      const stored = await storage.matches.get(match.id)
      expect(stored).toMatchObject({ status: 'running', seed: 42, currentTurn: 1, updatedAt: at })
      expect(stored?.hostPlayerId).toBe(match.hostPlayerId)
    })

    it('deletes an inactive match with everything it owns, and leaves live ones alone', async () => {
      // Ancient timestamps and a cutoff well before every other fixture, so a shared database's
      // other rows can never be in scope of this sweep.
      const ancient = new Date('2020-01-01T00:00:00.000Z')
      const cutoff = new Date('2021-01-01T00:00:00.000Z')
      const doomed = matchFixture({ status: 'finished', updatedAt: ancient })
      const running = matchFixture({ status: 'running', updatedAt: ancient })
      const recent = matchFixture({ status: 'finished' })
      for (const match of [doomed, running, recent]) await storage.matches.create(match)
      const player = playerFixture(doomed)
      await storage.players.create(player)
      await storage.turns.open(turnFixture(doomed, 1), [player.id])
      await storage.turns.upsertReport({
        matchId: doomed.id,
        turn: 1,
        playerId: player.id,
        stateHash: 'a'.repeat(64),
        finished: true,
        reportedAt: ancient,
      })
      await storage.snapshots.put({
        matchId: doomed.id,
        turn: 1,
        formatVersion: 1,
        stateHash: 'a'.repeat(64),
        uploadedByPlayerId: player.id,
        uploadedAt: ancient,
        body: 'QUJD',
      })
      await storage.events.append({
        matchId: doomed.id,
        type: 'turn.opened',
        payload: { turn: 1, deadlineAt: null },
        createdAt: ancient.toISOString(),
      })

      expect(
        await storage.matches.deleteInactive(['finished', 'abandoned', 'lobby'], cutoff, 100),
      ).toBe(1)
      expect(await storage.matches.get(doomed.id)).toBeNull()
      expect(await storage.players.get(player.id)).toBeNull()
      expect(await storage.turns.get(doomed.id, 1)).toBeNull()
      expect(await storage.turns.listOrders(doomed.id, 1)).toEqual([])
      expect(await storage.turns.listReports(doomed.id, 1)).toEqual([])
      expect(await storage.snapshots.get(doomed.id, 1)).toBeNull()
      expect(await storage.events.listAfter(doomed.id, 0, 10)).toEqual([])
      expect(await storage.matches.get(running.id)).not.toBeNull()
      expect(await storage.matches.get(recent.id)).not.toBeNull()
    })

    it('lists public lobbies with the host name, newest first, and hides private or started ones', async () => {
      const host = uid('host')
      const visible = matchFixture({
        hostPlayerId: host,
        createdAt: new Date('2026-03-01T12:00:00.000Z'),
      })
      const older = matchFixture({
        createdAt: new Date('2026-03-01T09:00:00.000Z'),
        passwordHash: 'x',
      })
      const hidden = matchFixture({ settings: { ...visible.settings, visibility: 'private' } })
      const started = matchFixture({ status: 'running' })
      for (const match of [visible, older, hidden, started]) {
        await storage.matches.create(match)
        await storage.players.create(
          playerFixture(match, { id: match.hostPlayerId, displayName: `host of ${match.id}` }),
        )
      }
      // Filtered to this test's rows: the listing is a global query, and a database that outlives a
      // single run (the shared D1 instance, a Postgres service reused between runs) holds others.
      const mine = new Set([visible.id, older.id, hidden.id, started.id])
      const listing = (await storage.matches.listPublicLobbies(100)).filter((entry) =>
        mine.has(entry.id),
      )
      expect(listing.map((entry) => entry.id)).toEqual([visible.id, older.id])
      expect(listing[0]).toMatchObject({
        name: 'Conformance',
        hostDisplayName: `host of ${visible.id}`,
        playerCount: 1,
        maxPlayers: 2,
        passwordProtected: false,
        createdAt: '2026-03-01T12:00:00.000Z',
      })
      expect(listing[1]?.passwordProtected).toBe(true)
    })

    it('finds players by token hash, orders them by slot then join order, and updates status/slots', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const a = playerFixture(match, { joinOrder: 2 })
      const b = playerFixture(match, { joinOrder: 1 })
      await storage.players.create(a)
      await storage.players.create(b)
      expect(await storage.players.getByTokenHash(a.tokenHash as string)).toEqual(a)
      expect(await storage.players.getByTokenHash('missing')).toBeNull()
      expect((await storage.players.listByMatch(match.id)).map((p) => p.id)).toEqual([b.id, a.id])
      await storage.players.assignSlots([
        { playerId: a.id, slot: 0 },
        { playerId: b.id, slot: 1 },
      ])
      await storage.players.setStatus(b.id, 'kicked')
      const listed = await storage.players.listByMatch(match.id)
      expect(listed.map((p) => [p.id, p.slot, p.status])).toEqual([
        [a.id, 0, 'active'],
        [b.id, 1, 'kicked'],
      ])
      await storage.players.delete(b.id)
      expect(await storage.players.get(b.id)).toBeNull()
    })

    it('revokes a token so it resolves to nobody, without touching the player', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const player = playerFixture(match)
      await storage.players.create(player)
      await storage.players.revokeToken(player.id)
      expect(await storage.players.getByTokenHash(player.tokenHash as string)).toBeNull()
      expect(await storage.players.get(player.id)).toMatchObject({
        id: player.id,
        tokenHash: null,
        status: 'active',
      })
      // Two revoked memberships must coexist: a unique index over nulls would refuse the second.
      const second = playerFixture(match)
      await storage.players.create(second)
      await storage.players.revokeToken(second.id)
      expect((await storage.players.listByMatch(match.id)).length).toBe(2)
    })

    it('opens a turn with an empty orders row per player and accepts orders only while open', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const [a, b] = [playerFixture(match), playerFixture(match)]
      const turn = turnFixture(match, 1, { deadlineAt: new Date('2026-03-01T12:00:00.000Z') })
      expect(await storage.turns.open(turn, [a.id, b.id])).toBe(true)
      expect(await storage.turns.get(match.id, 1)).toEqual(turn)
      const empty = await storage.turns.listOrders(match.id, 1)
      expect(empty.map((row) => [row.playerId, row.ready, row.orders])).toEqual(
        [a, b]
          .map((p) => [p.id, false, null])
          .sort((x, y) => String(x[0]).localeCompare(String(y[0]))),
      )
      const submission = {
        orders: { schemaVersion: 1 as const, ops: [{ op: 'hire', args: { offer: 2 } }] },
        ordersHash: 'h'.repeat(64),
        ready: true,
        submittedAt: new Date('2026-03-01T11:30:00.000Z'),
      }
      expect(await storage.turns.submitOrders(match.id, 1, a.id, submission)).toBe(true)
      expect(await storage.turns.submitOrders(match.id, 1, 'stranger', submission)).toBe(false)
      expect(await storage.turns.getOrders(match.id, 1, a.id)).toEqual({
        matchId: match.id,
        turn: 1,
        playerId: a.id,
        ...submission,
      })
      expect(
        await storage.turns.transition(match.id, 1, ['open'], {
          status: 'sealed',
          sealedAt: new Date(),
        }),
      ).toBe(true)
      expect(await storage.turns.transition(match.id, 1, ['open'], { status: 'sealed' })).toBe(
        false,
      )
      expect(await storage.turns.submitOrders(match.id, 1, b.id, submission)).toBe(false)
      expect((await storage.turns.getOrders(match.id, 1, b.id))?.ready).toBe(false)
      await storage.turns.transition(match.id, 1, ['sealed'], {
        status: 'sealed',
        orderSetHash: 'o'.repeat(64),
        sealedSlots: [
          { playerId: a.id, slot: 0 },
          { playerId: b.id, slot: 1 },
        ],
      })
      const sealed = await storage.turns.get(match.id, 1)
      expect(sealed?.orderSetHash).toBe('o'.repeat(64))
      expect(sealed?.sealedSlots).toEqual([
        { playerId: a.id, slot: 0 },
        { playerId: b.id, slot: 1 },
      ])
    })

    it('refuses to re-open an existing turn but still fills a missing orders row', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const [a, b] = [playerFixture(match), playerFixture(match)]
      await storage.turns.open(turnFixture(match, 1), [a.id])
      // What a repaired seal does: the same open, now that the roster has one more row to create.
      expect(
        await storage.turns.open(turnFixture(match, 1, { status: 'sealed' }), [a.id, b.id]),
      ).toBe(false)
      expect((await storage.turns.get(match.id, 1))?.status).toBe('open')
      expect(
        (await storage.turns.listOrders(match.id, 1)).map((row) => row.playerId).sort(),
      ).toEqual([a.id, b.id].sort())
    })

    it('moves an open turn deadline and refuses once the turn is sealed', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      await storage.turns.open(turnFixture(match, 1), [])
      const later = new Date('2026-03-01T18:00:00.000Z')
      expect(await storage.turns.rescheduleDeadline(match.id, 1, later)).toBe(true)
      expect((await storage.turns.get(match.id, 1))?.deadlineAt).toEqual(later)
      expect(await storage.turns.rescheduleDeadline(match.id, 1, null)).toBe(true)
      expect((await storage.turns.get(match.id, 1))?.deadlineAt).toBeNull()
      await storage.turns.transition(match.id, 1, ['open'], { status: 'sealed' })
      expect(await storage.turns.rescheduleDeadline(match.id, 1, later)).toBe(false)
    })

    it('upserts reports per player and lists unsettled turns in order', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      await storage.turns.open(turnFixture(match, 1, { status: 'sealed' }), [])
      await storage.turns.open(turnFixture(match, 2, { status: 'desynced' }), [])
      await storage.turns.open(turnFixture(match, 3, { status: 'confirmed' }), [])
      await storage.turns.open(turnFixture(match, 4), [])
      expect((await storage.turns.listUnsettled(match.id)).map((t) => t.number)).toEqual([1, 2])
      const report = {
        matchId: match.id,
        turn: 1,
        playerId: 'p1',
        stateHash: 'a'.repeat(64),
        finished: false,
        reportedAt: new Date('2026-03-01T13:00:00.000Z'),
      }
      await storage.turns.upsertReport(report)
      await storage.turns.upsertReport({ ...report, stateHash: 'b'.repeat(64), finished: true })
      expect(await storage.turns.listReports(match.id, 1)).toEqual([
        { ...report, stateHash: 'b'.repeat(64), finished: true },
      ])
    })

    it('finds expired open turns oldest first and ignores timerless ones', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      await storage.turns.open(
        turnFixture(match, 1, { deadlineAt: new Date('2026-03-01T12:00:00.000Z') }),
        [],
      )
      await storage.turns.open(
        turnFixture(match, 2, { deadlineAt: new Date('2026-03-01T11:00:00.000Z') }),
        [],
      )
      await storage.turns.open(turnFixture(match, 3), [])
      await storage.turns.open(
        turnFixture(match, 4, { deadlineAt: new Date('2026-03-01T14:00:00.000Z') }),
        [],
      )
      const expired = await storage.turns.listExpiredOpen(new Date('2026-03-01T12:00:00.000Z'), 10)
      expect(expired.filter((t) => t.matchId === match.id).map((t) => t.number)).toEqual([2, 1])
    })

    it('finds the current turn of a live match that is no longer open', async () => {
      const stalled = matchFixture({ status: 'running', currentTurn: 2 })
      const healthy = matchFixture({ status: 'running', currentTurn: 1 })
      const over = matchFixture({ status: 'finished', currentTurn: 1 })
      for (const match of [stalled, healthy, over]) await storage.matches.create(match)
      await storage.turns.open(turnFixture(stalled, 1, { status: 'confirmed' }), [])
      await storage.turns.open(turnFixture(stalled, 2, { status: 'sealed' }), [])
      await storage.turns.open(turnFixture(healthy, 1), [])
      await storage.turns.open(turnFixture(over, 1, { status: 'confirmed' }), [])
      const stalls = await storage.turns.listStalledSeals(50)
      expect(stalls.filter((t) => t.matchId === stalled.id)).toEqual([
        { matchId: stalled.id, number: 2 },
      ])
      expect(stalls.filter((t) => t.matchId === healthy.id || t.matchId === over.id)).toEqual([])
    })

    it('stores snapshots per turn, replacing on re-upload, and serves the latest', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const base = {
        matchId: match.id,
        formatVersion: 3,
        stateHash: 'c'.repeat(64),
        uploadedByPlayerId: 'h',
        uploadedAt: new Date('2026-03-01T15:00:00.000Z'),
        body: 'QUJD',
      }
      await storage.snapshots.put({ ...base, turn: 1 })
      await storage.snapshots.put({ ...base, turn: 2, body: 'REVG' })
      await storage.snapshots.put({ ...base, turn: 2, body: 'R0hJ' })
      expect((await storage.snapshots.getLatest(match.id))?.body).toBe('R0hJ')
      expect((await storage.snapshots.get(match.id, 1))?.body).toBe('QUJD')
      expect(await storage.snapshots.get(match.id, 9)).toBeNull()
    })

    it('numbers appended events gaplessly, even when they are written concurrently', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const body = (turn: number) =>
        ({
          matchId: match.id,
          type: 'turn.opened',
          payload: { turn, deadlineAt: null },
          createdAt: '2026-03-01T16:00:00.000Z',
        }) as const
      expect(await storage.events.lastSeq(match.id)).toBe(0)

      // Concurrent appends contend for the same next number; every one of them must come back with a
      // distinct sequence and no hole, because a stream cursor that passed a hole would never
      // return to it.
      const appended = await Promise.all(
        [1, 2, 3, 4, 5].map((turn) => storage.events.append(body(turn))),
      )
      expect([...appended.map((event) => event.seq)].sort((a, b) => a - b)).toEqual([1, 2, 3, 4, 5])
      expect(await storage.events.lastSeq(match.id)).toBe(5)

      const all = await storage.events.listAfter(match.id, 0, 10)
      expect(all.map((event) => event.seq)).toEqual([1, 2, 3, 4, 5])
      expect(all.map((event) => (event.payload as { turn: number }).turn).sort()).toEqual([
        1, 2, 3, 4, 5,
      ])
      expect((await storage.events.listAfter(match.id, 1, 1)).map((e) => e.seq)).toEqual([2])
      expect(await storage.events.listAfter(match.id, 5, 10)).toEqual([])
      expect(await storage.events.lastSeq('missing')).toBe(0)
    })
  })
}
