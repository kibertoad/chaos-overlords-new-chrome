import type { OrderDocument } from '@chaos-overlords/contracts'
import { beforeEach, describe, expect, it } from 'vitest'
import { ConflictError, createKernel, type Kernel, type Principal } from '../src'
import {
  InMemoryStorage,
  ManualClock,
  RecordingLogger,
  RecordingNotifier,
  RecordingScheduler,
} from '../src/testing'

const orders = (n: number): OrderDocument => ({
  schemaVersion: 1,
  ops: [{ op: 'move', args: { n } }],
})
const HASH_A = 'a'.repeat(64)
const HASH_B = 'b'.repeat(64)

describe('multiplayer kernel', () => {
  let kernel: Kernel
  let storage: InMemoryStorage
  let clock: ManualClock
  let notifier: RecordingNotifier
  let scheduler: RecordingScheduler

  beforeEach(() => {
    storage = new InMemoryStorage()
    clock = new ManualClock()
    notifier = new RecordingNotifier()
    scheduler = new RecordingScheduler()
    kernel = createKernel({ storage, notifier, scheduler, clock, logger: new RecordingLogger() })
  })

  async function principalOf(token: string): Promise<Principal> {
    return kernel.auth.authenticate(token)
  }

  async function startedMatch(turnTimerSeconds = 0) {
    const host = await kernel.lobby.createMatch({
      settings: {
        name: 'Night City',
        maxPlayers: 3,
        turnTimerSeconds,
        visibility: 'public',
        gameSettings: { scenario: 3 },
      },
      hostDisplayName: 'Host',
    })
    const guest = await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    await kernel.lobby.start(await principalOf(host.token))
    return { host, guest }
  }

  it('creates a lobby, joins by code, and refuses a wrong password', async () => {
    const created = await kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
      password: 'secret1',
    })
    await expect(
      kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' }),
    ).rejects.toMatchObject({
      details: { reason: 'password_required' },
    })
    await expect(
      kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G', password: 'nope!!' }),
    ).rejects.toMatchObject({ details: { reason: 'wrong_password' } })
    const joined = await kernel.lobby.join({
      joinCode: created.joinCode,
      displayName: 'G',
      password: 'secret1',
    })
    expect(joined.match.players).toHaveLength(2)
    await expect(
      kernel.lobby.join({ joinCode: created.joinCode, displayName: 'Third', password: 'secret1' }),
    ).rejects.toMatchObject({ details: { reason: 'match_full' } })
  })

  it('start seats players deterministically, seeds the match and opens turn 1', async () => {
    const { host, guest } = await startedMatch()
    const view = await kernel.query.view((await principalOf(guest.token)).match)
    expect(view.status).toBe('running')
    expect(view.seed).not.toBeNull()
    expect(view.currentTurn).toBe(1)
    expect(view.players.map((p) => [p.displayName, p.slot])).toEqual([
      ['Host', 0],
      ['Guest', 1],
    ])
    expect(view.turn?.status).toBe('open')
    expect(notifier.events.map((e) => e.type)).toEqual([
      'lobby.playerJoined',
      'lobby.playerJoined',
      'match.started',
      'turn.opened',
    ])
    expect(host.player.isHost).toBe(true)
  })

  it('keeps orders private until every player is ready, then seals and opens the next turn', async () => {
    const { host, guest } = await startedMatch()
    const hostP = await principalOf(host.token)
    const guestP = await principalOf(guest.token)
    await kernel.turns.submitOrders(hostP, 1, { orders: orders(1), ready: true })
    await expect(kernel.query.sealedOrders(hostP.match, 1)).rejects.toBeInstanceOf(ConflictError)

    await kernel.turns.submitOrders(guestP, 1, { orders: orders(2), ready: false })
    expect(storage.statusOf(hostP.match.id)).toBe('running')
    await kernel.turns.submitOrders(guestP, 1, { orders: orders(3), ready: true })

    const sealed = await kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players.map((p) => [p.slot, p.orders.ops[0]?.args.n])).toEqual([
      [0, 1],
      [1, 3],
    ])
    expect(sealed.orderSetHash).toMatch(/^[0-9a-f]{64}$/)
    const refreshed = await principalOf(guest.token)
    expect(refreshed.match.currentTurn).toBe(2)
    await expect(
      kernel.turns.submitOrders(refreshed, 1, { orders: orders(9), ready: true }),
    ).rejects.toMatchObject({
      details: { reason: 'not_current_turn' },
    })
  })

  it('confirms a turn on unanimous hashes and finishes the match when all report the end', async () => {
    const { host, guest } = await startedMatch()
    await kernel.turns.submitOrders(await principalOf(host.token), 1, {
      orders: orders(1),
      ready: true,
    })
    await kernel.turns.submitOrders(await principalOf(guest.token), 1, {
      orders: orders(2),
      ready: true,
    })
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: true,
    })
    expect(storage.statusOf(host.match.id)).toBe('running')
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: true,
    })
    expect(storage.statusOf(host.match.id)).toBe('finished')
    const types = notifier.events.map((e) => e.type)
    expect(types).toContain('turn.confirmed')
    expect(types.at(-1)).toBe('match.statusChanged')
  })

  it('flags a desync, pauses the match, and recovers through the host snapshot', async () => {
    const { host, guest } = await startedMatch()
    await kernel.turns.submitOrders(await principalOf(host.token), 1, {
      orders: orders(1),
      ready: true,
    })
    await kernel.turns.submitOrders(await principalOf(guest.token), 1, {
      orders: orders(2),
      ready: true,
    })
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(storage.statusOf(host.match.id)).toBe('desynced')
    await expect(
      kernel.turns.submitOrders(await principalOf(guest.token), 2, {
        orders: orders(5),
        ready: true,
      }),
    ).rejects.toMatchObject({ details: { reason: 'match_desynced' } })

    await expect(
      kernel.snapshots.upload(await principalOf(guest.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
      }),
    ).rejects.toMatchObject({ details: { reason: 'host_only' } })
    await kernel.snapshots.upload(await principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
    })
    expect(storage.statusOf(host.match.id)).toBe('desynced')
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    expect(storage.statusOf(host.match.id)).toBe('running')
    expect((await kernel.snapshots.latest(host.match.id)).body).toBe('AAAA')
    const types = notifier.events.map((e) => e.type)
    expect(types.filter((t) => t === 'turn.desynced')).toHaveLength(1)
    expect(types.at(-1)).toBe('match.statusChanged')
  })

  it('seals on the deadline through the scheduler and the sweeper, only once', async () => {
    const { host } = await startedMatch(60)
    expect(scheduler.scheduled).toEqual([
      { matchId: host.match.id, turn: 1, dueAt: new Date(clock.now().getTime() + 60_000) },
    ])
    expect(await kernel.turns.trySeal(host.match.id, 1, 'deadline')).toBe(false)
    clock.advance(60_000)
    expect(await kernel.turns.sweepExpiredTurns()).toBe(1)
    expect(await kernel.turns.trySeal(host.match.id, 1, 'deadline')).toBe(false)
    expect((await principalOf(host.token)).match.currentTurn).toBe(2)
    expect(scheduler.scheduled).toHaveLength(2)
  })

  it('kicking the straggler completes readiness, and a leaving host hands over', async () => {
    const { host, guest } = await startedMatch()
    const third = await kernel.lobby
      .join({ joinCode: host.joinCode, displayName: 'Third' })
      .catch(() => null)
    expect(third).toBeNull() // already started
    await kernel.turns.submitOrders(await principalOf(host.token), 1, {
      orders: orders(1),
      ready: true,
    })
    await kernel.lobby.kick(await principalOf(host.token), guest.player.id)
    expect((await principalOf(host.token)).match.currentTurn).toBe(2)
    await expect(
      kernel.turns.submitOrders(await principalOf(guest.token), 2, {
        orders: orders(1),
        ready: true,
      }),
    ).rejects.toMatchObject({
      details: { reason: 'not_active' },
    })
    await kernel.lobby.leave(await principalOf(host.token))
    expect(storage.statusOf(host.match.id)).toBe('abandoned')
  })

  it('a host leaving the lobby abandons it; a guest leaving frees the seat', async () => {
    const created = await kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    const guest = await kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    await kernel.lobby.leave(await principalOf(guest.token))
    expect((await kernel.query.listPublicLobbies(10))[0]?.playerCount).toBe(1)
    await kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G2' })
    await kernel.lobby.leave(await principalOf(created.token))
    expect(storage.statusOf(created.match.id)).toBe('abandoned')
    expect(await kernel.query.listPublicLobbies(10)).toEqual([])
  })
})
