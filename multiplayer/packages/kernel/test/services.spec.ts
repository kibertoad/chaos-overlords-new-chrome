import type { OrderDocument } from '@chaos-overlords/contracts'
import { beforeEach, describe, expect, it } from 'vitest'
import { ConflictError, createKernel, hashOrderSet, type Kernel, type Principal } from '../src'
import {
  InMemoryStorage,
  ManualClock,
  RecordingLogger,
  RecordingNotifier,
  RecordingScheduler,
  RecordingStreamCloser,
} from '../src/testing'

/**
 * A one-op order document for a principal, distinguishable by `gang`. Ops carry the submitter's own
 * slot, because the server refuses any that name another player.
 */
const ordersFor = (principal: Principal, gang: number): OrderDocument => ({
  schemaVersion: 1,
  ops: [{ op: 'cancelCommand', player: principal.player.slot, gang }],
})
const gangOf = (document: OrderDocument): number | undefined => {
  const op = document.ops[0]
  return op && 'gang' in op ? op.gang : undefined
}
const HASH_A = 'a'.repeat(64)
const HASH_B = 'b'.repeat(64)

describe('multiplayer kernel', () => {
  let kernel: Kernel
  let storage: InMemoryStorage
  let clock: ManualClock
  let notifier: RecordingNotifier
  let scheduler: RecordingScheduler
  let streams: RecordingStreamCloser

  beforeEach(() => {
    storage = new InMemoryStorage()
    clock = new ManualClock()
    notifier = new RecordingNotifier()
    scheduler = new RecordingScheduler()
    streams = new RecordingStreamCloser()
    kernel = createKernel({
      storage,
      notifier,
      scheduler,
      streams,
      clock,
      logger: new RecordingLogger(),
    })
  })

  /** Submit a one-op document for a principal, in the vocabulary the server accepts. */
  function submit(principal: Principal, turn: number, gang: number, ready: boolean) {
    return kernel.turns.submitOrders(principal, turn, {
      orders: ordersFor(principal, gang),
      ready,
    })
  }

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

  /** A started match of three, the smallest roster where a majority can outvote the host. */
  async function startedMatchOfThree(turnTimerSeconds = 0) {
    const host = await kernel.lobby.createMatch({
      settings: {
        name: 'Night City',
        maxPlayers: 3,
        turnTimerSeconds,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    const guest = await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    const third = await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Third' })
    await kernel.lobby.start(await principalOf(host.token))
    return { host, guest, third }
  }

  it('starts with one human and lets the host revise lobby settings', async () => {
    const host = await kernel.lobby.createMatch({
      settings: {
        name: 'Solo Online',
        maxPlayers: 6,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: { scenario: 1 },
      },
      hostDisplayName: 'Host',
    })
    await kernel.lobby.updateSettings(await principalOf(host.token), {
      ...host.match.settings,
      name: 'Named Session',
      turnTimerSeconds: 120,
      visibility: 'public',
      gameSettings: { scenario: 2, allowLateJoin: true },
    })
    await kernel.lobby.start(await principalOf(host.token))
    const started = await principalOf(host.token)
    expect(started.match.status).toBe('running')
    expect(started.match.settings.name).toBe('Named Session')
    expect(started.match.settings.turnTimerSeconds).toBe(120)
  })

  it('persists protocol provenance and defaults omitted legacy metadata to version 1', async () => {
    const create = (name: string, protocolVersion?: number) =>
      kernel.lobby.createMatch({
        settings: {
          name,
          maxPlayers: 2,
          turnTimerSeconds: 0,
          visibility: 'private',
          gameSettings: {},
        },
        hostDisplayName: 'Host',
        protocolVersion,
      })

    const legacy = await create('Legacy')
    expect((await principalOf(legacy.token)).match.protocolVersion).toBe(1)

    const current = await create('Current', 2)
    const currentPrincipal = await principalOf(current.token)
    expect(currentPrincipal.match.protocolVersion).toBe(2)
    await kernel.lobby.start(currentPrincipal)
    await kernel.snapshots.upload(await principalOf(current.token), {
      turn: 0,
      formatVersion: 1,
      protocolVersion: 2,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    expect((await storage.snapshots.get(current.match.id, 0))?.protocolVersion).toBe(2)

    await kernel.lobby.start(await principalOf(legacy.token))
    await kernel.snapshots.upload(await principalOf(legacy.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    expect((await storage.snapshots.get(legacy.match.id, 0))?.protocolVersion).toBe(1)
  })

  it('allows late joining only into a never-human computer slot', async () => {
    const host = await kernel.lobby.createMatch({
      settings: {
        name: 'Drop In',
        maxPlayers: 6,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: { allowLateJoin: true },
      },
      hostDisplayName: 'Host',
    })
    await kernel.lobby.start(await principalOf(host.token))
    await kernel.snapshots.upload(await principalOf(host.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [{ slot: 3, gangs: 4, sites: 5, sectors: 6 }],
    })
    const listing = (await kernel.query.listPublicLobbies(10)).find(
      (candidate) => candidate.id === host.match.id,
    )
    expect(listing?.availableSeatSummaries).toContainEqual({
      slot: 3,
      gangs: 4,
      sites: 5,
      sectors: 6,
    })
    const joined = await kernel.lobby.joinRunning({
      match: host.match.id,
      displayName: 'Late',
      slot: 3,
    })
    expect(joined.player.slot).toBe(3)
    expect(joined.player.status).toBe('active')
    await expect(
      kernel.lobby.joinRunning({
        match: host.joinCode,
        displayName: 'Other',
        slot: 3,
      }),
    ).rejects.toMatchObject({ details: { reason: 'seat_reserved' } })
    expect(notifier.events.at(-1)?.type).toBe('match.latePlayerJoined')
  })

  it('refuses a late join by id into a private match, and past maxPlayers', async () => {
    const host = await kernel.lobby.createMatch({
      settings: {
        name: 'Code Only',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: { allowLateJoin: true },
      },
      hostDisplayName: 'Host',
    })
    const guest = await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    await kernel.lobby.start(await principalOf(host.token))
    await kernel.snapshots.upload(await principalOf(host.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    // The id rides every event and the client's recovery file, so it cannot be the key to a
    // code-gated match. The code still is.
    await expect(
      kernel.lobby.joinRunning({ match: host.match.id, displayName: 'Late', slot: 4 }),
    ).rejects.toMatchObject({ details: { reason: 'unknown_match' } })
    // Two seats, two humans: the late-join door has no seat counter behind it, so it reads the
    // host's own limit or a two-player match grows to six.
    await expect(
      kernel.lobby.joinRunning({ match: host.joinCode, displayName: 'Late', slot: 4 }),
    ).rejects.toMatchObject({ details: { reason: 'match_full' } })
    expect(guest.player.slot).toBe(-1)
  })

  it('refuses a second player under a name already on the roster', async () => {
    const host = await kernel.lobby.createMatch({
      settings: {
        name: 'Impostors',
        maxPlayers: 4,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Ada',
    })
    // Case, spacing and the combining form of an accent are all the same name: the roster is the
    // only thing telling players apart, so a second Ada would make every vote a guess.
    for (const name of ['ada', 'ADA', ' Ada ']) {
      await expect(
        kernel.lobby.join({ joinCode: host.joinCode, displayName: name }),
      ).rejects.toMatchObject({ details: { reason: 'display_name_taken' } })
    }
    const guest = await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Grace' })
    expect(guest.player.displayName).toBe('Grace')
  })

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
    await submit(hostP, 1, 1, true)
    await expect(kernel.query.sealedOrders(hostP.match, 1)).rejects.toBeInstanceOf(ConflictError)

    await submit(guestP, 1, 2, false)
    expect(storage.statusOf(hostP.match.id)).toBe('running')
    await submit(guestP, 1, 3, true)

    const sealed = await kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players.map((p) => [p.slot, gangOf(p.orders)])).toEqual([
      [0, 1],
      [1, 3],
    ])
    expect(sealed.orderSetHash).toMatch(/^[0-9a-f]{64}$/)
    const refreshed = await principalOf(guest.token)
    expect(refreshed.match.currentTurn).toBe(2)
    await expect(submit(refreshed, 1, 9, true)).rejects.toMatchObject({
      details: { reason: 'not_current_turn' },
    })
  })

  it('confirms a turn on unanimous hashes and finishes the match when all report the end', async () => {
    const { host, guest } = await startedMatch()
    await submit(await principalOf(host.token), 1, 1, true)
    await submit(await principalOf(guest.token), 1, 2, true)
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

  it('publishes current seat summaries from the host turn report without another snapshot', async () => {
    const { host, guest } = await startedMatch()
    await submit(await principalOf(host.token), 1, 1, true)
    await submit(await principalOf(guest.token), 1, 2, true)
    const summaries = [{ slot: 3, gangs: 4, sites: 5, sectors: 6 }]

    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
      seatSummaries: [{ slot: 2, gangs: 1, sites: 1, sectors: 1 }],
    })
    expect((await storage.matches.get(host.match.id))?.settings.gameSettings.seatSummaries).toBe(
      undefined,
    )
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
      seatSummaries: summaries,
    })

    expect((await storage.matches.get(host.match.id))?.settings.gameSettings.seatSummaries).toEqual(
      summaries,
    )
    expect(await storage.snapshots.getLatest(host.match.id)).toBeNull()
  })

  it('flags a desync, pauses the match, and recovers through the host snapshot', async () => {
    const { host, guest } = await startedMatch()
    await submit(await principalOf(host.token), 1, 1, true)
    await submit(await principalOf(guest.token), 1, 2, true)
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(storage.statusOf(host.match.id)).toBe('desynced')
    await expect(submit(await principalOf(guest.token), 2, 5, true)).rejects.toMatchObject({
      details: { reason: 'match_desynced' },
    })

    await expect(
      kernel.snapshots.upload(await principalOf(guest.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'host_only' } })
    await kernel.snapshots.upload(await principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
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
    expect(await kernel.turns.sweep()).toEqual({ sealed: 1, repaired: 0 })
    expect(await kernel.turns.trySeal(host.match.id, 1, 'deadline')).toBe(false)
    expect((await principalOf(host.token)).match.currentTurn).toBe(2)
    expect(scheduler.scheduled).toHaveLength(2)
  })

  it('keeps an absent human seat when a player votes to wait, then cancels on return', async () => {
    const { host, guest } = await startedMatch(60)
    await submit(await principalOf(host.token), 1, 1, true)
    clock.advance(60_000)
    await kernel.turns.sweep()

    expect((await storage.players.get(guest.player.id))?.status).toBe('takeoverPending')
    expect(notifier.events.map((event) => event.type)).toContain('match.takeoverVoteRequested')
    await kernel.lobby.voteOnTakeover(await principalOf(host.token), guest.player.id, {
      decision: 'wait',
    })
    expect((await storage.players.get(guest.player.id))?.status).toBe('takeoverPending')

    await submit(await principalOf(guest.token), 2, 2, false)
    expect((await storage.players.get(guest.player.id))?.status).toBe('active')
    expect(notifier.events.map((event) => event.type)).toContain('match.takeoverVoteCancelled')
  })

  it('requires every present player to approve computer control exactly once', async () => {
    const { host, guest, third } = await startedMatchOfThree(60)
    await submit(await principalOf(host.token), 1, 1, true)
    await submit(await principalOf(third.token), 1, 3, true)
    clock.advance(60_000)
    await kernel.turns.sweep()

    await kernel.lobby.voteOnTakeover(await principalOf(host.token), guest.player.id, {
      decision: 'computer',
    })
    expect((await storage.players.get(guest.player.id))?.status).toBe('takeoverPending')
    await kernel.lobby.voteOnTakeover(await principalOf(third.token), guest.player.id, {
      decision: 'computer',
    })
    expect((await storage.players.get(guest.player.id))?.status).toBe('computer')
    expect(notifier.events.filter((event) => event.type === 'match.playerTakenOver')).toHaveLength(
      1,
    )
    await kernel.lobby.rejoin(await principalOf(guest.token))
    expect((await storage.players.get(guest.player.id))?.status).toBe('active')
    expect(notifier.events.at(-1)?.type).toBe('match.playerReturned')
    expect(notifier.events.at(-1)?.payload).toEqual({
      playerId: guest.player.id,
      replacedComputer: true,
    })
  })

  it('kicking the straggler completes readiness and preserves a match with nobody present', async () => {
    const { host, guest } = await startedMatch()
    const third = await kernel.lobby
      .join({ joinCode: host.joinCode, displayName: 'Third' })
      .catch(() => null)
    expect(third).toBeNull() // already started
    await submit(await principalOf(host.token), 1, 1, true)
    await kernel.lobby.kick(await principalOf(host.token), guest.player.id)
    expect((await principalOf(host.token)).match.currentTurn).toBe(2)
    // The kick revokes the membership, so the token stops resolving at all: a kicked player loses
    // the event stream and the sealed order sets of the turns that follow, not just the right to act.
    await expect(principalOf(guest.token)).rejects.toMatchObject({
      details: { reason: 'invalid_token' },
    })
    await kernel.lobby.leave(await principalOf(host.token))
    expect(storage.statusOf(host.match.id)).toBe('running')
  })

  it('lets former members rejoin and makes the first returning player host', async () => {
    const { host, guest } = await startedMatch()
    await kernel.lobby.leave(await principalOf(host.token))
    await kernel.lobby.leave(await principalOf(guest.token))

    expect(storage.statusOf(host.match.id)).toBe('running')
    await kernel.lobby.rejoin(await principalOf(guest.token))

    const detail = await principalOf(guest.token)
    expect(detail.player.status).toBe('active')
    expect(detail.match.hostPlayerId).toBe(guest.player.id)
    expect(notifier.events.map((event) => event.type)).toContain('match.playerReturned')
    expect(notifier.events.map((event) => event.type)).toContain('lobby.hostChanged')
  })

  it('keeps the host role with a host who only missed a deadline', async () => {
    const { host, guest } = await startedMatch(60)
    await kernel.lobby.leave(await principalOf(guest.token))
    // One missed timed turn and the host is `takeoverPending`: still connected, still playing.
    // Treating that as an empty seat would hand the role to any former member who called rejoin at
    // that moment, and the new host can kick the old one, which revokes their token for good.
    await storage.players.setStatus(host.player.id, 'takeoverPending')
    await kernel.lobby.rejoin(await principalOf(guest.token))
    expect((await principalOf(guest.token)).match.hostPlayerId).toBe(host.player.id)

    // A host who is actually gone is replaced, which is what the rule is for.
    await storage.players.setStatus(host.player.id, 'left')
    await kernel.lobby.leave(await principalOf(guest.token))
    await kernel.lobby.rejoin(await principalOf(guest.token))
    expect((await principalOf(guest.token)).match.hostPlayerId).toBe(guest.player.id)
  })

  /**
   * A kick reaches a seat in any state. `remove` returned at once for anything but `active`, so
   * the host was answered 204 while the target kept a working token, an open stream, and `rejoin`,
   * which turns away nobody but the kicked.
   */
  it('kicks a player who has already left, and keeps them out', async () => {
    const { host, guest } = await startedMatch()
    await kernel.lobby.leave(await principalOf(guest.token))
    expect((await storage.players.get(guest.player.id))?.status).toBe('left')

    await kernel.lobby.kick(await principalOf(host.token), guest.player.id)

    expect((await storage.players.get(guest.player.id))?.status).toBe('kicked')
    expect(streams.closed).toEqual([{ matchId: host.match.id, playerId: guest.player.id }])
    await expect(principalOf(guest.token)).rejects.toMatchObject({
      details: { reason: 'invalid_token' },
    })
  })

  it('hangs up the event streams of a membership it revokes', async () => {
    const { host, guest } = await startedMatch()
    await kernel.lobby.kick(await principalOf(host.token), guest.player.id)
    // The revoke stops the next request. A stream already open is never authenticated again, so it
    // has to be closed from this side or the kicked player reads the match for as long as they like.
    expect(streams.closed).toEqual([{ matchId: host.match.id, playerId: guest.player.id }])
    // Leaving is not a revoke: the membership survives so the player can rejoin their seat.
    await kernel.lobby.leave(await principalOf(host.token))
    expect(streams.closed).toHaveLength(1)
  })

  it('refuses a report and a routine full snapshot after a turn is confirmed', async () => {
    const { host, guest } = await startedMatch()
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
    for (const token of [host.token, guest.token]) {
      await kernel.turns.report(await principalOf(token), 1, { stateHash: HASH_A, finished: false })
    }
    // The verdict is in; a later report cannot change it or rewrite its evidence.
    await expect(
      kernel.turns.report(await principalOf(host.token), 1, { stateHash: HASH_B, finished: false }),
    ).rejects.toMatchObject({ details: { reason: 'turn_confirmed' } })

    // Ordinary recovery replays the stored order sets, so a full upload is not accepted here.
    await kernel.lobby.leave(await principalOf(guest.token))
    await expect(
      kernel.snapshots.upload(await principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_B,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'snapshot_not_required' } })
  })

  /**
   * A turn that has sealed but not settled is still counting its reports. `settle` judges every
   * report against a snapshot for that turn when there is one, and anything short of unanimity on
   * it answers `pending` — so a snapshot accepted here would put the turn's desync verdict out of
   * reach for good and leave it unsettled, which is what a desync pause waits on to lift.
   */
  it('refuses a full snapshot while a turn is still collecting reports', async () => {
    const { host, guest } = await startedMatch()
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })

    await expect(
      kernel.snapshots.upload(await principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'snapshot_not_required' } })

    // The disagreement is still free to surface.
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(notifier.events.some((event) => event.type === 'turn.desynced')).toBe(true)
    // And the repair the desync asks for is accepted.
    await kernel.snapshots.upload(await principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
  })

  /**
   * `availableSlots` listed every slot no player row held and ignored the host's own limit, while
   * `joinRunning` counts every such row against it: every join against those seats was refused.
   */
  it('advertises no late-join seats once the roster is at the host limit', async () => {
    const host = await kernel.lobby.createMatch({
      settings: {
        name: 'Two Up',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: { allowLateJoin: true },
      },
      hostDisplayName: 'Host',
    })
    await kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    await kernel.lobby.start(await principalOf(host.token))
    await kernel.snapshots.upload(await principalOf(host.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })

    const listing = (await kernel.query.listPublicLobbies(10)).find(
      (candidate) => candidate.id === host.match.id,
    )

    expect(listing?.availableSlots).toEqual([])
    expect(listing?.availableSeatSummaries).toEqual([])
    await expect(
      kernel.lobby.joinRunning({ match: host.match.id, displayName: 'Late', slot: 4 }),
    ).rejects.toMatchObject({ details: { reason: 'match_full' } })
  })

  it('refuses seat summaries that would push the settings blob past its cap', async () => {
    const host = await kernel.lobby.createMatch({
      settings: {
        name: 'Fat Settings',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: { filler: 'x'.repeat(8 * 1024 - 64) },
      },
      hostDisplayName: 'Host',
    })
    await kernel.lobby.start(await principalOf(host.token))
    // The merge writes through `json_set`, which skips the checks `createMatch` applies — and the
    // blob is served on every match read and in every public listing.
    await expect(
      kernel.snapshots.upload(await principalOf(host.token), {
        turn: 0,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [{ slot: 1, gangs: 1, sites: 1, sectors: 1 }],
      }),
    ).rejects.toMatchObject({ details: { reason: 'game_settings_too_large' } })
  })

  it('collects a running match nobody came back to, long after the ordinary window', async () => {
    const { host, guest } = await startedMatch()
    await kernel.lobby.leave(await principalOf(host.token))
    await kernel.lobby.leave(await principalOf(guest.token))
    expect(storage.statusOf(host.match.id)).toBe('running')

    // Thirty-one days is the terminated-match window and leaves it alone: the match is kept running
    // precisely so somebody can rejoin it.
    clock.advance(31 * 24 * 60 * 60 * 1000)
    expect(await kernel.retention.collect()).toBe(0)
    expect(storage.statusOf(host.match.id)).toBe('running')

    clock.advance(60 * 24 * 60 * 60 * 1000)
    expect(await kernel.retention.collect()).toBe(1)
    expect(storage.statusOf(host.match.id)).toBeUndefined()
  })

  it('serves a sealed set that re-hashes to the digest it was announced with', async () => {
    const { host, guest } = await startedMatch()
    // The guest plans, is then kicked, and the turn seals without them. The vote has not approved
    // computer control yet, so their orders must be in neither the set nor its digest.
    await submit(await principalOf(guest.token), 1, 2, false)
    await kernel.lobby.kick(await principalOf(host.token), guest.player.id)
    await submit(await principalOf(host.token), 1, 1, true)

    const hostP = await principalOf(host.token)
    const sealed = await kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players.map((row) => row.playerId)).toEqual([host.player.id])
    expect(
      await hashOrderSet(
        sealed.players.map((row) => ({ slot: row.slot, ordersHash: row.ordersHash })),
      ),
    ).toBe(sealed.orderSetHash)
  })

  it('keeps a departed player in the set of a turn that sealed before they left', async () => {
    const { host, guest } = await startedMatch()
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
    await kernel.lobby.kick(await principalOf(host.token), guest.player.id)
    const hostP = await principalOf(host.token)
    const sealed = await kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players.map((row) => row.slot)).toEqual([0, 1])
    expect(
      await hashOrderSet(
        sealed.players.map((row) => ({ slot: row.slot, ordersHash: row.ordersHash })),
      ),
    ).toBe(sealed.orderSetHash)
  })

  it('numbers the event log without gaps when two players act at the same time', async () => {
    const { host, guest } = await startedMatch()
    const [hostP, guestP] = await Promise.all([principalOf(host.token), principalOf(guest.token)])
    await Promise.all([submit(hostP, 1, 1, true), submit(guestP, 1, 2, true)])
    const log = await storage.events.listAfter(hostP.match.id, 0, 100)
    // Contiguous from 1: a hole would be a sequence number a stream cursor has already passed.
    expect(log.map((event) => event.seq)).toEqual(log.map((_, index) => index + 1))
    expect(await storage.events.lastSeq(hostP.match.id)).toBe(log.length)
    const types = log.map((event) => event.type)
    expect(types.filter((type) => type === 'turn.readiness')).toHaveLength(2)
    expect(types.filter((type) => type === 'turn.sealed')).toHaveLength(1)
    expect(notifier.events.map((event) => event.seq)).toEqual(log.map((event) => event.seq))
  })

  it('finishes a seal that was interrupted before the next turn opened', async () => {
    const { host, guest } = await startedMatch()
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, false)
    }
    // What a process dying mid-seal leaves behind: turn 1 sealed, no digest, no turn 2.
    expect(
      await storage.turns.transition(host.match.id, 1, ['open'], {
        status: 'sealed',
        sealedAt: clock.now(),
      }),
    ).toBe(true)
    expect((await principalOf(host.token)).match.currentTurn).toBe(1)

    expect(await kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })

    const hostP = await principalOf(host.token)
    expect(hostP.match.currentTurn).toBe(2)
    const sealed = await kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players).toHaveLength(2)
    expect(notifier.events.map((event) => event.type)).toContain('turn.sealed')
    // And a second sweep has nothing left to do.
    expect(await kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
  })

  it('restarts the open turn clock when a desync pause lifts', async () => {
    const { host, guest } = await startedMatch(60)
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(storage.statusOf(host.match.id)).toBe('desynced')

    // The pause outlasts the turn timer; orders are refused throughout, so the turn must not seal
    // empty the moment the match resumes.
    clock.advance(120_000)
    await kernel.snapshots.upload(await principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    expect(storage.statusOf(host.match.id)).toBe('running')

    const open = await storage.turns.get(host.match.id, 2)
    expect(open?.deadlineAt).toEqual(new Date(clock.now().getTime() + 60_000))
    expect(notifier.events.map((event) => event.type)).toContain('turn.deadlineExtended')
    expect(scheduler.scheduled.at(-1)).toEqual({
      matchId: host.match.id,
      turn: 2,
      dueAt: open?.deadlineAt,
    })
    expect(await kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
  })

  it('collects matches past their retention age, and never a live one', async () => {
    const { host } = await startedMatch()
    const abandoned = await kernel.lobby.createMatch({
      settings: {
        name: 'empty',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Nobody',
    })
    await kernel.lobby.leave(await principalOf(abandoned.token))
    expect(storage.statusOf(abandoned.match.id)).toBe('abandoned')

    expect(await kernel.retention.collect()).toBe(0)
    clock.advance(31 * 24 * 60 * 60 * 1000)
    expect(await kernel.retention.collect()).toBe(1)
    expect(storage.statusOf(abandoned.match.id)).toBeUndefined()
    expect(storage.statusOf(host.match.id)).toBe('running')
  })

  it('gives the seat back when the player row cannot be written', async () => {
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
    // The seat is claimed before the row exists, so a failure in between would otherwise shrink the
    // lobby's capacity for good.
    const create = storage.players.create
    storage.players.create = async () => {
      throw new Error('disk on fire')
    }
    await expect(
      kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' }),
    ).rejects.toThrow('disk on fire')
    storage.players.create = create
    expect((await kernel.query.listPublicLobbies(10))[0]?.playerCount).toBe(1)
    const recovered = await kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    expect(recovered.player.displayName).toBe('G')
  })

  it('refuses routine full snapshots even when they match a confirmed turn', async () => {
    const { host, guest } = await startedMatch()
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
    for (const token of [host.token, guest.token]) {
      await kernel.turns.report(await principalOf(token), 1, {
        stateHash: HASH_A,
        finished: false,
      })
    }
    const upload = async (stateHash: string) =>
      kernel.snapshots.upload(await principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash,
        body: 'AAAA',
        seatSummaries: [],
      })
    // Neither matching nor contradictory bytes belong on the normal confirmed-turn path.
    await expect(upload(HASH_A)).rejects.toMatchObject({
      details: { reason: 'snapshot_not_required' },
    })
    await expect(upload(HASH_B)).rejects.toMatchObject({
      details: { reason: 'snapshot_not_required' },
    })
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

  /**
   * The submitted slot is the one every client applies the orders under, so an op naming another
   * player would make the two attributions disagree. The server is the only party that knows the
   * submitter's slot for certain, so it is the only one that can refuse this.
   */
  it('refuses orders that act for another slot', async () => {
    const { host } = await startedMatch()
    const hostP = await principalOf(host.token)
    expect(hostP.player.slot).toBe(0)
    await expect(
      kernel.turns.submitOrders(hostP, 1, {
        orders: { schemaVersion: 1, ops: [{ op: 'cancelCommand', player: 1, gang: 4 }] },
        ready: true,
      }),
    ).rejects.toMatchObject({ details: { reason: 'foreign_slot_ops' } })
    // The refusal leaves nothing behind: no orders, and no readiness that could seal the turn.
    expect((await kernel.query.ownSubmission(hostP.match, hostP.player.id, 1)).orders).toBeNull()
    expect(storage.statusOf(hostP.match.id)).toBe('running')
  })

  /**
   * A recovery snapshot becomes the hash everyone converges on, so a host who desynced deliberately
   * must not be able to name a state of its own invention.
   */
  it('refuses a recovery snapshot no majority of players reported', async () => {
    const { host, guest, third } = await startedMatchOfThree()
    for (const token of [host.token, guest.token, third.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
    // Two honest clients agree; the host is the odd one out.
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await kernel.turns.report(await principalOf(third.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(storage.statusOf(host.match.id)).toBe('desynced')
    const desync = notifier.events.find((event) => event.type === 'turn.desynced')
    expect(desync?.payload).toMatchObject({ candidateStateHashes: [HASH_A] })

    const upload = async (stateHash: string) =>
      kernel.snapshots.upload(await principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash,
        body: 'AAAA',
        seatSummaries: [],
      })
    await expect(upload(HASH_B)).rejects.toMatchObject({
      details: { reason: 'uncorroborated_state_hash', candidateStateHashes: [HASH_A] },
    })
    // The majority's hash is allowed, and settles the turn once the host re-reports against it.
    await expect(upload(HASH_A)).resolves.toBeUndefined()
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    expect(storage.statusOf(host.match.id)).toBe('running')
  })

  it('lets the host break a genuine tie, which is all a two-player desync can be', async () => {
    const { host, guest } = await startedMatch()
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
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
      kernel.snapshots.upload(await principalOf(host.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).resolves.toBeUndefined()
  })

  /**
   * Retention only collects matches that are over, so a long match that desyncs repeatedly would
   * otherwise hold a megabyte of base64 per turn with nothing to stop it.
   */
  it('keeps only the recent snapshots of a live match', async () => {
    const { host, guest } = await startedMatch()
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
    const hostP = await principalOf(host.token)
    // Turn 1 sealed, so turns 0..1 may be snapshotted; drive currentTurn up to make room for more.
    for (let turn = 0; turn <= 7; turn += 1) {
      await storage.snapshots.put({
        matchId: hostP.match.id,
        turn,
        formatVersion: 1,
        protocolVersion: 1,
        stateHash: HASH_A,
        uploadedByPlayerId: hostP.player.id,
        uploadedAt: clock.now(),
        body: 'AAAA',
      })
    }
    expect(await storage.snapshots.prune(hostP.match.id, 5)).toBe(3)
    const remaining = []
    for (let turn = 0; turn <= 7; turn += 1) {
      if (await storage.snapshots.get(hostP.match.id, turn)) remaining.push(turn)
    }
    expect(remaining).toEqual([3, 4, 5, 6, 7])
    // The newest is what a reconnecting client bootstraps from, so it must survive.
    expect((await storage.snapshots.getLatest(hostP.match.id))?.turn).toBe(7)
  })

  it('does not store a confirmed-turn full snapshot', async () => {
    const { host, guest } = await startedMatch()
    await submit(await principalOf(host.token), 1, 1, true)
    await submit(await principalOf(guest.token), 1, 2, true)
    await kernel.turns.report(await principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await kernel.turns.report(await principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })

    await expect(
      kernel.snapshots.upload(await principalOf(host.token), {
        turn: 1,
        formatVersion: 23,
        stateHash: HASH_A,
        body: 'AUTOSAVE',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'snapshot_not_required' } })
    await expect(kernel.snapshots.latest(host.match.id)).rejects.toMatchObject({
      details: { reason: 'no_snapshot' },
    })
  })

  /**
   * The seal opens the successor before anyone has reported, so the turn that ends the match is
   * always followed by an open one. Leaving its deadline armed would have the sweeper chasing a
   * finished match forever, and a client counting down after the game ended.
   */
  it('disarms the open turn left behind when a match finishes', async () => {
    const { host, guest } = await startedMatch(60)
    for (const token of [host.token, guest.token]) {
      await submit(await principalOf(token), 1, 1, true)
    }
    expect((await storage.turns.get(host.match.id, 2))?.deadlineAt).toBeInstanceOf(Date)
    for (const token of [host.token, guest.token]) {
      await kernel.turns.report(await principalOf(token), 1, { stateHash: HASH_A, finished: true })
    }
    expect(storage.statusOf(host.match.id)).toBe('finished')
    expect((await storage.turns.get(host.match.id, 2))?.deadlineAt).toBeNull()
    clock.advance(120_000)
    expect(await kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
  })

  /**
   * The other way a match can be left with nothing to play: the status changed and the process died
   * before turn 1 existed. Same repair, driven from the match rather than from a turn row.
   */
  it('opens turn 1 for a match whose start was interrupted', async () => {
    const created = await kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    await kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    // What `start` leaves behind if it dies after the transition: running, but no turn at all.
    expect(
      await storage.matches.transition(created.match.id, ['lobby'], {
        status: 'running',
        seed: 1,
        currentTurn: 0,
        updatedAt: clock.now(),
      }),
    ).toBe(true)
    expect(await storage.turns.get(created.match.id, 1)).toBeNull()

    expect(await kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 1 })
    expect((await storage.turns.get(created.match.id, 1))?.status).toBe('open')
    expect((await kernel.auth.authenticate(created.token)).match.currentTurn).toBe(1)
    // Idempotent: a second sweep has nothing left to repair.
    expect(await kernel.turns.sweep()).toEqual({ sealed: 0, repaired: 0 })
  })

  /**
   * A seat claimed a moment before the host pressed start must not become an unseated player in a
   * running match: they would hold a seat, count towards readiness, and have no slot to play.
   */
  it('refuses a join that lands after the match has started', async () => {
    const created = await kernel.lobby.createMatch({
      settings: {
        name: 'x',
        maxPlayers: 4,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
    })
    await kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' })
    await kernel.lobby.start(await principalOf(created.token))
    // The same refusal an unknown code gets, so a scan of the code space cannot tell a code that
    // exists from one that does not.
    await expect(
      kernel.lobby.join({ joinCode: created.joinCode, displayName: 'Late' }),
    ).rejects.toMatchObject({ details: { reason: 'unknown_join_code' } })
    await expect(
      kernel.lobby.join({ joinCode: 'ZZZZZZZZ', displayName: 'Late' }),
    ).rejects.toMatchObject({ details: { reason: 'unknown_join_code' } })

    // And when the race is lost inside the window: the seat is claimed while the lobby is open, the
    // match starts, and only then does the row get written. Capacity and the roster stay honest.
    const running = (await principalOf(created.token)).match
    expect(await storage.matches.claimSeat(running.id)).toBeNull()
    const roster = await storage.players.listByMatch(running.id)
    expect(roster.every((player) => player.slot >= 0)).toBe(true)
    expect(roster).toHaveLength(2)
  })
})
