import { beforeEach, describe, expect, it } from 'vitest'
import { ConflictError } from '../src'
import { createHarness, gangOf, HASH_A, HASH_B, type Harness } from './harness'

describe('the lobby, the roster and the turn barrier', () => {
  let h: Harness

  beforeEach(() => {
    h = createHarness()
  })

  it('starts with one human and lets the host revise lobby settings', async () => {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Solo Online',
        maxPlayers: 6,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: { scenario: 1 },
      },
      hostDisplayName: 'Host',
    })
    await h.kernel.lobby.updateSettings(await h.principalOf(host.token), {
      ...host.match.settings,
      name: 'Named Session',
      turnTimerSeconds: 120,
      visibility: 'public',
      gameSettings: { scenario: 2, allowLateJoin: true },
    })
    await h.kernel.lobby.start(await h.principalOf(host.token))
    const started = await h.principalOf(host.token)
    expect(started.match.status).toBe('running')
    expect(started.match.settings.name).toBe('Named Session')
    expect(started.match.settings.turnTimerSeconds).toBe(120)
  })

  it('persists protocol and session provenance, defaulting omitted legacy metadata to 1', async () => {
    const create = (name: string, protocolVersion?: number, sessionVersion?: number) =>
      h.kernel.lobby.createMatch({
        settings: {
          name,
          maxPlayers: 2,
          turnTimerSeconds: 0,
          visibility: 'private',
          gameSettings: {},
        },
        hostDisplayName: 'Host',
        protocolVersion,
        sessionVersion,
      })

    const legacy = await create('Legacy')
    const legacyMatch = (await h.principalOf(legacy.token)).match
    expect(legacyMatch.protocolVersion).toBe(1)
    expect(legacyMatch.sessionVersion).toBe(1)

    // A newer protocol over the same session shape: the pair is kept apart, not collapsed.
    const current = await create('Current', 2, 1)
    const currentPrincipal = await h.principalOf(current.token)
    expect(currentPrincipal.match.protocolVersion).toBe(2)
    expect(currentPrincipal.match.sessionVersion).toBe(1)
    await h.kernel.lobby.start(currentPrincipal)
    await h.kernel.snapshots.upload(await h.principalOf(current.token), {
      turn: 0,
      formatVersion: 1,
      protocolVersion: 2,
      sessionVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    const currentSnapshot = await h.storage.snapshots.get(current.match.id, 0)
    expect(currentSnapshot?.protocolVersion).toBe(2)
    expect(currentSnapshot?.sessionVersion).toBe(1)

    await h.kernel.lobby.start(await h.principalOf(legacy.token))
    await h.kernel.snapshots.upload(await h.principalOf(legacy.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    const legacySnapshot = await h.storage.snapshots.get(legacy.match.id, 0)
    expect(legacySnapshot?.protocolVersion).toBe(1)
    expect(legacySnapshot?.sessionVersion).toBe(1)
  })

  /**
   * The face is the one thing a player brings to the roster besides their name, and every client
   * generates that seat's overlord from it, so it has to survive create, join and the read back.
   */
  it('seats each player under the face they chose, and the first face when none was sent', async () => {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Faces',
        maxPlayers: 3,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
      hostPortraitId: 7,
    })
    const guest = await h.kernel.lobby.join({
      joinCode: host.joinCode,
      displayName: 'Guest',
      portraitId: 12,
    })
    const legacy = await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Legacy' })
    expect(host.player.portraitId).toBe(7)
    expect(guest.player.portraitId).toBe(12)
    expect(legacy.player.portraitId).toBe(0)
    const view = await h.kernel.query.view((await h.principalOf(guest.token)).match)
    expect(view.players.map((player) => [player.displayName, player.portraitId])).toEqual([
      ['Host', 7],
      ['Guest', 12],
      ['Legacy', 0],
    ])
  })

  it('allows late joining only into a never-human computer slot', async () => {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Drop In',
        maxPlayers: 6,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: { allowLateJoin: true },
      },
      hostDisplayName: 'Host',
    })
    await h.kernel.lobby.start(await h.principalOf(host.token))
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [{ slot: 3, gangs: 4, sites: 5, sectors: 6 }],
    })
    const listing = (await h.kernel.query.listPublicLobbies(10)).find(
      (candidate) => candidate.id === host.match.id,
    )
    expect(listing?.availableSeatSummaries).toContainEqual({
      slot: 3,
      gangs: 4,
      sites: 5,
      sectors: 6,
    })
    const joined = await h.kernel.lobby.joinRunning({
      match: host.match.id,
      displayName: 'Late',
      slot: 3,
      // The face that seat has worn since the match was generated, not one the latecomer picked.
      portraitId: 9,
    })
    expect(joined.player.slot).toBe(3)
    expect(joined.player.status).toBe('active')
    expect(joined.player.portraitId).toBe(9)
    await expect(
      h.kernel.lobby.joinRunning({
        match: host.joinCode,
        displayName: 'Other',
        slot: 3,
      }),
    ).rejects.toMatchObject({ details: { reason: 'seat_reserved' } })
    expect(h.notifier.events.at(-1)?.type).toBe('match.latePlayerJoined')
  })

  it('refuses a late join by id into a private match, and past maxPlayers', async () => {
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Code Only',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: { allowLateJoin: true },
      },
      hostDisplayName: 'Host',
    })
    const guest = await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    await h.kernel.lobby.start(await h.principalOf(host.token))
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 0,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    // The id rides every event and the client's recovery file, so it cannot be the key to a
    // code-gated match. The code still is.
    await expect(
      h.kernel.lobby.joinRunning({ match: host.match.id, displayName: 'Late', slot: 4 }),
    ).rejects.toMatchObject({ details: { reason: 'unknown_match' } })
    // Two seats, two humans: the late-join door has no seat counter behind it, so it reads the
    // host's own limit or a two-player match grows to six.
    await expect(
      h.kernel.lobby.joinRunning({ match: host.joinCode, displayName: 'Late', slot: 4 }),
    ).rejects.toMatchObject({ details: { reason: 'match_full' } })
    expect(guest.player.slot).toBe(-1)
  })

  it('refuses a second player under a name already on the roster', async () => {
    const host = await h.kernel.lobby.createMatch({
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
        h.kernel.lobby.join({ joinCode: host.joinCode, displayName: name }),
      ).rejects.toMatchObject({ details: { reason: 'display_name_taken' } })
    }
    const guest = await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Grace' })
    expect(guest.player.displayName).toBe('Grace')
  })

  it('creates a lobby, joins by code, and refuses a wrong password', async () => {
    const created = await h.kernel.lobby.createMatch({
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
      h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G' }),
    ).rejects.toMatchObject({
      details: { reason: 'password_required' },
    })
    await expect(
      h.kernel.lobby.join({ joinCode: created.joinCode, displayName: 'G', password: 'nope!!' }),
    ).rejects.toMatchObject({ details: { reason: 'wrong_password' } })
    const joined = await h.kernel.lobby.join({
      joinCode: created.joinCode,
      displayName: 'G',
      password: 'secret1',
    })
    expect(joined.match.players).toHaveLength(2)
    await expect(
      h.kernel.lobby.join({
        joinCode: created.joinCode,
        displayName: 'Third',
        password: 'secret1',
      }),
    ).rejects.toMatchObject({ details: { reason: 'match_full' } })
  })

  it('start seats players deterministically, seeds the match and opens turn 1', async () => {
    const { host, guest } = await h.startedMatch()
    const view = await h.kernel.query.view((await h.principalOf(guest.token)).match)
    expect(view.status).toBe('running')
    expect(view.seed).not.toBeNull()
    expect(view.currentTurn).toBe(1)
    expect(view.players.map((p) => [p.displayName, p.slot])).toEqual([
      ['Host', 0],
      ['Guest', 1],
    ])
    expect(view.turn?.status).toBe('open')
    expect(h.notifier.events.map((e) => e.type)).toEqual([
      'lobby.playerJoined',
      'lobby.playerJoined',
      'match.started',
      'turn.opened',
    ])
    expect(host.player.isHost).toBe(true)
  })

  it('keeps orders private until every player is ready, then seals and opens the next turn', async () => {
    const { host, guest } = await h.startedMatch()
    const hostP = await h.principalOf(host.token)
    const guestP = await h.principalOf(guest.token)
    await h.submit(hostP, 1, 1, true)
    await expect(h.kernel.query.sealedOrders(hostP.match, 1)).rejects.toBeInstanceOf(ConflictError)

    await h.submit(guestP, 1, 2, false)
    expect(h.storage.statusOf(hostP.match.id)).toBe('running')
    await h.submit(guestP, 1, 3, true)

    const sealed = await h.kernel.query.sealedOrders(hostP.match, 1)
    expect(sealed.players.map((p) => [p.slot, gangOf(p.orders)])).toEqual([
      [0, 1],
      [1, 3],
    ])
    expect(sealed.orderSetHash).toMatch(/^[0-9a-f]{64}$/)
    const refreshed = await h.principalOf(guest.token)
    expect(refreshed.match.currentTurn).toBe(2)
    // Losing the final HTTP response must not turn a safe retry into a false refusal. The exact
    // document is already durable and is acknowledged even though its first call advanced the turn.
    await expect(h.submit(refreshed, 1, 3, true)).resolves.toMatchObject({
      turn: 1,
      ready: true,
    })
    await expect(h.submit(refreshed, 1, 9, true)).rejects.toMatchObject({
      details: { reason: 'not_current_turn' },
    })
  })

  it('confirms a turn on unanimous hashes and finishes the match when all report the end', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: true,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('running')
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: true,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('finished')
    const types = h.notifier.events.map((e) => e.type)
    expect(types).toContain('turn.confirmed')
    expect(types.at(-1)).toBe('match.statusChanged')
  })

  it('publishes current seat summaries from the host turn report without another snapshot', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    const summaries = [{ slot: 3, gangs: 4, sites: 5, sectors: 6 }]

    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
      seatSummaries: [{ slot: 2, gangs: 1, sites: 1, sectors: 1 }],
    })
    expect((await h.storage.matches.get(host.match.id))?.settings.gameSettings.seatSummaries).toBe(
      undefined,
    )
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
      seatSummaries: summaries,
    })

    expect(
      (await h.storage.matches.get(host.match.id))?.settings.gameSettings.seatSummaries,
    ).toEqual(summaries)
    expect(await h.storage.snapshots.getLatest(host.match.id)).toBeNull()
  })

  it('flags a desync, pauses the match, and recovers through the host snapshot', async () => {
    const { host, guest } = await h.startedMatch()
    await h.submit(await h.principalOf(host.token), 1, 1, true)
    await h.submit(await h.principalOf(guest.token), 1, 2, true)
    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_B,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('desynced')
    await expect(h.submit(await h.principalOf(guest.token), 2, 5, true)).rejects.toMatchObject({
      details: { reason: 'match_desynced' },
    })

    await expect(
      h.kernel.snapshots.upload(await h.principalOf(guest.token), {
        turn: 1,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'host_only' } })
    await h.kernel.snapshots.upload(await h.principalOf(host.token), {
      turn: 1,
      formatVersion: 1,
      stateHash: HASH_A,
      body: 'AAAA',
      seatSummaries: [],
    })
    expect(h.storage.statusOf(host.match.id)).toBe('desynced')
    await h.kernel.turns.report(await h.principalOf(guest.token), 1, {
      stateHash: HASH_A,
      finished: false,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('running')
    expect((await h.kernel.snapshots.latest(host.match.id)).body).toBe('AAAA')
    const types = h.notifier.events.map((e) => e.type)
    expect(types.filter((t) => t === 'turn.desynced')).toHaveLength(1)
    expect(types.at(-1)).toBe('match.statusChanged')
  })

  it('seals on the deadline through the h.scheduler and the sweeper, only once', async () => {
    const { host, guest } = await h.startedMatch(60)
    await h.submit(await h.principalOf(host.token), 1, 1, false)
    await h.submit(await h.principalOf(guest.token), 1, 2, false)
    expect(h.scheduler.scheduled).toEqual([
      { matchId: host.match.id, turn: 1, dueAt: new Date(h.clock.now().getTime() + 60_000) },
    ])
    expect(await h.kernel.turns.trySeal(host.match.id, 1, 'deadline')).toBe(false)
    h.clock.advance(60_000)
    expect(await h.kernel.turns.sweep()).toEqual({ sealed: 1, repaired: 0 })
    expect(await h.kernel.turns.trySeal(host.match.id, 1, 'deadline')).toBe(false)
    expect((await h.principalOf(host.token)).match.currentTurn).toBe(2)
    expect(h.scheduler.scheduled).toHaveLength(2)
  })
})
