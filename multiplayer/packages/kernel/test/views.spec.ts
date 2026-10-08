import { describe, expect, it } from 'vitest'
import { FakeTurnResolver, fakeSealedHash, fakeStartHash, readFakeSeatView } from '../src/testing'
import { createHarness, HASH_A, type Harness } from './harness'

/**
 * Matches played from per-seat views (docs/MULTIPLAYER.md, "Per-seat views"): the server resolves
 * every turn alone and serves each seat its own view, against a resolver whose rules are a digest
 * chain. What is under test is which mode a match gets, what each seat may read while it runs and
 * after it ends, and how the match waits for a resolver that fails.
 */
describe('matches played from seat views', () => {
  const viewServer = (
    options: ConstructorParameters<typeof FakeTurnResolver>[0] = {},
    seatViews = true,
  ) => {
    const resolver = new FakeTurnResolver(options)
    return { h: createHarness({ resolver, seatViews }), resolver }
  }

  const seedOf = async (h: Harness, matchId: string) => {
    const seed = (await h.storage.matches.get(matchId))?.seed
    if (seed === null || seed === undefined) throw new Error('the match has no seed')
    return seed
  }

  /** Every listed seat submits and readies `turn`; returns the frozen digest of the set. */
  const playTurn = async (h: Harness, tokens: string[], turn: number) => {
    let matchId = ''
    for (const [i, token] of tokens.entries()) {
      const principal = await h.principalOf(token)
      matchId = principal.match.id
      await h.submit(principal, turn, i + 1, true)
    }
    const row = await h.storage.turns.get(matchId, turn)
    if (!row?.orderSetHash) throw new Error(`turn ${turn} did not seal`)
    return row.orderSetHash
  }

  const viewOf = async (h: Harness, token: string) => {
    const view = await h.kernel.views.seatView(await h.principalOf(token))
    return { ...view, held: readFakeSeatView(view.body) }
  }

  const eventsOf = (h: Harness, type: string) =>
    h.notifier.events.filter((event) => event.type === type)

  it('stamps the mode at creation, only where the resolver plays the session version', async () => {
    const on = viewServer()
    const { host } = await on.h.startedMatch()
    expect((await on.h.storage.matches.get(host.match.id))?.seatViews).toBe(true)
    expect(host.match.seatViews).toBe(true)

    const other = await on.h.kernel.lobby.createMatch({
      settings: {
        name: 'Elsewhere',
        maxPlayers: 2,
        turnTimerSeconds: 0,
        visibility: 'private',
        gameSettings: {},
      },
      hostDisplayName: 'Host',
      sessionVersion: 2,
    })
    expect((await on.h.storage.matches.get(other.match.id))?.seatViews).toBe(false)

    const off = viewServer({}, false)
    const lockstep = await off.h.startedMatch()
    expect((await off.h.storage.matches.get(lockstep.host.match.id))?.seatViews).toBe(false)
  })

  it('withholds the seed, other seats’ orders and the whole state while the match runs', async () => {
    const { h } = viewServer()
    const { host, guest } = await h.startedMatch()
    const hostP = await h.principalOf(host.token)

    expect(eventsOf(h, 'match.started')[0]?.payload).toMatchObject({ seed: null })
    const view = await h.kernel.query.view(hostP.match)
    expect(view).toMatchObject({ seed: null, seatViews: true, refereed: true })

    await playTurn(h, [host.token, guest.token], 1)
    const withheld = { details: { reason: 'withheld_until_end' } }
    await expect(h.kernel.query.memberSealedOrders(hostP.match, 1)).rejects.toMatchObject(withheld)
    await expect(h.kernel.snapshots.memberLatest(hostP.match)).rejects.toMatchObject(withheld)
    await expect(h.kernel.snapshots.memberGet(hostP.match, 1)).rejects.toMatchObject(withheld)
    // A seat still reads its own document.
    const own = await h.kernel.query.ownSubmission(hostP.match, hostP.player.id, 1)
    expect(own.ready).toBe(true)
    await expect(
      h.kernel.turns.report(hostP, 1, { stateHash: HASH_A, finished: false }),
    ).rejects.toMatchObject({ details: { reason: 'reports_not_taken' } })
    await expect(
      h.kernel.snapshots.upload(hostP, {
        turn: 0,
        formatVersion: 1,
        stateHash: HASH_A,
        body: 'AAAA',
        seatSummaries: [],
      }),
    ).rejects.toMatchObject({ details: { reason: 'snapshot_not_required' } })
  })

  it('serves each seat its own view of the open turn, resolved by the server alone', async () => {
    const { h } = viewServer()
    const { host, guest } = await h.startedMatch()
    const start = fakeStartHash(await seedOf(h, host.match.id))

    const first = await viewOf(h, host.token)
    expect(first).toMatchObject({ turn: 1, slot: 0, formatVersion: 1, sessionVersion: 1 })
    expect(first.held).toMatchObject({ slot: 0, turn: 1, stateHash: start })
    expect((await viewOf(h, guest.token)).held).toMatchObject({ slot: 1, turn: 1 })

    const digest = await playTurn(h, [host.token, guest.token], 1)
    const resolved = fakeSealedHash(start, digest)
    // The confirmation is what tells clients the next views can be read.
    expect(eventsOf(h, 'turn.confirmed').at(-1)?.payload).toEqual({
      turn: 1,
      stateHash: resolved,
    })
    const second = await viewOf(h, guest.token)
    expect(second).toMatchObject({ turn: 2, slot: 1 })
    expect(second.held).toMatchObject({ slot: 1, turn: 2, stateHash: resolved })
  })

  it('refuses a view to a seat out of the match and on a match that is not played from views', async () => {
    const { h } = viewServer({ outSlots: [1] })
    const { guest } = await h.startedMatch()
    await expect(h.kernel.views.seatView(await h.principalOf(guest.token))).rejects.toMatchObject({
      details: { reason: 'seat_out' },
    })

    const off = viewServer({}, false)
    const lockstep = await off.h.startedMatch()
    await expect(
      off.h.kernel.views.seatView(await off.h.principalOf(lockstep.host.token)),
    ).rejects.toMatchObject({ details: { reason: 'not_a_view_match' } })
  })

  it('starts a turn’s clock when the turn before it is resolved, not when it opens', async () => {
    const { h } = viewServer()
    const { host, guest } = await h.startedMatch(60)
    const turnOne = await h.storage.turns.get(host.match.id, 1)
    expect(turnOne?.deadlineAt).not.toBeNull()

    await playTurn(h, [host.token, guest.token], 1)
    expect(eventsOf(h, 'turn.opened').at(-1)?.payload).toEqual({ turn: 2, deadlineAt: null })
    const turnTwo = await h.storage.turns.get(host.match.id, 2)
    expect(turnTwo?.deadlineAt).not.toBeNull()
    expect(eventsOf(h, 'turn.deadlineExtended').at(-1)?.payload).toEqual({
      turn: 2,
      deadlineAt: turnTwo?.deadlineAt?.toISOString(),
    })
  })

  it('waits for a resolver that failed at the seal, and a view request carries the match on', async () => {
    const { h, resolver } = viewServer()
    const { host, guest } = await h.startedMatch(60)
    resolver.failure = new Error('resolver down')
    await playTurn(h, [host.token, guest.token], 1)

    // No report can settle it and no clock runs on a turn nobody can plan.
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('sealed')
    expect((await h.storage.turns.get(host.match.id, 2))?.deadlineAt).toBeNull()
    await expect(h.kernel.views.seatView(await h.principalOf(host.token))).rejects.toMatchObject({
      details: { reason: 'view_not_ready' },
    })
    await h.kernel.turns.sweep()
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('sealed')

    resolver.failure = null
    const view = await viewOf(h, host.token)
    expect(view.turn).toBe(2)
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('confirmed')
    expect((await h.storage.turns.get(host.match.id, 2))?.deadlineAt).not.toBeNull()
  })

  it('confirms a turn whose resolution was recorded but never settled', async () => {
    const { h, resolver } = viewServer()
    const { host, guest } = await h.startedMatch()
    resolver.failure = new Error('resolver down')
    await playTurn(h, [host.token, guest.token], 1)
    resolver.failure = null
    // The resolution is written to the turn row, but nothing settles it: the seal's settle was cut
    // short after the record.
    const hostP = await h.principalOf(host.token)
    await h.kernel.referee.resolveThrough(hostP.match, 1)
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('sealed')

    expect((await viewOf(h, host.token)).turn).toBe(2)
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('confirmed')
  })

  it('answers match_finished when the resolution a view request retries ends the match', async () => {
    const { h, resolver } = viewServer({ finishAfterTurn: 1 })
    const { host, guest } = await h.startedMatch()
    resolver.failure = new Error('resolver down')
    await playTurn(h, [host.token, guest.token], 1)
    const stale = await h.principalOf(host.token)
    resolver.failure = null

    await expect(h.kernel.views.seatView(stale)).rejects.toMatchObject({
      details: { reason: 'match_finished' },
    })
    expect(h.storage.statusOf(host.match.id)).toBe('finished')
  })

  it('answers match_not_running for an abandoned match, which a client tells from a finished one', async () => {
    const { h } = viewServer()
    const { host } = await h.startedMatch()
    await h.storage.matches.transition(host.match.id, ['running'], {
      status: 'abandoned',
      updatedAt: new Date(),
    })

    await expect(viewOf(h, host.token)).rejects.toMatchObject({
      details: { reason: 'match_not_running' },
    })
  })

  it('serves a view again after the resolver lost the match', async () => {
    const { h, resolver } = viewServer()
    const { host, guest } = await h.startedMatch()
    const digest = await playTurn(h, [host.token, guest.token], 1)
    resolver.matches.clear()

    const view = await viewOf(h, host.token)
    expect(view.held).toMatchObject({
      turn: 2,
      stateHash: fakeSealedHash(fakeStartHash(await seedOf(h, host.match.id)), digest),
    })
  })

  it('releases the seed, every sealed set and the final state once the match ends', async () => {
    const { h } = viewServer({ finishAfterTurn: 2 })
    const { host, guest } = await h.startedMatch()
    const tokens = [host.token, guest.token]
    await playTurn(h, tokens, 1)
    await playTurn(h, tokens, 2)
    expect(h.storage.statusOf(host.match.id)).toBe('finished')

    const hostP = await h.principalOf(host.token)
    const seed = await seedOf(h, host.match.id)
    expect((await h.kernel.query.view(hostP.match)).seed).toBe(seed)
    const sealed = await h.kernel.query.memberSealedOrders(hostP.match, 1)
    expect(sealed.players.map((row) => row.slot)).toEqual([0, 1])
    const final = await h.kernel.snapshots.memberLatest(hostP.match)
    const confirmed = await h.storage.turns.get(host.match.id, 2)
    expect(final).toMatchObject({ turn: 2, stateHash: confirmed?.stateHash })
    await expect(h.kernel.views.seatView(hostP)).rejects.toMatchObject({
      details: { reason: 'match_finished' },
    })
  })

  it('takes a late joiner without a starting snapshot and lists the match as joinable', async () => {
    const { h } = viewServer()
    const host = await h.kernel.lobby.createMatch({
      settings: {
        name: 'Open City',
        maxPlayers: 3,
        turnTimerSeconds: 0,
        visibility: 'public',
        gameSettings: { allowLateJoin: true },
      },
      hostDisplayName: 'Host',
    })
    await h.kernel.lobby.join({ joinCode: host.joinCode, displayName: 'Guest' })
    await h.kernel.lobby.start(await h.principalOf(host.token))

    const listing = (await h.kernel.query.listPublicLobbies(10)).find(
      (row) => row.id === host.match.id,
    )
    expect(listing?.availableSlots.length).toBeGreaterThan(0)
    const late = await h.kernel.lobby.joinRunning({
      match: host.match.id,
      displayName: 'Late',
      slot: 3,
    })
    const view = await viewOf(h, late.token)
    expect(view).toMatchObject({ turn: 1, slot: 3 })
  })
})
