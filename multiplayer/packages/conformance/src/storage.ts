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
    eventSeq: 0,
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
    ...overrides,
  }
}

/**
 * Pins the atomicity contracts the services rely on. A dialect that maps a column differently, or
 * turns a conditional statement into a read-then-write, fails here rather than in a live race.
 */
export function defineStorageConformance(harness: StorageConformanceHarness): void {
  describe('storage conformance', () => {
    let storage: MultiplayerStorage

    beforeEach(async () => {
      storage = await harness.createStorage()
    })

    it('round-trips a match with its settings, dates and nullable columns', async () => {
      const match = matchFixture({ passwordHash: 'pbkdf2$1$aa$bb' })
      await storage.matches.create(match)
      expect(await storage.matches.get(match.id)).toEqual(match)
      expect(await storage.matches.getByJoinCode(match.joinCode)).toEqual(match)
      expect(await storage.matches.get('nope')).toBeNull()
    })

    it('claims seats atomically up to capacity and only while in the lobby', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      expect(await storage.matches.claimSeat(match.id)).toBe(true)
      expect(await storage.matches.claimSeat(match.id)).toBe(false)
      await storage.matches.releaseSeat(match.id)
      expect((await storage.matches.get(match.id))?.seatCount).toBe(1)
      await storage.matches.transition(match.id, ['lobby'], {
        status: 'running',
        updatedAt: new Date(),
      })
      expect(await storage.matches.claimSeat(match.id)).toBe(false)
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

    it('allocates strictly increasing event sequence numbers', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const seqs = await Promise.all(
        [1, 2, 3, 4, 5].map(() => storage.matches.allocateEventSeq(match.id)),
      )
      expect([...seqs].sort((a, b) => a - b)).toEqual([1, 2, 3, 4, 5])
      await expect(storage.matches.allocateEventSeq('missing')).rejects.toThrow()
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
      const listing = await storage.matches.listPublicLobbies(10)
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

    it('finds players by token hash, orders them by slot then join time, and updates status/slots', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const a = playerFixture(match, { joinedAt: new Date('2026-03-01T10:00:05.000Z') })
      const b = playerFixture(match, { joinedAt: new Date('2026-03-01T10:00:01.000Z') })
      await storage.players.create(a)
      await storage.players.create(b)
      expect(await storage.players.getByTokenHash(a.tokenHash)).toEqual(a)
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

    it('opens a turn with an empty orders row per player and accepts orders only while open', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const [a, b] = [playerFixture(match), playerFixture(match)]
      const turn = turnFixture(match, 1, { deadlineAt: new Date('2026-03-01T12:00:00.000Z') })
      await storage.turns.open(turn, [a.id, b.id])
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
      })
      expect((await storage.turns.get(match.id, 1))?.orderSetHash).toBe('o'.repeat(64))
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

    it('appends events, refuses a duplicate sequence, and pages after a sequence', async () => {
      const match = matchFixture()
      await storage.matches.create(match)
      const make = (seq: number) => ({
        matchId: match.id,
        seq,
        type: 'turn.opened' as const,
        payload: { turn: seq, deadlineAt: null },
        createdAt: '2026-03-01T16:00:00.000Z',
      })
      await storage.events.append(make(1))
      await storage.events.append(make(2))
      await storage.events.append(make(3))
      await expect(storage.events.append(make(2))).rejects.toThrow()
      expect((await storage.events.listAfter(match.id, 1, 1)).map((e) => e.seq)).toEqual([2])
      expect(await storage.events.listAfter(match.id, 1, 10)).toEqual([make(2), make(3)])
      expect(await storage.events.listAfter(match.id, 3, 10)).toEqual([])
    })
  })
}
