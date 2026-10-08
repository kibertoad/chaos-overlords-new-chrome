import { describe, expect, it } from 'vitest'
import { FakeTurnResolver, fakeHandoverHash, fakeSealedHash, fakeStartHash } from '../src/testing'
import { createHarness, HASH_B, type Harness } from './harness'

/**
 * The server resolving every sealed turn itself and deciding it on its own state
 * (docs/MULTIPLAYER.md, "Resolving turns on the server"), against a resolver whose rules are a
 * digest chain: what is under test is which state decides, when, and what each seat is told.
 */
describe('refereeing turns with the server resolver', () => {
  const refereed = (options: ConstructorParameters<typeof FakeTurnResolver>[0] = {}) => {
    const resolver = new FakeTurnResolver(options)
    return { h: createHarness({ resolver }), resolver }
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

  const report = async (h: Harness, token: string, turn: number, stateHash: string) =>
    h.kernel.turns.report(await h.principalOf(token), turn, { stateHash, finished: false })

  const eventsOf = (h: Harness, type: string) =>
    h.notifier.events.filter((event) => event.type === type)

  it('confirms a sealed turn on the server state before anyone reports', async () => {
    const { h } = refereed()
    const { host, guest } = await h.startedMatch()
    const tokens = [host.token, guest.token]
    const digest = await playTurn(h, tokens, 1)
    const expected = fakeSealedHash(fakeStartHash(await seedOf(h, host.match.id)), digest)

    const turn = await h.storage.turns.get(host.match.id, 1)
    expect(turn).toMatchObject({ status: 'confirmed', stateHash: expected, resolvedHash: expected })
    expect(eventsOf(h, 'turn.confirmed').at(-1)?.payload).toEqual({ turn: 1, stateHash: expected })
    const view = await h.kernel.query.view((await h.principalOf(host.token)).match)
    expect(view.refereed).toBe(true)

    // A report that agrees changes nothing and is told nothing.
    await report(h, host.token, 1, expected)
    expect(eventsOf(h, 'turn.diverged')).toEqual([])
  })

  it('keeps the seat summaries a host report carries on a turn confirmed at its seal', async () => {
    const { h } = refereed()
    const { host, guest } = await h.startedMatch()
    const digest = await playTurn(h, [host.token, guest.token], 1)
    const expected = fakeSealedHash(fakeStartHash(await seedOf(h, host.match.id)), digest)
    const seatSummaries = [{ slot: 1, gangs: 3, sites: 2, sectors: 4 }]

    await h.kernel.turns.report(await h.principalOf(host.token), 1, {
      stateHash: expected,
      finished: false,
      seatSummaries,
    })

    const settings = (await h.storage.matches.get(host.match.id))?.settings.gameSettings
    expect(settings).toMatchObject({ seatSummaries })
  })

  it('tells only the seat whose report differs, with the server snapshot stored first', async () => {
    const { h } = refereed()
    const { host, guest } = await h.startedMatch()
    const tokens = [host.token, guest.token]
    const digest = await playTurn(h, tokens, 1)
    const expected = fakeSealedHash(fakeStartHash(await seedOf(h, host.match.id)), digest)

    await report(h, guest.token, 1, HASH_B)
    await report(h, host.token, 1, expected)

    const diverged = eventsOf(h, 'turn.diverged')
    expect(diverged.map((event) => event.payload)).toEqual([
      { turn: 1, playerId: guest.player.id, stateHash: expected, reportedStateHash: HASH_B },
    ])
    const snapshot = await h.kernel.snapshots.get(host.match.id, 1)
    expect(snapshot).toMatchObject({ stateHash: expected, uploadedByPlayerId: 'server' })
    // Nobody is paused: the match runs on and the next turn takes orders.
    expect(h.storage.statusOf(host.match.id)).toBe('running')
    expect(eventsOf(h, 'turn.desynced')).toEqual([])
    await expect(h.submit(await h.principalOf(guest.token), 2, 1, false)).resolves.toBeDefined()
    // The same report again is told once.
    await report(h, guest.token, 1, HASH_B)
    expect(eventsOf(h, 'turn.diverged')).toHaveLength(1)
  })

  it('is not outvoted by several seats reporting the same doctored state', async () => {
    const { h } = refereed()
    const { host, guest, third } = await h.startedMatchOfThree()
    const tokens = [host.token, guest.token, third.token]
    const digest = await playTurn(h, tokens, 1)
    const expected = fakeSealedHash(fakeStartHash(await seedOf(h, host.match.id)), digest)

    await report(h, host.token, 1, HASH_B)
    await report(h, guest.token, 1, HASH_B)
    await report(h, third.token, 1, expected)

    expect((await h.storage.turns.get(host.match.id, 1))?.stateHash).toBe(expected)
    expect(
      eventsOf(h, 'turn.diverged')
        .map((event) => (event.payload as { playerId: string }).playerId)
        .sort(),
    ).toEqual([host.player.id, guest.player.id].sort())
    expect(h.storage.statusOf(host.match.id)).toBe('running')
  })

  it('takes the host start only when it is the state the server built', async () => {
    const { h } = refereed()
    const { host } = await h.startedMatch()
    const start = fakeStartHash(await seedOf(h, host.match.id))
    const upload = async (stateHash: string) =>
      h.kernel.snapshots.upload(await h.principalOf(host.token), {
        turn: 0,
        formatVersion: 1,
        stateHash,
        body: 'AAAA',
        seatSummaries: [],
      })

    await expect(upload(HASH_B)).rejects.toMatchObject({
      details: { reason: 'uncorroborated_state_hash', candidateStateHashes: [start] },
    })
    await expect(upload(start)).resolves.toBeUndefined()
    const stored = await h.kernel.snapshots.get(host.match.id, 0)
    expect(stored).toMatchObject({ stateHash: start, uploadedByPlayerId: 'server' })
    expect(stored.body).not.toBe('AAAA')
  })

  it('applies a handover once, in its place in the log, even when two callers feed together', async () => {
    const { h, resolver } = refereed()
    const { host, guest, third } = await h.startedMatchOfThree()
    const tokens = [host.token, guest.token, third.token]
    const seed = await seedOf(h, host.match.id)
    const first = await playTurn(h, tokens, 1)
    // The third seat goes to the computer before turn 2 seals.
    await h.kernel.lobby.leave(await h.principalOf(third.token))
    for (const voter of [host.token, guest.token]) {
      await h.kernel.lobby.voteOnTakeover(await h.principalOf(voter), third.player.id, {
        decision: 'computer',
      })
    }
    const takenOver = eventsOf(h, 'match.playerTakenOver')
    expect(takenOver).toHaveLength(1)
    // The resolver lost the match; two reports rebuild and feed it at the same time.
    resolver.matches.clear()
    const second = await playTurn(h, tokens.slice(0, 2), 2)
    const expected = fakeSealedHash(
      fakeHandoverHash(
        fakeSealedHash(fakeStartHash(seed), first),
        'match.playerTakenOver',
        third.player.id,
      ),
      second,
    )
    resolver.matches.clear()
    const match = await h.storage.matches.get(host.match.id)
    if (!match) throw new Error('no match')
    await Promise.all([
      h.kernel.referee.resolveThrough(match, 2),
      h.kernel.referee.resolveThrough(match, 2),
    ])
    expect((await h.storage.turns.get(host.match.id, 2))?.resolvedHash).toBe(expected)
    expect((await resolver.status(host.match.id))?.stateHash).toBe(expected)
  })

  it('checkpoints every ten turns and rebuilds a lost match from the newest checkpoint', async () => {
    const { h, resolver } = refereed()
    const { host, guest } = await h.startedMatch()
    const tokens = [host.token, guest.token]
    let state = fakeStartHash(await seedOf(h, host.match.id))
    for (let turn = 1; turn <= 11; turn++) {
      state = fakeSealedHash(state, await playTurn(h, tokens, turn))
    }
    const checkpoint = await h.kernel.snapshots.get(host.match.id, 10)
    expect(checkpoint.uploadedByPlayerId).toBe('server')
    expect((await h.storage.turns.get(host.match.id, 11))?.stateHash).toBe(state)

    resolver.matches.clear()
    const bootstraps = resolver.count('bootstrap')
    state = fakeSealedHash(state, await playTurn(h, tokens, 12))
    expect((await h.storage.turns.get(host.match.id, 12))?.stateHash).toBe(state)
    expect(resolver.count('restore')).toBe(1)
    expect(resolver.count('bootstrap')).toBe(bootstraps)
  })

  it('leaves the turn to the reports while the resolver fails, and decides it once it is back', async () => {
    const { h, resolver } = refereed()
    const { host, guest } = await h.startedMatch()
    const tokens = [host.token, guest.token]
    resolver.failure = new Error('the resolver Worker is unreachable')
    const digest = await playTurn(h, tokens, 1)
    const expected = fakeSealedHash(fakeStartHash(await seedOf(h, host.match.id)), digest)
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('sealed')

    // The reports disagree, and with no resolver that is a desync, as without a referee.
    await report(h, host.token, 1, expected)
    await report(h, guest.token, 1, HASH_B)
    expect(h.storage.statusOf(host.match.id)).toBe('desynced')

    // Back again: the next report resolves the turn, the server state decides it, the pause lifts
    // and only the seat that was off is told.
    resolver.failure = null
    await report(h, guest.token, 1, HASH_B)
    expect(await h.storage.turns.get(host.match.id, 1)).toMatchObject({
      status: 'confirmed',
      stateHash: expected,
    })
    expect(h.storage.statusOf(host.match.id)).toBe('running')
    expect(eventsOf(h, 'turn.diverged').map((event) => event.payload)).toEqual([
      { turn: 1, playerId: guest.player.id, stateHash: expected, reportedStateHash: HASH_B },
    ])
  })

  it('does not referee a match stored under a session version it does not play', async () => {
    const { h, resolver } = refereed({ sessionVersion: 2 })
    const { host, guest } = await h.startedMatch()
    const tokens = [host.token, guest.token]
    await playTurn(h, tokens, 1)
    expect((await h.storage.turns.get(host.match.id, 1))?.status).toBe('sealed')
    await report(h, host.token, 1, HASH_B)
    await report(h, guest.token, 1, HASH_B)
    expect((await h.storage.turns.get(host.match.id, 1))?.stateHash).toBe(HASH_B)
    expect(resolver.count('applyEvents')).toBe(0)
    expect(
      (await h.kernel.query.view((await h.principalOf(host.token)).match)).refereed,
    ).toBeUndefined()
  })

  it('finishes the match on the server state and still judges the last reports', async () => {
    const { h } = refereed({ finishAfterTurn: 1 })
    const { host, guest } = await h.startedMatch()
    const tokens = [host.token, guest.token]
    const digest = await playTurn(h, tokens, 1)
    const expected = fakeSealedHash(fakeStartHash(await seedOf(h, host.match.id)), digest)
    expect(h.storage.statusOf(host.match.id)).toBe('finished')

    await report(h, host.token, 1, expected)
    await report(h, guest.token, 1, HASH_B)
    expect(eventsOf(h, 'turn.diverged').map((event) => event.payload)).toEqual([
      { turn: 1, playerId: guest.player.id, stateHash: expected, reportedStateHash: HASH_B },
    ])
  })
})
